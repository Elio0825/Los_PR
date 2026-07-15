namespace LosPr.BLM.Core;

internal interface IBlmActionIdNormalizer
{
    uint Normalize(uint actionId);
}

internal sealed class IdentityBlmActionIdNormalizer : IBlmActionIdNormalizer
{
    public static IdentityBlmActionIdNormalizer Instance { get; } = new();

    private IdentityBlmActionIdNormalizer()
    {
    }

    public uint Normalize(uint actionId) => actionId;
}

internal sealed class PrBlmActionIdNormalizer : IBlmActionIdNormalizer
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
