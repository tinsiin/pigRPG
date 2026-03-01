using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 友情コンビ登録データのランタイム管理。
/// GameContextに保持され、WalkProgressData経由で永続化される。
/// </summary>
public sealed class FriendshipComboRegistry
{
    private readonly List<FriendshipComboSaveData> _combos = new();
    private readonly Dictionary<string, EnemyPersistenceData> _enemyStates = new();

    // GUIDからコンビ検索のキャッシュ
    private readonly Dictionary<string, FriendshipComboSaveData> _guidToCombo = new();

    public IReadOnlyList<FriendshipComboSaveData> AllCombos => _combos;

    /// <summary>
    /// コンビを登録する
    /// </summary>
    public void Register(FriendshipComboSaveData combo)
    {
        if (combo == null) return;
        _combos.Add(combo);
        foreach (var guid in combo.MemberGuids)
        {
            _guidToCombo[guid] = combo;
        }
    }

    /// <summary>
    /// GUIDからコンビを検索。見つからなければnull。
    /// </summary>
    public FriendshipComboSaveData FindComboByMemberGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        _guidToCombo.TryGetValue(guid, out var combo);
        return combo;
    }

    /// <summary>
    /// 敵個体の状態を記録/更新する
    /// </summary>
    public void UpdateEnemyState(EnemyPersistenceData data)
    {
        if (data == null || string.IsNullOrEmpty(data.EnemyGuid)) return;
        _enemyStates[data.EnemyGuid] = data;
    }

    /// <summary>
    /// GUIDから敵個体の永続データを取得
    /// </summary>
    public EnemyPersistenceData GetEnemyState(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        _enemyStates.TryGetValue(guid, out var data);
        return data;
    }

    /// <summary>
    /// EncounterSO Id + テンプレートインデックスから永続データを取得
    /// </summary>
    public EnemyPersistenceData GetEnemyStateByEncounterIndex(string encounterId, int templateIndex)
    {
        foreach (var data in _enemyStates.Values)
        {
            if (data.EncounterId == encounterId && data.TemplateIndex == templateIndex)
                return data;
        }
        return null;
    }

    /// <summary>
    /// 敵をbrokenとしてマーク。コンビの有効メンバーから除外される。
    /// </summary>
    public void MarkBroken(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return;
        if (!_enemyStates.TryGetValue(guid, out var data))
        {
            data = new EnemyPersistenceData { EnemyGuid = guid };
            _enemyStates[guid] = data;
        }
        data.IsBroken = true;
    }

    /// <summary>
    /// コンビの有効メンバー数（非broken）を返す
    /// </summary>
    public int GetActiveMembers(FriendshipComboSaveData combo)
    {
        if (combo == null) return 0;
        int count = 0;
        foreach (var guid in combo.MemberGuids)
        {
            var state = GetEnemyState(guid);
            if (state == null || !state.IsBroken)
                count++;
        }
        return count;
    }

    /// <summary>
    /// セーブ用にエクスポート
    /// </summary>
    public FriendshipComboRegistrySaveData Export()
    {
        return new FriendshipComboRegistrySaveData
        {
            Combos = new List<FriendshipComboSaveData>(_combos),
            EnemyStates = new List<EnemyPersistenceData>(_enemyStates.Values)
        };
    }

    /// <summary>
    /// ロード時にインポート
    /// </summary>
    public void Import(FriendshipComboRegistrySaveData data)
    {
        Clear();
        if (data == null) return;

        if (data.EnemyStates != null)
        {
            foreach (var state in data.EnemyStates)
            {
                if (state != null && !string.IsNullOrEmpty(state.EnemyGuid))
                    _enemyStates[state.EnemyGuid] = state;
            }
        }

        if (data.Combos != null)
        {
            foreach (var combo in data.Combos)
            {
                if (combo != null)
                    Register(combo);
            }
        }
    }

    /// <summary>
    /// ニューゲーム時に全データをクリア
    /// </summary>
    public void Clear()
    {
        _combos.Clear();
        _enemyStates.Clear();
        _guidToCombo.Clear();
    }
}
