using System.Collections.Generic;
using RandomExtensions;

public sealed class GameContext
{
    private readonly Dictionary<string, bool> flags = new();
    private readonly Dictionary<string, int> counters = new();
    private readonly HashSet<string> tags = new();
    private readonly Dictionary<string, EncounterState> encounterStatesByTableId = new();
    private readonly Dictionary<EncounterSO, List<NormalEnemy>> encounterEnemies = new();
    private readonly Dictionary<AllyId, StageBonus> stageBonuses = new();
    private readonly EncounterOverlayStack encounterOverlays = new();
    private bool isEncounterDisabled;

    public PlayersContext Players { get; private set; }
    public WalkCounters Counters { get; }
    public WalkState WalkState { get; }
    public IBattleRunner BattleRunner { get; set; }
    public IEventUI EventUI { get; set; }

    // Phase 2: Gate/Anchor integration
    public GateResolver GateResolver { get; set; }
    public AnchorManager AnchorManager { get; set; }
    public SideObjectSelector SideObjectSelector { get; set; }
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

    public bool HasTag(string tag)
    {
        return !string.IsNullOrEmpty(tag) && tags.Contains(tag);
    }

    public void AddTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        tags.Add(tag);
    }

    public void RemoveTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        tags.Remove(tag);
    }

    public IReadOnlyCollection<string> GetAllTags()
    {
        return tags;
    }

    public void RestoreTags(IEnumerable<string> tagList)
    {
        tags.Clear();
        if (tagList == null) return;
        foreach (var tag in tagList)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                tags.Add(tag);
            }
        }
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
        var tableId = table.TableId;
        if (string.IsNullOrEmpty(tableId))
        {
            // Fallback: use asset name + instance ID to avoid collisions between
            // duplicated assets with the same name in different folders
            tableId = $"{table.name}_{table.GetInstanceID()}";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning($"[Walk] EncounterTableSO '{table.name}' has empty TableId. " +
                "Consider setting a unique TableId to ensure consistent state across sessions.");
#endif
        }
        if (!encounterStatesByTableId.TryGetValue(tableId, out var state))
        {
            state = new EncounterState();
            encounterStatesByTableId[tableId] = state;
        }
        return state;
    }

    public List<EncounterStateData> ExportEncounterStates()
    {
        var list = new List<EncounterStateData>();
        foreach (var kvp in encounterStatesByTableId)
        {
            list.Add(new EncounterStateData(kvp.Key, kvp.Value));
        }
        return list;
    }

    public void ImportEncounterStates(List<EncounterStateData> dataList)
    {
        encounterStatesByTableId.Clear();
        if (dataList == null) return;
        foreach (var data in dataList)
        {
            if (string.IsNullOrEmpty(data.TableId)) continue;
            encounterStatesByTableId[data.TableId] = data.ToState();
        }
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

    public void SetRandomSeed(uint seed)
    {
        // Note: RandomEx.Shared doesn't support seeding directly.
        // For reproducibility, use WalkState.NodeSeed in deterministic calculations.
        _ = seed;
    }

    public int GetRandomInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return RandomEx.Shared.NextInt(minInclusive, maxExclusive);
    }

    public float GetRandomFloat()
    {
        return RandomEx.Shared.NextFloat();
    }

    public bool IsEncounterDisabled
    {
        get => isEncounterDisabled;
        set => isEncounterDisabled = value;
    }

    public EncounterOverlayStack EncounterOverlays => encounterOverlays;

    public void PushEncounterOverlay(string id, float multiplier, int steps, bool persistent)
    {
        encounterOverlays.Push(id, multiplier, steps, persistent);
    }

    public void RemoveEncounterOverlay(string id)
    {
        encounterOverlays.Remove(id);
    }

    public void AdvanceEncounterOverlays()
    {
        encounterOverlays.AdvanceStep();
    }

    public float GetEncounterMultiplier()
    {
        return encounterOverlays.GetCombinedMultiplier();
    }
}
