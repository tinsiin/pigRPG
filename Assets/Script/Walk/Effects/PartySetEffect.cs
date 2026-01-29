using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// パーティーメンバーを一括設定するエフェクト。
/// ノード進入時などにパーティー編成を指定したメンバーに変更する。
///
/// 使用例:
/// - NodeSO.onEnterEvent → EventDefinitionSO.terminalEffects → PartySetEffect
/// - 特定ステージでは特定のパーティー編成を強制
///
/// 制約:
/// - 戦闘中は編成変更不可
/// - 解放済み（Roster登録済み）のキャラのみ設定可能
/// - 未解放キャラは無視される
/// </summary>
[CreateAssetMenu(menuName = "Walk/Effects/Party Set")]
public sealed class PartySetEffect : EffectSO
{
    [Header("パーティー設定")]
    [Tooltip("パーティーに設定するキャラクターID（順序は保持されない）")]
    [SerializeField] private string[] memberIds;

    [Header("オプション")]
    [Tooltip("trueの場合、既存のパーティーをクリアしてから設定。falseの場合、追加のみ（既存メンバーは維持）")]
    [SerializeField] private bool clearExisting = true;

    [Tooltip("未解放キャラがいた場合に警告を出すか")]
    [SerializeField] private bool warnOnUnlocked = true;

    public override UniTask Apply(GameContext context)
    {
        // 戦闘中は編成変更不可
        if (BattleContextHub.Current != null)
        {
            Debug.LogWarning("PartySetEffect: 戦闘中はパーティー編成を変更できません");
            return UniTask.CompletedTask;
        }

        var players = context?.Players;
        var composition = players?.Composition;
        var roster = players?.Roster;

        if (composition == null || roster == null)
        {
            Debug.LogWarning("PartySetEffect: Players コンテキストが見つかりません");
            return UniTask.CompletedTask;
        }

        if (memberIds == null || memberIds.Length == 0)
        {
            Debug.LogWarning("PartySetEffect: メンバーIDが設定されていません");
            return UniTask.CompletedTask;
        }

        // 解放済みかつ有効なIDのみを収集
        var validIds = new System.Collections.Generic.List<CharacterId>();
        foreach (var idStr in memberIds)
        {
            var id = new CharacterId(idStr);
            if (!id.IsValid)
            {
                Debug.LogWarning($"PartySetEffect: 無効なキャラクターID '{idStr}'");
                continue;
            }

            if (!roster.IsUnlocked(id))
            {
                if (warnOnUnlocked)
                {
                    Debug.LogWarning($"PartySetEffect: 未解放キャラ '{id}' は無視されます");
                }
                continue;
            }

            validIds.Add(id);
        }

        if (validIds.Count == 0)
        {
            Debug.LogWarning("PartySetEffect: 有効なメンバーがいません");
            return UniTask.CompletedTask;
        }

        // パーティーを設定
        if (clearExisting)
        {
            // 既存をクリアして新しいメンバーを設定
            composition.SetMembers(validIds.ToArray());
            Debug.Log($"PartySetEffect: パーティーを設定しました: {string.Join(", ", validIds)}");
        }
        else
        {
            // 追加のみ（既存メンバーは維持）
            int added = 0;
            foreach (var id in validIds)
            {
                if (!composition.Contains(id) && composition.AddMember(id))
                {
                    added++;
                }
            }
            Debug.Log($"PartySetEffect: {added} 人をパーティーに追加しました");
        }

        return UniTask.CompletedTask;
    }
}
