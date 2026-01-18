using System.Collections.Generic;

public sealed class GameContext
{
    private readonly Dictionary<string, bool> flags = new();
    private readonly Dictionary<string, int> counters = new();
    private readonly Dictionary<EncounterTableSO, EncounterState> encounterStates = new();
    private readonly Dictionary<EncounterSO, List<NormalEnemy>> encounterEnemies = new();
    private readonly Dictionary<AllyId, StageBonus> stageBonuses = new();

    public PlayersContext Players { get; private set; }
    public WalkCounters Counters { get; }
    public WalkState WalkState { get; }
    public IBattleRunner BattleRunner { get; set; }

    // Phase 2: Gate/Anchor integration
    public GateResolver GateResolver { get; set; }
    public AnchorManager AnchorManager { get; set; }
    public NodeSO CurrentNode { get; set; }
    private bool _requestRefreshWithoutStep;
    public bool RequestRefreshWithoutStep
    {
        get => _requestRefreshWithoutStep;
        set
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (value != _requestRefreshWithoutStep)
            {
                UnityEngine.Debug.Log($"[Walk] RequestRefreshWithoutStep: {_requestRefreshWithoutStep} → {value}\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}");
            }
#endif
            _requestRefreshWithoutStep = value;
        }
    }
    public bool IsWalkingStep { get; set; }

    public GameContext(PlayersContext players)
    {
        Players = players;
        Counters = new WalkCounters();
        WalkState = new WalkState();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[Walk] GameContext created: RequestRefreshWithoutStep={RequestRefreshWithoutStep}, IsWalkingStep={IsWalkingStep}");
#endif
    }

    public void SetPlayersContext(PlayersContext players)
    {
        Players = players;
    }

    public bool HasFlag(string key)
    {
        return key != null && flags.TryGetValue(key, out var value) && value;
    }

    public void SetFlag(string key, bool value = true)
    {
        if (key == null) return;
        flags[key] = value;
    }

    public int GetCounter(string key)
    {
        return key != null && counters.TryGetValue(key, out var value) ? value : 0;
    }

    public void SetCounter(string key, int value)
    {
        if (key == null) return;
        counters[key] = value;
    }

    public StageBonus GetStageBonus(AllyId id)
    {
        return stageBonuses.TryGetValue(id, out var bonus) ? bonus : default;
    }

    public void SetStageBonus(AllyId id, StageBonus bonus)
    {
        stageBonuses[id] = bonus;
    }

    public void AddStageBonus(AllyId id, StageBonus bonus)
    {
        stageBonuses[id] = GetStageBonus(id) + bonus;
    }

    public EncounterState GetEncounterState(EncounterTableSO table)
    {
        if (table == null) return null;
        if (!encounterStates.TryGetValue(table, out var state))
        {
            state = new EncounterState();
            encounterStates[table] = state;
        }
        return state;
    }

    public IReadOnlyList<NormalEnemy> GetRuntimeEnemies(EncounterSO encounter)
    {
        if (encounter == null) return null;
        if (!encounterEnemies.TryGetValue(encounter, out var list) || list == null)
        {
            list = new List<NormalEnemy>();
            var source = encounter.EnemyList;
            if (source != null)
            {
                for (var i = 0; i < source.Count; i++)
                {
                    var enemy = source[i];
                    if (enemy == null) continue;
                    list.Add(enemy.DeepCopy());
                }
            }
            encounterEnemies[encounter] = list;
        }
        return list;
    }

    public IReadOnlyDictionary<string, bool> GetAllFlags()
    {
        return flags;
    }

    public IReadOnlyDictionary<string, int> GetAllCounters()
    {
        return counters;
    }

    public void RestoreFlags(IEnumerable<FlagEntry> entries)
    {
        flags.Clear();
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (entry == null || entry.Key == null) continue;
            flags[entry.Key] = entry.Value;
        }
    }

    public void RestoreCounters(IEnumerable<CounterEntry> entries)
    {
        counters.Clear();
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (entry == null || entry.Key == null) continue;
            counters[entry.Key] = entry.Value;
        }
    }
}
