using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// キャラクターを解放（アンロック）するエフェクト。
/// ストーリーイベントで新キャラクターを仲間にする際に使用。
///
/// 処理フロー:
/// 1. CharacterDataRegistryからキャラクターデータを取得
/// 2. AllyClassインスタンスを生成
/// 3. PlayersRosterに登録
///
/// 注意:
/// - 既に解放済みのキャラクターは無視される
/// - 解放後、パーティーに追加するにはPartyMemberEffectを別途使用
/// </summary>
[CreateAssetMenu(menuName = "Walk/Effects/Character Unlock")]
public sealed class CharacterUnlockEffect : EffectSO
{
    [Header("解放対象")]
    [Tooltip("解放するキャラクターのID（小文字英数字）")]
    [SerializeField] private string targetCharacterId;

    [Header("オプション")]
    [Tooltip("解放と同時にパーティーに追加するか")]
    [SerializeField] private bool addToPartyOnUnlock;

    [Tooltip("解放時に演出を再生するか（将来実装用）")]
    [SerializeField] private bool playUnlockAnimation;

    public override UniTask Apply(GameContext context)
    {
        var id = new CharacterId(targetCharacterId);
        if (!id.IsValid)
        {
            Debug.LogWarning($"CharacterUnlockEffect: 無効なキャラクターID '{targetCharacterId}'");
            return UniTask.CompletedTask;
        }

        var players = context?.Players;
        var roster = players?.Roster as PlayersRoster;
        var composition = players?.Composition;

        if (roster == null)
        {
            Debug.LogWarning("CharacterUnlockEffect: PlayersRoster が見つかりません");
            return UniTask.CompletedTask;
        }

        // 既に解放済みかチェック
        if (roster.IsUnlocked(id))
        {
            Debug.Log($"CharacterUnlockEffect: {id} は既に解放済みです");

            // 解放済みでもパーティー追加オプションが有効なら追加を試みる
            if (addToPartyOnUnlock && composition != null && !composition.Contains(id))
            {
                if (composition.AddMember(id))
                {
                    Debug.Log($"CharacterUnlockEffect: {id} をパーティーに追加しました");
                }
            }

            return UniTask.CompletedTask;
        }

        // CharacterDataRegistryからデータを取得
        var registry = players.CharacterRegistry;
        if (registry == null)
        {
            Debug.LogError("CharacterUnlockEffect: CharacterDataRegistry が見つかりません");
            return UniTask.CompletedTask;
        }

        var characterData = registry.GetCharacter(id);
        if (characterData == null)
        {
            Debug.LogWarning($"CharacterUnlockEffect: {id} のCharacterDataSOが見つかりません");
            return UniTask.CompletedTask;
        }

        // AllyClassインスタンスを生成
        var allyInstance = characterData.CreateInstance();
        if (allyInstance == null)
        {
            Debug.LogWarning($"CharacterUnlockEffect: {id} のAllyClassインスタンス生成に失敗しました");
            return UniTask.CompletedTask;
        }

        // Rosterに登録
        roster.RegisterAlly(id, allyInstance);
        Debug.Log($"CharacterUnlockEffect: {id} を解放しました");

        // TuningとSkillUIをバインド（PlayersRuntime.Init と同様）
        var tuning = players.Tuning;
        var skillUI = players.SkillUI;
        if (tuning != null) allyInstance.BindTuning(tuning);
        if (skillUI != null) allyInstance.BindSkillUI(skillUI);

        // スキル初期化
        allyInstance.OnInitializeSkillsAndChara();
        allyInstance.DecideDefaultMyImpression();

        // スキルボタンのバインド（UIControl経由）
        var uiControl = players.UIControl;
        uiControl?.BindNewCharacter(id);

        // パーティーに追加（オプション）
        if (addToPartyOnUnlock && composition != null)
        {
            if (composition.AddMember(id))
            {
                Debug.Log($"CharacterUnlockEffect: {id} をパーティーに追加しました");
            }
        }

        // TODO: 演出再生（playUnlockAnimation が true の場合）
        // 将来的にはUnlockAnimationController等を呼び出す

        return UniTask.CompletedTask;
    }
}
