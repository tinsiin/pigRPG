using System;
using UnityEngine;

/// <summary>
/// ダイアログ状態のスナップショット。
/// 戻る機能で状態を復元するために使用。
/// </summary>
[Serializable]
public sealed class DialogueStateSnapshot
{
    public int StepIndex;
    public DisplayMode DisplayMode;
    public PortraitState LeftPortrait;
    public PortraitState RightPortrait;
    public bool HasBackground;
    public string BackgroundId;
    public Sprite CentralObjectSprite;

    public DialogueStateSnapshot() { }

    public DialogueStateSnapshot(int stepIndex, DialogueStep step, DisplayMode currentMode)
    {
        StepIndex = stepIndex;
        DisplayMode = step?.DisplayMode ?? currentMode;
        LeftPortrait = step?.LeftPortrait?.Clone();
        RightPortrait = step?.RightPortrait?.Clone();
        HasBackground = step?.HasBackground ?? false;
        BackgroundId = step?.BackgroundId;
    }

    /// <summary>
    /// ステップから状態を更新する（累積適用）。
    /// </summary>
    public void ApplyStep(DialogueStep step)
    {
        if (step == null) return;

        DisplayMode = step.DisplayMode;

        // 立ち絵は変更があれば更新（nullの場合はクリアとして扱う）
        // step.LeftPortrait != null -> 新しい立ち絵を設定
        // step.LeftPortrait == null で現在値と異なる -> クリア
        if (step.LeftPortrait != null)
        {
            LeftPortrait = step.LeftPortrait.Clone();
        }
        else if (!PortraitEquals(step.LeftPortrait, LeftPortrait))
        {
            // stepがnullで現在がnon-null -> クリア
            LeftPortrait = null;
        }

        if (step.RightPortrait != null)
        {
            RightPortrait = step.RightPortrait.Clone();
        }
        else if (!PortraitEquals(step.RightPortrait, RightPortrait))
        {
            // stepがnullで現在がnon-null -> クリア
            RightPortrait = null;
        }

        // 背景は常に適用（HasBackground=falseは背景非表示を意味する）
        HasBackground = step.HasBackground;
        BackgroundId = step.HasBackground ? step.BackgroundId : null;
    }

    private static bool PortraitEquals(PortraitState a, PortraitState b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.CharacterId == b.CharacterId && a.Expression == b.Expression;
    }

    /// <summary>
    /// 現在の状態をクローンする。
    /// </summary>
    public DialogueStateSnapshot Clone()
    {
        return new DialogueStateSnapshot
        {
            StepIndex = StepIndex,
            DisplayMode = DisplayMode,
            LeftPortrait = LeftPortrait?.Clone(),
            RightPortrait = RightPortrait?.Clone(),
            HasBackground = HasBackground,
            BackgroundId = BackgroundId,
            CentralObjectSprite = CentralObjectSprite  // 参照型なのでそのまま代入
        };
    }
}
