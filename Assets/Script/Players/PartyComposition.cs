using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// パーティー編成の実装。
/// 注意: メンバーの順序は不定。
/// </summary>
public sealed class PartyComposition : IPartyComposition
{
    private readonly List<CharacterId> _members = new();
    private IReadOnlyList<CharacterId> _readOnlyMembers;

    public int MaxMembers => 3;

    /// <summary>外部からの変更を防ぐため AsReadOnly() で返す</summary>
    public IReadOnlyList<CharacterId> ActiveMemberIds
        => _readOnlyMembers ??= _members.AsReadOnly();

    public bool IsEmpty => _members.Count == 0;
    public bool IsFull => _members.Count >= MaxMembers;
    public int Count => _members.Count;

    public bool AddMember(CharacterId id)
    {
        if (!id.IsValid)
        {
            Debug.LogWarning($"PartyComposition.AddMember: 無効なID");
            return false;
        }
        if (IsFull)
        {
            Debug.LogWarning($"PartyComposition.AddMember: パーティーが満員です");
            return false;
        }
        if (Contains(id))
        {
            Debug.LogWarning($"PartyComposition.AddMember: {id} は既にパーティーにいます");
            return false;
        }
        _members.Add(id);
        return true;
    }

    public bool RemoveMember(CharacterId id)
    {
        var removed = _members.Remove(id);
        if (!removed)
        {
            Debug.LogWarning($"PartyComposition.RemoveMember: {id} はパーティーにいません");
        }
        return removed;
    }

    public void SetMembers(params CharacterId[] ids)
    {
        _members.Clear();
        foreach (var id in ids.Take(MaxMembers))
        {
            if (id.IsValid && !Contains(id))
            {
                _members.Add(id);
            }
        }
    }

    public bool Contains(CharacterId id) => _members.Contains(id);

    /// <summary>
    /// 固定メンバー（Geino, Noramlia, Sites）が何人いるかカウント
    /// </summary>
    public int CountOriginalMembers()
    {
        return _members.Count(id => id.IsOriginalMember);
    }
}
