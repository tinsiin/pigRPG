using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// パーティーメンバーの追加・除外を行うエフェクト。
/// ストーリーイベントで使用。
///
/// 制約:
/// - 戦闘中は編成変更不可
/// - 解放済み（Roster登録済み）のキャラのみ追加可能
/// </summary>
[CreateAssetMenu(menuName = "Walk/Effects/Party Member")]
public sealed class PartyMemberEffect : EffectSO
{
    public enum Action
    {
        Add,    // パーティーに追加
        Remove  // パーティーから除外
    }

    [SerializeField] private string targetCharacterId;
    [SerializeField] private Action action;

    public override UniTask Apply(GameContext context)
    {
        // 戦闘中は編成変更不可
        if (BattleContextHub.Current != null)
        {
            Debug.LogWarning("PartyMemberEffect: 戦闘中はパーティー編成を変更できません");
            return UniTask.CompletedTask;
        }

        var players = context?.Players;
        var composition = players?.Composition;
        var roster = players?.Roster;

        if (composition == null || roster == null)
        {
            Debug.LogWarning("PartyMemberEffect: Players コンテキストが見つかりません");
            return UniTask.CompletedTask;
        }

        var id = new CharacterId(targetCharacterId);
        if (!id.IsValid)
        {
            Debug.LogWarning($"PartyMemberEffect: 無効なキャラクターID '{targetCharacterId}'");
            return UniTask.CompletedTask;
        }

        switch (action)
        {
            case Action.Add:
                // 解放済みチェック
                if (!roster.IsUnlocked(id))
                {
                    Debug.LogWarning($"PartyMemberEffect: 未解放キャラ '{id}' は追加できません");
                    return UniTask.CompletedTask;
                }
                if (composition.AddMember(id))
                {
                    Debug.Log($"パーティーに {id} を追加しました");
                }
                break;

            case Action.Remove:
                if (composition.RemoveMember(id))
                {
                    Debug.Log($"パーティーから {id} を除外しました");
                }
                break;
        }

        return UniTask.CompletedTask;
    }
}
