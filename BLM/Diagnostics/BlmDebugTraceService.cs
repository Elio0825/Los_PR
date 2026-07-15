using System.Diagnostics;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LosPr.BLM.Diagnostics;

internal sealed record BlmDebugTraceOptions
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

internal sealed class BlmDebugTraceService : IBlmDebugSink, IBlmDebugViewSource, IDisposable
{
    private const int DecisionDedupeWindowMs = 2000;
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(5);
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly object _gate = new();
    private readonly object _retentionGate = new();
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
    private StreamWriter? _readableWriter;
    private Task _retentionCleanup = Task.CompletedTask;
    private string _currentFilePath = string.Empty;
    private string _currentReadableFilePath = string.Empty;
    private string _pendingRetentionPath = string.Empty;
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
            var fileLoggingEnabled = ReadFileLoggingEnabled();
            var lastError = Volatile.Read(ref _lastError);
            return new BlmDebugSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                FileLoggingEnabled = fileLoggingEnabled,
                WriterHealthy = string.IsNullOrEmpty(lastError),
                LogDirectory = _logDirectory,
                CurrentFilePath = Volatile.Read(ref _currentFilePath),
                ReadableFilePath = Volatile.Read(ref _currentReadableFilePath),
                PendingCount = Volatile.Read(ref _pendingWrites),
                AcceptedCount = _acceptedCount,
                WrittenCount = Interlocked.Read(ref _writtenCount),
                DroppedCount = _droppedCount,
                LastError = lastError,
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

        Task retentionCleanup;
        lock (_retentionGate)
        {
            retentionCleanup = _retentionCleanup;
        }

