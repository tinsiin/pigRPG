public sealed class EncounterState
{
    public bool IsInitialized { get; set; }
    public int CooldownRemaining { get; set; }
    public int GraceRemaining { get; set; }
    public int Misses { get; set; }
}
