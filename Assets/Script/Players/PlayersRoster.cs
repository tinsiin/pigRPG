using System.Collections.Generic;
using UnityEngine;

public sealed class PlayersRoster : IPlayersRoster
{
    private readonly Dictionary<CharacterId, AllyClass> _allies = new();

    /// <summary>解放済みキャラ数</summary>
    public int AllyCount => _allies.Count;

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

    /// <summary>全解放済みキャラクターIDの一覧</summary>
    public IEnumerable<CharacterId> AllIds => _allies.Keys;

    /// <summary>全キャラクターをクリアする（ロード時の再構築用）</summary>
    public void Clear()
    {
        _allies.Clear();
    }

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
}
