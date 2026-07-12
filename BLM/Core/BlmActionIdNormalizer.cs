namespace LosPr.BLM.Core;

public interface IBlmActionIdNormalizer
{
    uint Normalize(uint actionId);
}

public sealed class IdentityBlmActionIdNormalizer : IBlmActionIdNormalizer
{
    public static IdentityBlmActionIdNormalizer Instance { get; } = new();

    private IdentityBlmActionIdNormalizer()
    {
    }

    public uint Normalize(uint actionId) => actionId;
}

public sealed class PrBlmActionIdNormalizer : IBlmActionIdNormalizer
{
    public uint Normalize(uint actionId)
    {
        if (actionId == 0)
        {
            return 0;
        }

        try
        {
            var adjusted = ActionHelper.GetAdjustedActionId(actionId);
            return adjusted == 0 ? actionId : adjusted;
        }
        catch
        {
            return actionId;
        }
    }
}
