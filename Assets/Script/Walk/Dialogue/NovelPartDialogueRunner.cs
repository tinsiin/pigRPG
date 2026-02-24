using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// IDialogueRunnerの実装。
/// DialogueSOを実行し、各ステップを順次処理する。
/// 戻る機能とバックログをサポート。
/// </summary>
public sealed class NovelPartDialogueRunner : IDialogueRunner
{
    /// <summary>
    /// ExecuteStepがバックログ表示により中断されたことを示す戻り値。
    /// </summary>
    private const int StepInterrupted = -2;

    private readonly INovelEventUI ui;
    private DialogueStep previousStep;
    private DialogueBacklog backlog;
    private List<DialogueStateSnapshot> snapshots;
    private bool allowBacktrack;
    private bool backRequested;
    private ReactionSegment reactionTriggered;

    public NovelPartDialogueRunner(INovelEventUI ui)
    {
        this.ui = ui;
    }

    private void OnReactionClicked(ReactionSegment segment)
    {
        reactionTriggered = segment;
        // 入力待ちをキャンセルして即座にリアクション処理へ進む
        ui.InputProvider?.CancelWait();
    }

    public async UniTask<DialogueResult> RunDialogueAsync(DialogueContext context)
    {
        if (context == null || !context.HasSteps)
        {
            return DialogueResult.FailedResult;
        }

        var result = new DialogueResult { Completed = true, SelectedChoiceIndex = -1 };
        previousStep = null;
        reactionTriggered = null;
        backRequested = false;

        // バックログと戻る機能の初期化
        allowBacktrack = context.AllowBacktrack;
        if (context.AllowBacktrack || context.ShowBacklog)
        {
            var maxEntries = ui.BacklogLinesPerPage * ui.BacklogMaxBacktrackPages;
            backlog = new DialogueBacklog(maxEntries);
        }
        else
        {
            backlog = null;
        }
        snapshots = context.AllowBacktrack ? new List<DialogueStateSnapshot>() : null;

        ui.SetBackButtonEnabled(false);
        ui.ClearReactions();

        // 前回の立ち絵・雑音などの内部状態をリセット
        // （TabState.walkで見た目は消えていても、Presenterの状態が残っていると
        //  同キャラ同表情判定でトランジションがスキップされるため）
        await ui.HideAllAsync();

        // 主人公の精神属性を表示（ディノイドモード用）
        var protagonistImpression = context.GetProtagonistImpression();
        ui.SetProtagonistSpiritualProperty(protagonistImpression);

        // 初期モード設定
        if (ui.CurrentDisplayMode != context.InitialMode)
        {
            await ui.SwitchTextBox(context.InitialMode);
        }

        // 注: ズーム責務はNovelDialogueStep側に一本化
        // CentralObjectRTは中央オブジェクトスプライト変更判定にのみ使用

        var steps = context.GetSteps();
        var currentIndex = 0;

        try
        {
            while (currentIndex < steps.Length)
            {
                var step = steps[currentIndex];
                if (step == null)
                {
                    currentIndex++;
                    continue;
                }

                // 戻るボタン有効化（2ステップ目以降、かつallowBacktrack）
                ui.SetBackButtonEnabled(allowBacktrack && currentIndex > 0);

                // 最後のステップではNextボタン無効化 + 閉じるボタン表示（イベント会話のみ）
                var isLastStep = allowBacktrack && currentIndex == steps.Length - 1;
                ui.SetNextButtonEnabled(!isLastStep);
                ui.SetCloseButtonVisible(isLastStep);

                var stepResult = await ExecuteStep(step, context, currentIndex);

                // バックログ表示により中断された場合、同じステップを再実行
                if (stepResult == StepInterrupted)
                {
                    continue;
                }

                // 状態スナップショット保存（ExecuteStep後、Effect実行後の状態を保存）
                if (snapshots != null && snapshots.Count <= currentIndex)
                {
                    var snapshot = new DialogueStateSnapshot(currentIndex, step, ui.CurrentDisplayMode);
                    if (previousStep != null)
                    {
                        // 前ステップの状態を引き継ぐ
                        var prevSnapshot = snapshots.Count > 0 ? snapshots[snapshots.Count - 1] : null;
                        if (prevSnapshot != null)
                        {
                            snapshot.LeftPortrait = prevSnapshot.LeftPortrait;
                            snapshot.RightPortrait = prevSnapshot.RightPortrait;
                            snapshot.HasBackground = prevSnapshot.HasBackground;
                            snapshot.BackgroundId = prevSnapshot.BackgroundId;
                            // 中央オブジェクトは継承しない（UIから実際の状態を取得する）
                        }
                    }
                    snapshot.ApplyStep(step);  // 既存：立ち絵/背景の累積更新

                    // 中央オブジェクトはApplyStepではなくUIから直接取得
                    snapshot.CentralObjectSprite = ui.GetCurrentCentralObjectSprite();
                    snapshot.CentralObjectCharacterId = ui.GetCurrentCentralObjectCharacterId();
                    snapshot.CentralObjectExpression = ui.GetCurrentCentralObjectExpression();

                    snapshots.Add(snapshot);
                }

                // リアクションがクリックされた場合 → リアクション終了
                if (reactionTriggered != null)
                {
                    Debug.Log($"[NovelPartDialogueRunner] Reaction triggered: {reactionTriggered.Text} (type={reactionTriggered.Type})");
                    await ui.HideAllAsync();
                    return DialogueResult.ReactionEndedResult(reactionTriggered);
                }

                // 戻るリクエストをチェック（NovelInputHubからの通知 or UIボタンからの通知）
                if (allowBacktrack && (backRequested || ui.ConsumeBackRequest()) && currentIndex > 0)
                {
                    // 戻れる範囲の制限
                    var maxBackSteps = ui.BacklogLinesPerPage * ui.BacklogMaxBacktrackPages;
                    var earliestAllowed = System.Math.Max(0, currentIndex - maxBackSteps);
                    if (currentIndex - 1 >= earliestAllowed)
                    {
                        backRequested = false;
                        currentIndex--;
                        var prevSnapshot = snapshots[currentIndex];
                        ui.RestoreState(prevSnapshot);
                        backlog?.TruncateTo(currentIndex);
                        previousStep = currentIndex > 0 ? steps[currentIndex - 1] : null;
                        continue;
                    }
                }
                backRequested = false;  // 使われなかった場合もリセット

                // バックログにエントリ追加
                backlog?.Add(currentIndex, step);

                if (step.HasChoices && stepResult >= 0)
                {
                    result.SelectedChoiceIndex = stepResult;

                    // 選択肢の精神属性を記録
                    var choices = step.Choices;
                    if (stepResult < choices.Length)
                    {
                        var choice = choices[stepResult];
                        if (!string.IsNullOrEmpty(choice?.SpiritProperty))
                        {
                            result.ChangedSpiritProperty = choice.SpiritProperty;
                        }

                        // 選択肢のEffectを実行
                        if (choice?.Effects != null)
                        {
                            await ApplyEffects(choice.Effects, context.GameContext);
                        }
                    }
                }

                previousStep = step;
                currentIndex++;
            }

            return result;
        }
        finally
        {
            // 正常終了・リアクション終了・例外中断いずれの場合も確実にクリーンアップ
            ui.ClearReactions();
            ui.SetBackButtonEnabled(false);
            ui.SetNextButtonEnabled(true);
            ui.SetCloseButtonVisible(false);
            ui.SetProtagonistSpiritualProperty(null);

            // 表示中の立ち絵をスライドアウトで退場させる（UIが消える前に）
            // Exit()はcurrentState==nullなら即returnするため、未表示側は安全にスキップ
            await UniTask.WhenAll(
                ui.ExitPortrait(PortraitPosition.Left),
                ui.ExitPortrait(PortraitPosition.Right)
            );

            // 注: ズームアウトはNovelDialogueStep側のfinallyで実行される
            ui.SetTabState(TabState.walk);
        }
    }

