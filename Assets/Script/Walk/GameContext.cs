using System;
using System.Collections.Generic;
using RandomExtensions;

public sealed class GameContext
{
    private readonly Dictionary<string, bool> flags = new();
    private readonly Dictionary<string, int> counters = new();
    private readonly HashSet<string> tags = new();
    private readonly Dictionary<string, EncounterState> encounterStatesByTableId = new();
    private readonly Dictionary<EncounterSO, List<NormalEnemy>> encounterEnemies = new();
    // セーブデータから復元された敵状態。ロード時の復元専用（read-only after import）。
    // ランタイム中の状態はencounterEnemies内のNormalEnemyインスタンスが正。
    // エクスポート時はランタイムインスタンスから直接読む（ここは参照しない）。
    private readonly Dictionary<string, EnemyPersistenceData> _savedEnemyStates = new();
    private readonly Dictionary<CharacterId, StageBonus> stageBonuses = new();
    private readonly EncounterOverlayStack encounterOverlays = new();
    private bool isEncounterDisabled;

    public PlayersContext Players { get; private set; }
    public WalkCounters Counters { get; }
    public WalkState WalkState { get; }
    public IBattleRunner BattleRunner { get; set; }
    public IEventUI EventUI { get; set; }
    public IDialogueRunner DialogueRunner { get; set; }
    public EventEntryStateManager EventEntryStateManager { get; } = new();

    // 友情コンビ登録システム
    public FriendshipComboRegistry ComboRegistry { get; } = new();

    // Phase 2: Gate/Anchor integration
    public GateResolver GateResolver { get; set; }
    public AnchorManager AnchorManager { get; set; }
    public SideObjectSelector SideObjectSelector { get; set; }
    public CentralObjectSelector CentralObjectSelector { get; set; }
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

    public StageBonus GetStageBonus(CharacterId id)
    {
        return stageBonuses.TryGetValue(id, out var bonus) ? bonus : default;
    }

    public void SetStageBonus(CharacterId id, StageBonus bonus)
    {
        stageBonuses[id] = bonus;
    }

    public void AddStageBonus(CharacterId id, StageBonus bonus)
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
                    var copy = enemy.DeepCopy();

                    // セーブデータから全状態復元（コンボ・非コンボ問わず）
                    var saved = GetSavedEnemyState(encounter.Id, i);
                    if (saved != null)
                    {
                        copy.RestoreGuid(saved.EnemyGuid);
                        copy.RestoreState(saved);

                        // 復活状態の復元
                        if (saved.RebornState > 0)
                        {
                            EnemyRebornManager.Instance.RestoreRebornState(
                                copy, saved.RebornRemainingSteps,
                                saved.RebornLastProgress, (EnemyRebornState)saved.RebornState);
                        }
                    }

