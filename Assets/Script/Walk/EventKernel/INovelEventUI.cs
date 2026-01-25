using Cysharp.Threading.Tasks;

/// <summary>
/// ノベルパート用の拡張EventUIインターフェース。
/// 既存のIEventUIを継承し、立ち絵・背景・雑音・モード切替を追加。
/// </summary>
public interface INovelEventUI : IEventUI
{
    /// <summary>
    /// 左右の立ち絵を表示する。
    /// null指定で表示しない/非表示にする。
    /// トランジションはPortraitState.TransitionTypeで指定。
    /// </summary>
    UniTask ShowPortrait(PortraitState left, PortraitState right);

    /// <summary>
    /// 指定位置の立ち絵を非表示にする。
    /// </summary>
    UniTask HidePortrait(PortraitPosition position);

    /// <summary>
    /// 背景を表示する。
    /// </summary>
    UniTask ShowBackground(string backgroundId);

    /// <summary>
    /// 背景を非表示にする。
    /// </summary>
    UniTask HideBackground();

    /// <summary>
    /// 雑音を再生する。fire-and-forget。
    /// 各NoiseEntryのDelaySeconds/SpeedMultiplier/VerticalOffsetに従う。
    /// </summary>
    void PlayNoise(NoiseEntry[] entries);

    /// <summary>
    /// 全ての雑音を加速する（セリフ飛ばし時）。
    /// </summary>
    void AccelerateNoises();

    /// <summary>
    /// テキストを表示する。
    /// DisplayModeに応じて表示方法が変わる。
    /// </summary>
    UniTask ShowText(string speaker, string text);

    /// <summary>
    /// テキストボックスのモードを切り替える。
    /// </summary>
    UniTask SwitchTextBox(DisplayMode mode);

    /// <summary>
    /// 現在の表示モード。
    /// </summary>
    DisplayMode CurrentDisplayMode { get; }

    /// <summary>
    /// バックログを表示する。
    /// </summary>
    UniTask ShowBacklog(DialogueBacklog backlog);

    /// <summary>
    /// バックログを非表示にする。
    /// </summary>
    void HideBacklog();

    /// <summary>
    /// 戻るボタンの有効/無効を設定する。
    /// </summary>
    void SetBackButtonEnabled(bool enabled);

    /// <summary>
    /// 戻るボタンが押されたか（ポーリング用）。
    /// 呼び出し後にリセットされる。
    /// </summary>
    bool ConsumeBackRequest();

    /// <summary>
    /// バックログボタンが押されたか（ポーリング用）。
    /// 呼び出し後にリセットされる。
    /// </summary>
    bool ConsumeBacklogRequest();

    /// <summary>
    /// 状態を即座に復元する（戻る機能用）。
    /// トランジションなしで状態を適用。
    /// </summary>
    void RestoreState(DialogueStateSnapshot snapshot);

    /// <summary>
    /// リアクション可能テキストを設定する。
    /// </summary>
    /// <param name="richText">TMPリッチテキスト（色付き + linkタグ付き）</param>
    /// <param name="reactions">リアクションセグメント配列</param>
    /// <param name="onClicked">クリック時コールバック</param>
    void SetReactionText(string richText, ReactionSegment[] reactions, System.Action<ReactionSegment> onClicked);

    /// <summary>
    /// リアクション設定をクリアする。
    /// </summary>
    void ClearReactions();

    /// <summary>
    /// 全UI要素を非表示にする（リアクション終了時等）。
    /// </summary>
    UniTask HideAllAsync();

    /// <summary>
    /// 入力プロバイダーを取得する。
    /// </summary>
    INovelInputProvider InputProvider { get; }

    /// <summary>
    /// TabStateを切り替える（USERUI側の表示切替）。
    /// </summary>
    void SetTabState(TabState state);
}
