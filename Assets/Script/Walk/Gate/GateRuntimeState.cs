using System;

[Serializable]
public sealed class GateRuntimeState
{
    public string GateId;
    public int ResolvedPosition;
    public bool IsCleared;
    public int CooldownRemaining;
    public int FailCount;

    public GateRuntimeState() { }

    public GateRuntimeState(string gateId, int resolvedPosition)
    {
        GateId = gateId;
        ResolvedPosition = resolvedPosition;
        IsCleared = false;
        CooldownRemaining = 0;
        FailCount = 0;
    }

    public GateRuntimeState Clone()
    {
        return new GateRuntimeState
        {
            GateId = this.GateId,
            ResolvedPosition = this.ResolvedPosition,
            IsCleared = this.IsCleared,
            CooldownRemaining = this.CooldownRemaining,
            FailCount = this.FailCount
        };
    }
}
