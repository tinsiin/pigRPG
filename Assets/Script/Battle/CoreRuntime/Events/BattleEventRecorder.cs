using System.Collections.Generic;

public sealed class BattleEventRecorder : IBattleEventSink
{
    private readonly List<BattleEvent> _events = new();
    private readonly List<BattleInputRecord> _inputs = new();
    public IReadOnlyList<BattleEvent> Events => _events;
    public IReadOnlyList<BattleInputRecord> Inputs => _inputs;
    public bool IsRecording { get; private set; }
    public int? RandomSeed { get; private set; }

    public void Start(int? randomSeed = null)
    {
        RandomSeed = randomSeed;
        IsRecording = true;
    }

    public void Stop()
    {
        IsRecording = false;
    }

    public void Clear()
    {
        _events.Clear();
        _inputs.Clear();
        RandomSeed = null;
    }

    public void OnBattleEvent(BattleEvent battleEvent)
    {
        if (!IsRecording) return;
        _events.Add(battleEvent);
    }

    public void RecordInput(BattleInput input, BaseStates actor, int turnCount, System.Collections.Generic.IReadOnlyList<BaseStates> allCharacters = null)
    {
        if (!IsRecording) return;
        _inputs.Add(BattleInputRecord.FromInput(input, actor, turnCount, allCharacters));
    }
}
