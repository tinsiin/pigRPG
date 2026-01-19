using System;

[Serializable]
public sealed class EncounterStateData
{
    public string TableId;
    public bool IsInitialized;
    public int CooldownRemaining;
    public int GraceRemaining;
    public int Misses;
    public float PityAccumulated;

    public EncounterStateData() { }

    public EncounterStateData(string tableId, EncounterState state)
    {
        TableId = tableId;
        if (state != null)
        {
            IsInitialized = state.IsInitialized;
            CooldownRemaining = state.CooldownRemaining;
            GraceRemaining = state.GraceRemaining;
            Misses = state.Misses;
            PityAccumulated = state.PityAccumulated;
        }
    }

    public EncounterState ToState()
    {
        return new EncounterState
        {
            IsInitialized = IsInitialized,
            CooldownRemaining = CooldownRemaining,
            GraceRemaining = GraceRemaining,
            Misses = Misses,
            PityAccumulated = PityAccumulated
        };
    }
}