        try
        {
            if (!retentionCleanup.Wait(DisposeTimeout))
            {
                RecordError("关闭 Debug 日志保留清理超时", new TimeoutException());
            }
        }
        catch (Exception exception)
        {
            RecordError("关闭 Debug 日志保留清理失败", exception);
        }
    }

    private BlmDebugEvent Freeze(BlmDebugEventDraft draft)
    {
        var context = draft.Context ?? BlmContext.Unavailable;
        var tracker = context.Tracker ?? BlmTrackerSnapshot.Empty;
        var monotonicMs = draft.MonotonicMs > 0
            ? draft.MonotonicMs
            : context.CapturedAtMs;

        var debugEvent = new BlmDebugEvent
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
        return debugEvent with
        {
            Summary = BlmDebugHumanFormatter.BuildSummary(debugEvent),
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
            GcdBlockedByAlwaysBridge = draft.GcdBlockedByAlwaysBridge,
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
                resolver.GcdBlockedByAlwaysBridge,
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
        var debugEvent = new BlmDebugEvent
        {
            EventSequence = ++_nextEventSequence,
            Utc = DateTimeOffset.UtcNow,
            SessionId = _sessionId,
            Kind = kind,
            Detail = BlmDebugText.Clean(detail),
            DroppedCount = droppedCount,
        };
        AppendRecent(debugEvent with
        {
            Summary = BlmDebugHumanFormatter.BuildSummary(debugEvent),
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
        catch (Exception exception)
        {
            RecordConsumerFailure("Debug 日志消费者意外终止", exception);
            throw;
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
            var readableBlock = BlmDebugHumanFormatter.FormatDocument(debugEvent);
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
            _readableWriter!.Write(readableBlock);
            _writer.Flush();
            _readableWriter.Flush();
            _currentFileBytes += bytes;
            Interlocked.Increment(ref _writtenCount);
            Volatile.Write(ref _lastError, string.Empty);
            SchedulePendingRetentionCleanup();
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
        while (File.Exists(path) || File.Exists(Path.ChangeExtension(path, ".log")));

        var readablePath = Path.ChangeExtension(path, ".log");
        StreamWriter? writer = null;
        StreamWriter? readableWriter = null;
        try
        {
            writer = CreateWriter(path);
            readableWriter = CreateWriter(readablePath);
            WriteReadableHeader(readableWriter);
        }
        catch
        {
            writer?.Dispose();
            readableWriter?.Dispose();
            if (writer is not null)
                TryDeleteCreatedFile(path);
            if (readableWriter is not null)
                TryDeleteCreatedFile(readablePath);
            throw;
        }

        _writer = writer;
        _readableWriter = readableWriter;
        _currentFileBytes = 0;
        Volatile.Write(ref _currentFilePath, path);
        Volatile.Write(ref _currentReadableFilePath, readablePath);
        _pendingRetentionPath = path;
    }

    private void SchedulePendingRetentionCleanup()
    {
        var currentPath = _pendingRetentionPath;
        if (currentPath.Length == 0)
        {
            return;
        }

        _pendingRetentionPath = string.Empty;
        lock (_retentionGate)
        {
            _retentionCleanup = _retentionCleanup.ContinueWith(
                antecedent =>
                {
                    ObserveRetentionAntecedent(antecedent);
                    var activePath = Volatile.Read(ref _currentFilePath);
                    CleanupRetention(activePath.Length == 0 ? currentPath : activePath);
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    private void ObserveRetentionAntecedent(Task antecedent)
    {
        if (antecedent.IsCompletedSuccessfully)
        {
            return;
        }

        try
        {
            antecedent.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException exception)
        {
            RecordError("前序 Debug 日志保留清理已取消", exception);
        }
        catch (Exception exception)
        {
            RecordError("前序 Debug 日志保留清理失败", exception);
        }
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
                    TryDeleteCompanionLog(otherFiles[index]);
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
        var readableWriter = _readableWriter;
        _writer = null;
        _readableWriter = null;
        DisposeWriter(writer, "关闭 Debug JSONL 失败");
        DisposeWriter(readableWriter, "关闭易读 Debug 日志失败");
    }

    private void DisposeWriter(StreamWriter? writer, string operation)
    {
        if (writer is null)
            return;

        try
        {
            writer.Dispose();
        }
        catch (Exception exception)
        {
            RecordError(operation, exception);
        }
    }

    private static StreamWriter CreateWriter(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            16 * 1024,
            FileOptions.SequentialScan);
        try
        {
            return new StreamWriter(stream, Utf8WithoutBom, 16 * 1024, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private void WriteReadableHeader(StreamWriter writer)
    {
        writer.WriteLine("Los 黑魔职业调试日志（易读版）");
        writer.WriteLine("用途：供玩家直接阅读；同名 .jsonl 保存完整原始数据，反馈问题时请两份一起提供。");
        writer.WriteLine("说明：时间使用本地时间；英文规则 ID、检查码和序列号属于底层诊断必要信息。");
        writer.WriteLine($"会话：{_sessionId}");
        writer.WriteLine("================================================================================");
        writer.WriteLine();
    }

    private static void TryDeleteCompanionLog(string jsonlPath)
    {
        var readablePath = Path.ChangeExtension(jsonlPath, ".log");
        try
        {
            if (File.Exists(readablePath))
                File.Delete(readablePath);
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteCreatedFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
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
        var detail = SetErrorState(operation, exception);
        lock (_gate)
        {
            AppendLoggerEvent(BlmDebugEventKind.LoggerError, detail);
        }
    }

    private void RecordErrorCore(string operation, Exception exception)
    {
        var detail = SetErrorState(operation, exception);
        AppendLoggerEvent(BlmDebugEventKind.LoggerError, detail);
    }

    private void RecordConsumerFailure(string operation, Exception exception)
    {
        var detail = SetErrorState(operation, exception);
        if (!Monitor.TryEnter(_gate))
        {
            return;
        }

        try
        {
            AppendLoggerEvent(BlmDebugEventKind.LoggerError, detail);
        }
        finally
        {
            Monitor.Exit(_gate);
        }
    }

    private string SetErrorState(string operation, Exception exception)
    {
        var detail = $"{operation}: {exception.GetType().Name}";
        Volatile.Write(ref _lastError, detail);
        return detail;
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
