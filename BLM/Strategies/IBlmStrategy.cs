namespace LosPr.BLM.Strategies;

public interface IBlmStrategy
{
    RotationMode Mode { get; }

    BlmDecision Resolve(BlmDecisionInput input);
}