    private async UniTask<int> ExecuteStep(DialogueStep step, DialogueContext context, int stepIndex)
    {
        // 0. アニメーション前にUIを可視化（TabState設定）
        //    これがないと立ち絵トランジション等がUI非表示中に実行され、見えない
        ui.SetTabState(allowBacktrack ? TabState.EventDialogue : TabState.FieldDialogue);

        // 0.5. 前ステップの雑音による一時表情をリセット
        ui.ClearTemporaryExpressions();

        // 1. 背景変更
        if (ShouldUpdateBackground(step))
        {
            if (step.HasBackground)
            {
                await ui.ShowBackground(step.BackgroundId);
            }
            else
            {
                await ui.HideBackground();
            }
        }

        // 2. 立ち絵変更（Dinoid→Portrait切替時はステップ3で処理するためスキップ）
        var deferPortrait = ShouldSwitchMode(step) && step.DisplayMode == DisplayMode.Portrait;
        if (!deferPortrait && ShouldUpdatePortrait(step))
        {
            await ui.ShowPortrait(step.LeftPortrait, step.RightPortrait);
        }

        // 2.5. 中央オブジェクト変更
        if (step.HasCentralObjectChange && context.CentralObjectRT != null)
        {
            ui.UpdateCentralObjectSprite(step.CentralObjectSprite, step.CentralObjectCharacterId);
        }

        // 3. テキストボックス切り替え
        if (ShouldSwitchMode(step))
        {
            if (step.DisplayMode == DisplayMode.Dinoid)
            {
                // Portrait→Dinoid: 立ち絵退場 → テキストボックス切替
                await UniTask.WhenAll(
                    ui.ExitPortrait(PortraitPosition.Left),
                    ui.ExitPortrait(PortraitPosition.Right)
                );
                await ui.SwitchTextBox(step.DisplayMode);
            }
            else
            {
                // Dinoid→Portrait: テキストボックス閉 → 立ち絵登場 → テキストボックス開
                await ui.FadeOutCurrentTextBox();
                if (ShouldUpdatePortrait(step))
                {
                    await ui.ShowPortrait(step.LeftPortrait, step.RightPortrait);
                }
                await ui.FadeInNewTextBox(step.DisplayMode);
            }
        }

        // 4. テキスト表示 + 雑音発火（並列）
        if (!string.IsNullOrEmpty(step.Text))
        {
            // 雑音はfire-and-forget
            if (step.HasNoises)
            {
                ui.PlayNoise(step.Noises);
            }

            // リアクションがある場合はリッチテキストを設定
            if (step.HasReactions)
            {
                var richText = ReactionTextBuilder.Build(step.Text, step.Reactions);
                ui.SetReactionText(richText, step.Reactions, OnReactionClicked);
            }
            else
            {
                ui.ClearReactions();
                await ui.ShowText(step.Speaker, step.Text);
            }
        }
        else
        {
            ui.ClearReactions();
        }

        // 5. ユーザー入力待ち
        await WaitForInput(context);

        // 5.1. バックログリクエストで中断された場合、選択肢・Effectをスキップ
        if (backlog != null && ui.ConsumeBacklogRequest())
        {
            await ui.ShowBacklog(backlog);
            return StepInterrupted;
        }

        // 5.5. 雑音加速（入力後、残っている雑音を捌ける）
        ui.AccelerateNoises();

        // 6. 選択肢表示
        var selectedIndex = -1;
        if (step.HasChoices)
        {
            selectedIndex = await ShowChoices(step.Choices);
        }

        // 7. Effect実行
        if (step.HasEffects)
        {
            await ApplyEffects(step.Effects, context.GameContext);
        }

        return selectedIndex;
    }

