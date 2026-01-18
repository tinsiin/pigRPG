public sealed class WalkState
{
    public string CurrentNodeId { get; set; }
    public string LastExitId { get; set; }

    // Phase 2: Seed-based reproducibility
    public uint NodeSeed { get; set; }
    public int VarietyHistoryIndex { get; set; }

    public void SetCurrentNodeId(string nodeId)
    {
        CurrentNodeId = nodeId;
    }
}