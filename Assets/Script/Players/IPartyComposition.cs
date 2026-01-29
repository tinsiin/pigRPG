using System;
using System.Collections.Generic;

/// <summary>
/// パーティー編成を管理するインターフェース。
/// 注意: メンバーの順序は不定。順序依存の処理を書かないこと。
/// </summary>
public interface IPartyComposition
{
    /// <summary>パーティー構成が変更された時に発火するイベント</summary>
    event Action OnMembershipChanged;
    /// <summary>パーティー最大人数</summary>
    int MaxMembers { get; }

    /// <summary>
    /// 現在のパーティーメンバーID一覧。
    /// 順序は不定。UI/バトル順序には依存しないこと。
    /// </summary>
    IReadOnlyList<CharacterId> ActiveMemberIds { get; }

    /// <summary>パーティーにメンバーを追加（戦闘外のみ）</summary>
    bool AddMember(CharacterId id);

    /// <summary>パーティーからメンバーを除外（戦闘外のみ）</summary>
    bool RemoveMember(CharacterId id);

    /// <summary>パーティーを一括設定（戦闘外のみ）</summary>
    void SetMembers(params CharacterId[] ids);

    /// <summary>メンバーがパーティーに参加中か</summary>
    bool Contains(CharacterId id);

    /// <summary>パーティーが空か</summary>
    bool IsEmpty { get; }

    /// <summary>パーティーが満員か</summary>
    bool IsFull { get; }

    /// <summary>現在のパーティー人数</summary>
    int Count { get; }
}