    private bool ShouldUpdateBackground(DialogueStep current)
    {
        if (previousStep == null) return current.HasBackground;
        return previousStep.HasBackground != current.HasBackground
            || previousStep.BackgroundId != current.BackgroundId;
    }

    private bool ShouldUpdatePortrait(DialogueStep current)
    {
        if (previousStep == null)
        {
            return current.LeftPortrait != null || current.RightPortrait != null;
        }

        return !PortraitEquals(previousStep.LeftPortrait, current.LeftPortrait)
            || !PortraitEquals(previousStep.RightPortrait, current.RightPortrait);
    }

    private static bool PortraitEquals(PortraitState a, PortraitState b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.CharacterId == b.CharacterId && a.Expression == b.Expression;
    }

    private bool ShouldSwitchMode(DialogueStep current)
    {
        if (previousStep == null) return false;
        return previousStep.DisplayMode != current.DisplayMode;
    }

    private async UniTask WaitForInput(DialogueContext context)
    {
        var inputProvider = ui.InputProvider;
        if (inputProvider == null)
        {
            // フォールバック: 1フレーム待機
            await UniTask.Yield();
            return;
        }

        // 入力待ち（TabStateはExecuteStep冒頭で設定済み）
        try
        {
            if (allowBacktrack)
            {
                var isNext = await inputProvider.WaitForNextOrBackAsync();
                if (!isNext)
                {
                    // 戻るリクエストをセット（既存のConsumeBackRequestで処理される）
                    backRequested = true;
                }
            }
            else
            {
                await inputProvider.WaitForNextAsync();
            }
        }
        catch (System.OperationCanceledException)
        {
            // リアクションクリック等でキャンセルされた場合は正常終了
        }
    }

    private async UniTask<int> ShowChoices(DialogueChoice[] choices)
    {
        if (choices == null || choices.Length == 0) return -1;

        var labels = new string[choices.Length];
        var ids = new string[choices.Length];

        for (var i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            var label = choice?.Text ?? $"Choice {i}";

            // 精神属性があれば表示
            if (!string.IsNullOrEmpty(choice?.SpiritProperty))
            {
                label += $" [{choice.SpiritProperty}]";
            }

            labels[i] = label;
            ids[i] = i.ToString();
        }

        return await ui.ShowChoices(labels, ids);
    }

    private static async UniTask ApplyEffects(EffectSO[] effects, GameContext context)
    {
        if (effects == null || context == null) return;

        for (var i = 0; i < effects.Length; i++)
        {
            var effect = effects[i];
            if (effect == null) continue;
            await effect.Apply(context);
        }
    }
}
