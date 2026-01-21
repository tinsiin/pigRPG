using System;

[Serializable]
public sealed class GateRuntimeState
{
    public string GateId;
    public int ResolvedPosition;
    public bool IsCleared;
    public int FailCount;

    public GateRuntimeState() { }

    public GateRuntimeState(string gateId, int resolvedPosition)
    {
        GateId = gateId;
        ResolvedPosition = resolvedPosition;
        IsCleared = false;
        FailCount = 0;
    }

    public GateRuntimeState Clone()
    {
        return new GateRuntimeState
        {
            GateId = this.GateId,
            ResolvedPosition = this.ResolvedPosition,
            IsCleared = this.IsCleared,
            FailCount = this.FailCount
        };
    }
}
