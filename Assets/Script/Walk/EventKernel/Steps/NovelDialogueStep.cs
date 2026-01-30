using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ノベルパート（DialogueSO）を実行するEventStep。
/// ズーム責務はこのStep内で完結する。
/// </summary>
[Serializable]
public sealed class NovelDialogueStep : IEventStep
{
    [Header("ダイアログ参照")]
    [SerializeField] private DialogueSO dialogue;

    [Header("表示設定")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.Dinoid;
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private bool allowBacktrack = false;
    [SerializeField] private bool showBacklog = false;

    [Header("ズーム設定")]
    [Tooltip("trueの場合、Step側のズーム設定を使用。falseの場合、SO側の設定を使用。")]
    [SerializeField] private bool overrideZoom;
    [SerializeField] private bool zoomOnApproach = true;
    [SerializeField] private FocusArea focusArea;

    [Header("主人公設定（精神属性連携用）")]
    [Tooltip("精神属性の表示・変化対象となるパーティーメンバー")]
    [SerializeField] private bool hasProtagonist;
    [Tooltip("CharacterId（文字列）で指定")]
    [SerializeField] private string protagonistCharacterId;

    public DialogueSO Dialogue => dialogue;

    /// <summary>
    /// 主人公（精神属性連携用）をCharacterIdで取得。未設定の場合はnull。
    /// </summary>
    public CharacterId? GetProtagonistCharacterId()
    {
        if (!hasProtagonist) return null;

        if (!string.IsNullOrEmpty(protagonistCharacterId))
        {
            var id = new CharacterId(protagonistCharacterId);
            if (id.IsValid) return id;
        }

        return null;
    }
    public DisplayMode DisplayMode => displayMode;

    public NovelDialogueStep() { }

    public NovelDialogueStep(DialogueSO dialogueSO)
    {
        dialogue = dialogueSO;
        displayMode = dialogueSO?.DefaultMode ?? DisplayMode.Dinoid;
    }

    public async UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        var dialogueData = dialogue;
        if (dialogueData == null)
        {
            Debug.LogWarning("NovelDialogueStep: dialogue is null.");
            return Array.Empty<EffectSO>();
        }

        if (context.DialogueRunner == null)
        {
            Debug.LogWarning("NovelDialogueStep: DialogueRunner is null.");
            return Array.Empty<EffectSO>();
        }

        // ズーム設定の決定（Step側 or SO側）
        var shouldZoom = overrideZoom ? zoomOnApproach : dialogueData.ZoomOnApproach;
        var focus = overrideZoom ? focusArea : dialogueData.FocusArea;
        var canZoom = shouldZoom && context.NovelUI != null && context.CentralObjectRT != null;

        // 警告ログ: shouldZoom=true なのに CentralObjectRT=null の場合
        // データ不整合の早期発見用（課題4改善2）
        if (shouldZoom && context.CentralObjectRT == null)
        {
            Debug.LogWarning("[NovelDialogueStep] zoomOnApproach=true but CentralObjectRT is null. Zoom skipped. " +
                             "This may indicate the event was not triggered from a central object context.");
        }

        // ズームイン（Step内で制御）
        if (canZoom)
        {
            await context.NovelUI.ZoomToCentralAsync(context.CentralObjectRT, focus);
        }

        try
        {
            // ダイアログコンテキスト生成
            // 注: ズーム責務はStep側だが、CentralObjectRTは中央オブジェクトスプライト変更に必要なため渡す
            var dialogueContext = new DialogueContext(dialogueData, context.GameContext, displayMode, allowSkip)
            {
                AllowBacktrack = allowBacktrack,
                ShowBacklog = showBacklog,
                CentralObjectRT = context.CentralObjectRT,  // 3者会話構図で中央オブジェクトスプライト変更に使用
                // 精神属性連携用（CharacterId版を使用）
                ProtagonistCharacterId = GetProtagonistCharacterId(),
                Roster = context.GameContext?.Players?.Roster
            };

            // ダイアログ実行
            var result = await context.DialogueRunner.RunDialogueAsync(dialogueContext);

            // リアクション結果の処理
            return await ProcessDialogueResult(context, result);
        }
        finally
        {
            // ズームアウト（Step内で制御）
            if (canZoom)
            {
                await context.NovelUI.ExitZoomAsync();
            }
        }
    }

    private async UniTask<EffectSO[]> ProcessDialogueResult(EventContext context, DialogueResult result)
    {
        // リアクション終了でない場合は空
        if (!result.IsReactionEnded)
        {
            return Array.Empty<EffectSO>();
        }

        var reaction = result.TriggeredReaction;
        if (reaction == null)
        {
            return Array.Empty<EffectSO>();
        }

        // リアクションタイプに応じた処理
        switch (reaction.Type)
        {
            case ReactionType.Battle:
                if (reaction.Encounter != null && context.BattleRunner != null)
                {
                    // 戦闘を直接実行
                    var encounterContext = new EncounterContext(reaction.Encounter, context.GameContext);
                    var battleResult = await context.BattleRunner.RunBattleAsync(encounterContext);

                    // 戦闘結果に応じたEncounterSO側のイベントを実行
                    if (battleResult.Encountered && context.EventRunner != null)
                    {
                        var outcomeEvent = battleResult.Outcome switch
                        {
                            BattleOutcome.Victory => reaction.Encounter.OnWin,
                            BattleOutcome.Defeat => reaction.Encounter.OnLose,
                            BattleOutcome.Escape => reaction.Encounter.OnEscape,
                            _ => null
                        };

                        if (outcomeEvent != null)
                        {
                            return await context.EventRunner.RunAsync(outcomeEvent, context);
                        }
                    }
                }
                break;

            // 将来のリアクションタイプ対応
            // case ReactionType.Event:
            // case ReactionType.Custom:
        }

        return Array.Empty<EffectSO>();
    }
}
