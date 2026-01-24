using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// IDialogueRunnerの実装。
/// FieldDialogueSOを実行し、各ステップを順次処理する。
/// 戻る機能とバックログをサポート。
/// </summary>
public sealed class NovelPartDialogueRunner : IDialogueRunner
{
    private readonly INovelEventUI ui;
    private DialogueStep previousStep;
    private DialogueBacklog backlog;
    private List<DialogueStateSnapshot> snapshots;
    private bool allowBacktrack;
    private ReactionSegment reactionTriggered;

    public NovelPartDialogueRunner(INovelEventUI ui)
    {
        this.ui = ui;
    }

    private void OnReactionClicked(ReactionSegment segment)
    {
        reactionTriggered = segment;
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

        // バックログと戻る機能の初期化
        allowBacktrack = context.AllowBacktrack;
        backlog = context.AllowBacktrack || context.ShowBacklog ? new DialogueBacklog() : null;
        snapshots = context.AllowBacktrack ? new List<DialogueStateSnapshot>() : null;

        ui.SetBackButtonEnabled(false);
        ui.ClearReactions();

        // 初期モード設定
        if (ui.CurrentDisplayMode != context.InitialMode)
        {
            await ui.SwitchTextBox(context.InitialMode);
        }

        var steps = context.GetSteps();
        var currentIndex = 0;

        while (currentIndex < steps.Length)
        {
            var step = steps[currentIndex];
            if (step == null)
            {
                currentIndex++;
                continue;
            }

            // 戻るボタン有効化（2ステップ目以降、かつallowBacktrack）
            if (allowBacktrack && currentIndex > 0)
            {
                ui.SetBackButtonEnabled(true);
            }
            else
            {
                ui.SetBackButtonEnabled(false);
            }

            // 状態スナップショット保存
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
                    }
                }
                snapshot.ApplyStep(step);
                snapshots.Add(snapshot);
            }

            var stepResult = await ExecuteStep(step, context, currentIndex);

            // リアクションがクリックされた場合 → リアクション終了
            if (reactionTriggered != null)
            {
                Debug.Log($"[NovelPartDialogueRunner] Reaction triggered: {reactionTriggered.Text} (type={reactionTriggered.Type})");

                // ノベルパートを閉じる（終了）
                ui.ClearReactions();
                ui.SetBackButtonEnabled(false);
                await ui.HideAllAsync();

                // リアクション情報を含めて終了を返す
                return DialogueResult.ReactionEndedResult(reactionTriggered);
            }

            // 戻るリクエストをチェック
            if (allowBacktrack && ui.ConsumeBackRequest() && currentIndex > 0)
            {
                currentIndex--;
                var prevSnapshot = snapshots[currentIndex];
                ui.RestoreState(prevSnapshot);
                backlog?.TruncateTo(currentIndex);
                previousStep = currentIndex > 0 ? steps[currentIndex - 1] : null;
                continue;
            }

            // バックログリクエストをチェック
            if (backlog != null && ui.ConsumeBacklogRequest())
            {
                await ui.ShowBacklog(backlog);
                // バックログ閉じるまで待機
                await WaitForBacklogClose();
                continue; // 同じステップを再表示
            }

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

        ui.SetBackButtonEnabled(false);
        ui.ClearReactions();
        return result;
    }

    private async UniTask WaitForBacklogClose()
    {
        // バックログが閉じるまで待機
        while (true)
        {
            await UniTask.Yield();
            // バックログパネルが非アクティブになったら終了
            // または何らかの入力で閉じる
            break;
        }
        ui.HideBacklog();
    }

    private async UniTask<int> ExecuteStep(DialogueStep step, DialogueContext context, int stepIndex)
    {
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

        // 2. 立ち絵変更
        if (ShouldUpdatePortrait(step))
        {
            await ui.ShowPortrait(step.LeftPortrait, step.RightPortrait);
        }

        // 3. テキストボックス切り替え
        if (ShouldSwitchMode(step))
        {
            await ui.SwitchTextBox(step.DisplayMode);
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
        // TODO: 入力待ちの実装（クリック/タップ）
        // 今は簡易的に1フレーム待機
        await UniTask.Yield();
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
