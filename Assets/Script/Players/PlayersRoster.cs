using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PlayersRoster : IPlayersRoster
{
    // === 新しい Dictionary ベースのストレージ ===
    private readonly Dictionary<CharacterId, AllyClass> _allies = new();

    // === 互換性用の配列ビュー（既存コード用） ===
    private AllyClass[] _alliesArray;
    private bool _arrayDirty = true;

    /// <summary>解放済みキャラ数</summary>
    public int AllyCount => _allies.Count;

    /// <summary>
    /// 互換性用: 配列形式でのアクセス。
    /// 順序は Geino, Noramlia, Sites の固定順。
    /// </summary>
    public AllyClass[] Allies
    {
        get
        {
            if (_arrayDirty)
            {
                RebuildArray();
            }
            return _alliesArray;
        }
    }

    /// <summary>配列キャッシュを再構築</summary>
    private void RebuildArray()
    {
        // 固定順序: Geino, Noramlia, Sites
        _alliesArray = new AllyClass[3];
        _alliesArray[0] = GetAlly(CharacterId.Geino);
        _alliesArray[1] = GetAlly(CharacterId.Noramlia);
        _alliesArray[2] = GetAlly(CharacterId.Sites);
        _arrayDirty = false;
    }

    // === 新しい CharacterId ベースのAPI ===

    /// <summary>キャラクターを登録（解放/加入）</summary>
    public void RegisterAlly(CharacterId id, AllyClass ally)
    {
        if (!id.IsValid)
        {
            Debug.LogWarning($"PlayersRoster.RegisterAlly: 無効なキャラクターID");
            return;
        }
        if (ally == null)
        {
            Debug.LogWarning($"PlayersRoster.RegisterAlly: ally が null です");
            return;
        }
        ally.SetCharacterId(id);
        _allies[id] = ally;
        _arrayDirty = true;
    }

    /// <summary>キャラクターを取得</summary>
    public AllyClass GetAlly(CharacterId id)
    {
        return _allies.TryGetValue(id, out var ally) ? ally : null;
    }

    /// <summary>全解放済みキャラクター</summary>
    public IEnumerable<AllyClass> AllAllies => _allies.Values;

    /// <summary>
    /// 解放済みキャラクターを固定順序で取得（インデックスアクセス用）。
    /// Geino, Noramlia, Sites の順序で、登録済みのものだけ返す。
    /// </summary>
    public IReadOnlyList<AllyClass> OrderedAllies
    {
        get
        {
            var list = new List<AllyClass>(3);
            if (_allies.TryGetValue(CharacterId.Geino, out var geino)) list.Add(geino);
            if (_allies.TryGetValue(CharacterId.Noramlia, out var noramlia)) list.Add(noramlia);
            if (_allies.TryGetValue(CharacterId.Sites, out var sites)) list.Add(sites);
            return list;
        }
    }

    /// <summary>解放済みキャラクターID一覧</summary>
    public IEnumerable<CharacterId> AllCharacterIds => _allies.Keys;

    /// <summary>キャラクターが解放済みか</summary>
    public bool IsUnlocked(CharacterId id) => _allies.ContainsKey(id);

    /// <summary>キャラクターIDを逆引き</summary>
    public bool TryGetCharacterId(BaseStates actor, out CharacterId id)
    {
        foreach (var kvp in _allies)
        {
            if (ReferenceEquals(kvp.Value, actor))
            {
                id = kvp.Key;
                return true;
            }
        }
        id = default;
        return false;
    }

    // === 互換性レイヤー（既存コード用、段階的に廃止） ===

    /// <summary>互換性用: 配列で一括設定</summary>
    [Obsolete("RegisterAlly を使用してください")]
    public void SetAllies(AllyClass[] value)
    {
        _allies.Clear();
        if (value != null)
        {
            if (value.Length > 0 && value[0] != null)
                RegisterAlly(CharacterId.Geino, value[0]);
            if (value.Length > 1 && value[1] != null)
                RegisterAlly(CharacterId.Noramlia, value[1]);
            if (value.Length > 2 && value[2] != null)
                RegisterAlly(CharacterId.Sites, value[2]);
        }
    }

    /// <summary>互換性用: インデックスでアクセス</summary>
    public bool TryGetAllyIndex(BaseStates actor, out int index)
    {
        index = -1;
        if (actor == null) return false;

        var arr = Allies;
        for (int i = 0; i < arr.Length; i++)
        {
            if (ReferenceEquals(arr[i], actor))
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    /// <summary>互換性用: インデックスでアクセス</summary>
    public BaseStates GetAllyByIndex(int index)
    {
        var arr = Allies;
        if (index < 0 || index >= arr.Length) return null;
        return arr[index];
    }

    /// <summary>互換性用: AllyId で取得</summary>
    public bool TryGetAllyId(BaseStates actor, out AllyId id)
    {
        id = default;
        if (TryGetAllyIndex(actor, out var idx) && Enum.IsDefined(typeof(AllyId), idx))
        {
            id = (AllyId)idx;
            return true;
        }
        return false;
    }

    /// <summary>互換性用: AllyId で取得</summary>
    public BaseStates GetAllyById(AllyId id)
    {
        var charId = CharacterId.FromAllyId(id);
        return GetAlly(charId);
    }
}
