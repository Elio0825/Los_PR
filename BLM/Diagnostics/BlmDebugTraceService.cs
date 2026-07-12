using System.Diagnostics;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LosPr.BLM.Diagnostics;

public sealed record BlmDebugTraceOptions
{
    public int QueueCapacity { get; init; } = 4096;
    public int RecentEventCapacity { get; init; } = 300;
    public long MaximumFileBytes { get; init; } = 8L * 1024 * 1024;
    public int RetainedFileCount { get; init; } = 5;

    internal void Validate()
    {
        if (QueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity));
        }

        if (RecentEventCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RecentEventCapacity));
        }

        if (MaximumFileBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumFileBytes));
        }

        if (RetainedFileCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RetainedFileCount));
        }
    }
}

public sealed class BlmDebugTraceService : IBlmDebugSink, IBlmDebugViewSource, IDisposable
{
    private const int DecisionDedupeWindowMs = 2000;
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(5);
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly object _gate = new();
    private readonly string _logDirectory;
    private readonly Func<bool> _fileLoggingEnabled;
    private readonly BlmDebugTraceOptions _options;
    private readonly BlockingCollection<BlmDebugEvent> _diskQueue;
    private readonly ManualResetEventSlim _idle = new(initialState: true);
    private readonly Queue<BlmDebugEvent> _recent = new();
    private readonly Dictionary<uint, string> _targetKeys = new();
    private readonly Dictionary<string, (string Fingerprint, long AtMs)> _decisionDedupe =
        new(StringComparer.Ordinal);
    private readonly Task _consumer;
    private readonly string _sessionId;
    private readonly string _filePrefix;

    private StreamWriter? _writer;
    private string _currentFilePath = string.Empty;
    private string _lastError = string.Empty;
    private long _currentFileBytes;
    private long _nextEventSequence;
    private long _acceptedCount;
    private long _writtenCount;
    private long _droppedCount;
    private int _pendingWrites;
    private int _segmentIndex;
    private bool _disposed;