                    list.Add(copy);
                }
            }
            encounterEnemies[encounter] = list;
        }
        return list;
    }

    /// <summary>
    /// エンカウントテーブル内の全敵の生存比率を返す（0.0〜1.0）。
    /// 生存敵が少ないほどエンカウント頻度を下げるための補正値。
    /// 条件未解放のエントリはカウントから除外する（解放後に初めて母数に参入）。
    /// まだインスタンス化されていないエンカウントの敵は全員生存として扱う。
    /// </summary>
    public float GetAliveRatio(EncounterTableSO table)
    {
        if (table?.Entries == null || table.Entries.Length == 0) return 1f;

        var totalCount = 0;
        var aliveCount = 0;

        for (var i = 0; i < table.Entries.Length; i++)
        {
            var entry = table.Entries[i];
            if (entry?.Encounter == null) continue;

            // 条件未解放のエントリは母数に含めない
            if (!EncounterResolver.AreConditionsMet(entry.Conditions, this)) continue;

            if (encounterEnemies.TryGetValue(entry.Encounter, out var enemies) && enemies != null)
            {
                for (var j = 0; j < enemies.Count; j++)
                {
                    if (enemies[j] == null) continue;
                    totalCount++;
                    if (!enemies[j].Death() && !enemies[j].broken)
                        aliveCount++;
                }
            }
            else
            {
                // 未インスタンス化 → テンプレートの非nullエントリだけ全員生存扱い
                var templateList = entry.Encounter.EnemyList;
                if (templateList != null)
                {
                    for (var k = 0; k < templateList.Count; k++)
                    {
                        if (templateList[k] == null) continue;
                        totalCount++;
                        aliveCount++;
                    }
                }
            }
        }

        return totalCount == 0 ? 1f : (float)aliveCount / totalCount;
    }

    /// <summary>
    /// 全敵の個体状態をエクスポートする。
    /// EncounterSO → テンプレートインデックスの対応を保持して永続化に使用。
    /// </summary>
    public List<EnemyPersistenceData> ExportAllEnemyStates()
    {
        var result = new List<EnemyPersistenceData>();

        foreach (var kvp in encounterEnemies)
        {
            var encounter = kvp.Key;
            var runtimeList = kvp.Value;
            if (encounter == null || runtimeList == null) continue;

            var source = encounter.EnemyList;
            if (source == null) continue;

            // runtimeList は GetRuntimeEnemies で source の null をスキップして構築されるため、
            // 同じスキップ順序でインデックスを対応させる
            var runtimeIdx = 0;
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i] == null) continue;
                if (runtimeIdx >= runtimeList.Count) break;

                var enemy = runtimeList[runtimeIdx];
                runtimeIdx++;
                if (enemy == null) continue;

                // 相性値変動のシリアライズ
                List<BondDeltaEntry> bondDeltas = null;
                if (enemy.BondDeltas.Count > 0)
                {
                    bondDeltas = new List<BondDeltaEntry>();
                    foreach (var bd in enemy.BondDeltas)
                        bondDeltas.Add(new BondDeltaEntry { Guid = bd.Key, Delta = bd.Value });
                }

                // 復活状態の取得
                var rebornState = EnemyRebornManager.Instance.GetRebornState(enemy);

                result.Add(new EnemyPersistenceData
                {
                    EnemyGuid = enemy.EnemyGuid,
                    EncounterId = encounter.Id,
                    TemplateIndex = i,
                    IsBroken = enemy.broken,
                    HP = enemy.HP,
                    MentalHP = enemy.MentalHP,
                    LastEncounterProgress = enemy.LastEncounterProgress,
                    AdaptationRate = enemy.AdaptationRate,
                    AdaptationStartRate = enemy.AdaptationStartRate,
                    BattlesSinceProtocolSwitch = enemy.BattlesSinceProtocolSwitch,
                    RebornRemainingSteps = rebornState?.RemainingSteps ?? -1,
                    RebornLastProgress = rebornState?.LastProgress ?? -1,
                    RebornState = rebornState.HasValue ? (int)rebornState.Value.State : 0,
                    BondDeltas = bondDeltas
                });
            }
        }
        // 未訪問エンカウントの保存済み状態をマージ（ロード後に未訪問のまま再セーブした場合の消失を防止）
        var exportedKeys = new HashSet<string>();
        foreach (var entry in result)
            exportedKeys.Add($"{entry.EncounterId}:{entry.TemplateIndex}");

        foreach (var kvp in _savedEnemyStates)
        {
            if (!exportedKeys.Contains(kvp.Key))
                result.Add(kvp.Value);
        }

        return result;
    }

    /// <summary>
    /// ロード時にセーブデータから敵状態を取り込む（復元専用）
    /// </summary>
    public void ImportEnemyStates(List<EnemyPersistenceData> states)
    {
        _savedEnemyStates.Clear();
        if (states == null) return;
        foreach (var s in states)
        {
            if (s == null || string.IsNullOrEmpty(s.EncounterId)) continue;
            var key = $"{s.EncounterId}:{s.TemplateIndex}";
            _savedEnemyStates[key] = s;
        }
    }

    /// <summary>
    /// GetRuntimeEnemies内から呼ぶ。対応するセーブデータがあれば返す
    /// </summary>
    public EnemyPersistenceData GetSavedEnemyState(string encounterId, int templateIndex)
    {
        var key = $"{encounterId}:{templateIndex}";
        _savedEnemyStates.TryGetValue(key, out var data);
        return data;
    }

    /// <summary>
    /// ノード移動時に敵キャッシュと復元用データをクリアする
    /// </summary>
    public void ClearEnemyCache()
    {
        encounterEnemies.Clear();
        _savedEnemyStates.Clear();
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

    [Obsolete("Use EncounterOverlays.Push() instead")]
    public void PushEncounterOverlay(string id, float multiplier, int steps, bool persistent)
    {
        encounterOverlays.Push(id, multiplier, steps, persistent);
    }

    [Obsolete("Use EncounterOverlays.Remove() instead")]
    public void RemoveEncounterOverlay(string id)
    {
        encounterOverlays.Remove(id);
    }

    [Obsolete("Use EncounterOverlays.AdvanceStep() instead")]
    public void AdvanceEncounterOverlays()
    {
        encounterOverlays.AdvanceStep();
    }

    [Obsolete("Use EncounterOverlays.GetCombinedMultiplier() instead")]
    public float GetEncounterMultiplier()
    {
        return encounterOverlays.GetCombinedMultiplier();
    }
}