    public BlmDebugTraceService(
        string logDirectory,
        Func<bool> fileLoggingEnabled,
        BlmDebugTraceOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        ArgumentNullException.ThrowIfNull(fileLoggingEnabled);

        _options = options ?? new BlmDebugTraceOptions();
        _options.Validate();
        _logDirectory = Path.GetFullPath(logDirectory);
        _fileLoggingEnabled = fileLoggingEnabled;
        _sessionId = Guid.NewGuid().ToString("N")[..12];
        _filePrefix = $"Los-BLM-{DateTime.UtcNow:yyyyMMdd-HHmmss}-p{Environment.ProcessId}";
        _diskQueue = new BlockingCollection<BlmDebugEvent>(
            new ConcurrentQueue<BlmDebugEvent>(),
            _options.QueueCapacity);
        _consumer = Task.Factory.StartNew(
            Consume,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public void Publish(BlmDebugEventDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var debugEvent = Freeze(draft);
            if (ShouldDedupe(debugEvent))
            {
                return;
            }

            debugEvent = debugEvent with { EventSequence = ++_nextEventSequence };
            AppendRecent(debugEvent);
            _acceptedCount++;
            if (!ReadFileLoggingEnabled())
            {
                return;
            }

            if (TryQueue(debugEvent))
            {
                return;
            }

            if (_diskQueue.TryTake(out _))
            {
                _droppedCount++;
                CompletePendingWrite();
            }

            if (!TryQueue(debugEvent))
            {
                _droppedCount++;
            }

            AppendLoggerEvent(
                BlmDebugEventKind.LoggerDropped,
                $"后台日志队列已满，累计丢弃 {_droppedCount} 条待写事件。",
                _droppedCount);
        }
    }

    public BlmDebugSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new BlmDebugSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                FileLoggingEnabled = ReadFileLoggingEnabled(),
                WriterHealthy = string.IsNullOrEmpty(_lastError),
                LogDirectory = _logDirectory,
                CurrentFilePath = _currentFilePath,
                PendingCount = Volatile.Read(ref _pendingWrites),
                AcceptedCount = _acceptedCount,
                WrittenCount = Interlocked.Read(ref _writtenCount),
                DroppedCount = _droppedCount,
                LastError = _lastError,
                RecentEvents = BlmDebugSnapshot.Freeze(_recent.ToArray()),
            };
        }
    }

    public void ClearView()
    {
        lock (_gate)
        {
            _recent.Clear();
        }
    }

    public bool TryOpenLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _logDirectory,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception exception)
        {
            RecordError("打开日志目录失败", exception);
            return false;
        }
    }

    public bool Flush(TimeSpan timeout)
    {
        if (timeout < Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        return _idle.Wait(timeout);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _diskQueue.CompleteAdding();
        }

        try
        {
            if (!_consumer.Wait(DisposeTimeout))
            {
                RecordError("关闭 Debug 日志消费者超时", new TimeoutException());
            }
        }
        catch (Exception exception)
        {
            RecordError("关闭 Debug 日志消费者失败", exception);
        }
    }

    private BlmDebugEvent Freeze(BlmDebugEventDraft draft)
    {
        var context = draft.Context ?? BlmContext.Unavailable;
        var tracker = context.Tracker ?? BlmTrackerSnapshot.Empty;
        var monotonicMs = draft.MonotonicMs > 0
            ? draft.MonotonicMs
            : context.CapturedAtMs;

        return new BlmDebugEvent
        {
            Utc = DateTimeOffset.UtcNow,
            MonotonicMs = monotonicMs,
            SessionId = _sessionId,
            Kind = draft.Kind,
            EntryPoint = BlmDebugText.Clean(draft.EntryPoint),
            ActionId = draft.ActionId,
            ActionName = BlmActionNames.Get(draft.ActionId),
            NormalizedActionId = draft.NormalizedActionId,
            GlobalSequence = draft.GlobalSequence,
            Accepted = draft.Accepted,
            PActionType = BlmDebugText.Clean(draft.PActionType),
            RuleId = BlmDebugText.Clean(draft.RuleId),
            Reason = BlmDebugText.Clean(draft.Reason),
            Detail = BlmDebugText.Clean(draft.Detail),
            TargetKey = ResolveTargetKey(draft.TargetEntityId),
            CombatSerial = tracker.CombatSerial,
            StateGeneration = tracker.StateGeneration,
            FirePhaseSerial = tracker.FirePhaseSerial,
            IcePhaseSerial = tracker.IcePhaseSerial,
            DroppedCount = draft.DroppedCount,
            Resources = BlmDebugResourceSnapshot.FromContext(context),
            Resolver = FreezeResolver(draft.Resolver),
        };
    }

    private BlmDebugResolverSnapshot? FreezeResolver(BlmDebugResolverDraft? draft)
    {
        if (draft is null)
        {
            return null;
        }

        return new BlmDebugResolverSnapshot
        {
            FrameSequence = draft.FrameSequence,
            FrameCapturedAtMs = draft.FrameCapturedAtMs,
            FrameStateGeneration = draft.FrameStateGeneration,
            Channel = draft.Channel,
            CandidateActionId = draft.CandidateActionId,
            DeliverableActionId = draft.DeliverableActionId,
            TargetKey = ResolveTargetKey(draft.TargetEntityId),
            TargetKind = draft.TargetKind,
            ResolverId = BlmDebugText.Clean(draft.ResolverId),
            CheckCode = draft.CheckCode,
            HoldGcdForTranspose = draft.HoldGcdForTranspose,
            GcdBlockedByTransposeHold = draft.GcdBlockedByTransposeHold,
            DeliveryBlocked = draft.DeliveryBlocked,
            BlockReason = BlmDebugText.Clean(draft.BlockReason),
            HighPriorityQueueActive = draft.HighPriorityQueueActive,
            RemainingWeaves = draft.RemainingWeaves,
            FactCoverage = BlmDebugText.Clean(draft.FactCoverage),
        };
    }

    private bool ShouldDedupe(BlmDebugEvent debugEvent)
    {
        if (debugEvent.Kind is not BlmDebugEventKind.Decision
            and not BlmDebugEventKind.ResolverFrame)
        {
            return false;
        }

        var entryPoint = debugEvent.EntryPoint;
        var resolver = debugEvent.Resolver;
        var fingerprint = debugEvent.Kind == BlmDebugEventKind.ResolverFrame
            && resolver is not null
            ? string.Join(
                '|',
                debugEvent.StateGeneration,
                resolver.Channel,
                resolver.CandidateActionId,
                resolver.DeliverableActionId,
                resolver.TargetKey,
                resolver.TargetKind,
                resolver.ResolverId,
                resolver.CheckCode,
                resolver.HoldGcdForTranspose,
                resolver.GcdBlockedByTransposeHold,
                resolver.DeliveryBlocked,
                resolver.BlockReason,
                resolver.HighPriorityQueueActive,
                resolver.RemainingWeaves,
                resolver.FactCoverage)
            : string.Join(
                '|',
                debugEvent.ActionId,
                debugEvent.RuleId,
                debugEvent.Reason,
                debugEvent.StateGeneration);
        var duplicate = _decisionDedupe.TryGetValue(entryPoint, out var previous)
            && string.Equals(fingerprint, previous.Fingerprint, StringComparison.Ordinal)
            && debugEvent.MonotonicMs >= previous.AtMs
            && debugEvent.MonotonicMs - previous.AtMs < DecisionDedupeWindowMs;
        if (!duplicate)
        {
            _decisionDedupe[entryPoint] = (fingerprint, debugEvent.MonotonicMs);
        }

        return duplicate;
    }

    private void AppendRecent(BlmDebugEvent debugEvent)
    {
        _recent.Enqueue(debugEvent);
        while (_recent.Count > _options.RecentEventCapacity)
        {
            _recent.Dequeue();
        }
    }

    private void AppendLoggerEvent(
        BlmDebugEventKind kind,
        string detail,
        long droppedCount = 0)
    {
        AppendRecent(new BlmDebugEvent
        {
            EventSequence = ++_nextEventSequence,
            Utc = DateTimeOffset.UtcNow,
            SessionId = _sessionId,
            Kind = kind,
            Detail = BlmDebugText.Clean(detail),
            DroppedCount = droppedCount,
        });
    }

    private bool TryQueue(BlmDebugEvent debugEvent)
    {
        _idle.Reset();
        Interlocked.Increment(ref _pendingWrites);
        try
        {
            if (_diskQueue.TryAdd(debugEvent))
            {
                return true;
            }
        }
        catch (InvalidOperationException)
        {
        }

        CompletePendingWrite();
        return false;
    }

    private void Consume()
    {
        try
        {
            foreach (var debugEvent in _diskQueue.GetConsumingEnumerable())
            {
                try
                {
                    WriteEvent(debugEvent);
                }
                finally
                {
                    CompletePendingWrite();
                }
            }
        }
        finally
        {
            CloseWriter();
        }
    }

    private void WriteEvent(BlmDebugEvent debugEvent)
    {
        try
        {
            var line = JsonSerializer.Serialize(debugEvent, JsonOptions);
            var bytes = Utf8WithoutBom.GetByteCount(line) + 1L;
            if (_writer is not null
                && _currentFileBytes > 0
                && _currentFileBytes + bytes > _options.MaximumFileBytes)
            {
                CloseWriter();
            }

            EnsureWriter();
            _writer!.Write(line);
            _writer.Write('\n');
            _writer.Flush();
            _currentFileBytes += bytes;
            Interlocked.Increment(ref _writtenCount);
            lock (_gate)
            {
                _lastError = string.Empty;
            }
        }
        catch (Exception exception)
        {
            RecordError("写入 Debug 日志失败", exception);
            CloseWriter();
        }
    }

    private void EnsureWriter()
    {
        if (_writer is not null)
        {
            return;
        }

        Directory.CreateDirectory(_logDirectory);
        string path;
        do
        {
            path = Path.Combine(
                _logDirectory,
                _segmentIndex == 0
                    ? $"{_filePrefix}.jsonl"
                    : $"{_filePrefix}-{_segmentIndex:D2}.jsonl");
            _segmentIndex++;
        }
        while (File.Exists(path));

        var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            16 * 1024,
            FileOptions.SequentialScan);
        _writer = new StreamWriter(stream, Utf8WithoutBom, 16 * 1024, leaveOpen: false);
        _currentFileBytes = 0;
        lock (_gate)
        {
            _currentFilePath = path;
        }

        CleanupRetention(path);
    }

    private void CleanupRetention(string currentPath)
    {
        try
        {
            var files = Directory.GetFiles(_logDirectory, "Los-BLM-*.jsonl");
            Array.Sort(files, static (left, right) =>
                File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)));

            var otherFiles = files
                .Where(file => !string.Equals(
                    file,
                    currentPath,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var retainedOtherFiles = Math.Max(0, _options.RetainedFileCount - 1);
            for (var index = retainedOtherFiles; index < otherFiles.Length; index++)
            {
                try
                {
                    File.Delete(otherFiles[index]);
                }
                catch (IOException)
                {
                    // Another game instance can still own this file. Never delete active logs.
                }
            }
        }
        catch (Exception exception)
        {
            RecordError("清理旧 Debug 日志失败", exception);
        }
    }

    private void CloseWriter()
    {
        var writer = _writer;
        _writer = null;
        if (writer is null)
        {
            return;
        }

        try
        {
            writer.Dispose();
        }
        catch (Exception exception)
        {
            RecordError("关闭 Debug 日志失败", exception);
        }
    }

    private bool ReadFileLoggingEnabled()
    {
        try
        {
            return _fileLoggingEnabled();
        }
        catch (Exception exception)
        {
            RecordErrorCore("读取 Debug 日志开关失败", exception);
            return false;
        }
    }

    private string ResolveTargetKey(uint targetEntityId)
    {
        if (targetEntityId == 0)
        {
            return string.Empty;
        }

        if (_targetKeys.TryGetValue(targetEntityId, out var key))
        {
            return key;
        }

        key = $"T{_targetKeys.Count + 1:D3}";
        _targetKeys[targetEntityId] = key;
        return key;
    }

    private void RecordError(string operation, Exception exception)
    {
        lock (_gate)
        {
            RecordErrorCore(operation, exception);
        }
    }

    private void RecordErrorCore(string operation, Exception exception)
    {
        _lastError = $"{operation}: {exception.GetType().Name}";
        AppendLoggerEvent(BlmDebugEventKind.LoggerError, _lastError);
    }

    private void CompletePendingWrite()
    {
        if (Interlocked.Decrement(ref _pendingWrites) == 0)
        {
            _idle.Set();
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
