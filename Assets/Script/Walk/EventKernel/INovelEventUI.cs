using Cysharp.Threading.Tasks;

/// <summary>
/// ズーム制御インターフェース。
/// 中央オブジェクトへのズームイン/アウトとスプライト操作を担当。
/// </summary>
public interface INovelZoomUI
{
    /// <summary>
    /// 中央オブジェクトにズームインする。
    /// </summary>
    UniTask ZoomToCentralAsync(UnityEngine.RectTransform centralObjectRT, FocusArea focusArea);

    /// <summary>
    /// ズームを終了して原状復帰する。
    /// </summary>
    UniTask ExitZoomAsync();

    /// <summary>
    /// ズームを即座に原状復帰する（フェイルセーフ用）。
    /// </summary>
    void RestoreZoomImmediate();

    /// <summary>
    /// 中央オブジェクトのスプライトを変更する。
    /// characterId指定時はPortraitDatabaseから表情解決可能。
    /// </summary>
    void UpdateCentralObjectSprite(UnityEngine.Sprite sprite, string characterId = null, string expression = null);

    /// <summary>
    /// 現在の中央オブジェクトスプライトを取得する（スナップショット用）。
    /// </summary>
    UnityEngine.Sprite GetCurrentCentralObjectSprite();

    /// <summary>
    /// 現在の中央オブジェクトのキャラクターIDを取得する（雑音マッチング用）。
    /// </summary>
    string GetCurrentCentralObjectCharacterId();

    /// <summary>
    /// 現在の中央オブジェクトの表情IDを取得する（スナップショット用）。
    /// </summary>
    string GetCurrentCentralObjectExpression();
}

/// <summary>
/// リアクションシステムインターフェース。
/// セリフ内のクリッカブル要素の設定とクリアを担当。
/// </summary>
public interface INovelReactionUI
{
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
}

/// <summary>
/// ノベルパート用の統合EventUIインターフェース。
/// IEventUI + INovelZoomUI + INovelReactionUI を継承し、
/// 立ち絵・背景・雑音・モード切替・ナビゲーションを追加。
/// </summary>
public interface INovelEventUI : IEventUI, INovelZoomUI, INovelReactionUI
{
    /// <summary>
    /// 左右の立ち絵を表示する。nullで非表示。
    /// トランジションはPortraitState.TransitionTypeで指定。
    /// </summary>
    UniTask ShowPortrait(PortraitState left, PortraitState right);

    /// <summary>
    /// 指定位置の立ち絵をフェードアウトで非表示にする。
    /// </summary>
    UniTask HidePortrait(PortraitPosition position);

    /// <summary>
    /// 指定位置の立ち絵をスライドアウトで退場させる。
    /// </summary>
    UniTask ExitPortrait(PortraitPosition position);

    /// <summary>
    /// 背景を表示する。
    /// </summary>
    UniTask ShowBackground(string backgroundId);

    /// <summary>
    /// 背景を非表示にする。
    /// </summary>
    UniTask HideBackground();

    /// <summary>
    /// 雑音を再生する（fire-and-forget）。
    /// </summary>
    void PlayNoise(NoiseEntry[] entries);

    /// <summary>
    /// 残存する全雑音をEaseIn加速する（入力後の掃き出し用）。
    /// </summary>
    void AccelerateNoises();

    /// <summary>
    /// 雑音連動による一時表情をリセットする（ステップ境界で呼ぶ）。
    /// </summary>
    void ClearTemporaryExpressions();

    /// <summary>
    /// テキストを表示する。DisplayModeに応じて表示先が変わる。
    /// </summary>
    UniTask ShowText(string speaker, string text);

    /// <summary>
    /// テキストボックスのモードを切り替える（フェードアウト→フェードイン）。
    /// </summary>
    UniTask SwitchTextBox(DisplayMode mode);

    /// <summary>
    /// 現在のテキストボックスをフェードアウトのみ行う。
    /// モード切替時に立ち絵登場を間に挟むために使用。
    /// </summary>
    UniTask FadeOutCurrentTextBox();

    /// <summary>
    /// 新しいモードのテキストボックスをフェードインのみ行う。
    /// FadeOutCurrentTextBoxと対で使用。
    /// </summary>
    UniTask FadeInNewTextBox(DisplayMode mode);

    /// <summary>
    /// 現在の表示モード。
    /// </summary>
    DisplayMode CurrentDisplayMode { get; }

    /// <summary>
    /// バックログを表示し、閉じるまで待機する。
    /// </summary>
    UniTask ShowBacklog(DialogueBacklog backlog);

    /// <summary>
    /// バックログの1ページあたりのエントリ数。
    /// </summary>
    int BacklogLinesPerPage { get; }

    /// <summary>
    /// バックログの最大遡りページ数。
    /// </summary>
    int BacklogMaxBacktrackPages { get; }

    /// <summary>
    /// バックログを非表示にする。
    /// </summary>
    void HideBacklog();

    /// <summary>
    /// 戻るボタンの有効/無効を設定する。
    /// </summary>
    void SetBackButtonEnabled(bool enabled);

    /// <summary>
    /// 戻るボタンが押されたかを消費する（ポーリング用）。
    /// </summary>
    bool ConsumeBackRequest();

    /// <summary>
    /// バックログボタンが押されたかを消費する（ポーリング用）。
    /// </summary>
    bool ConsumeBacklogRequest();

    /// <summary>
    /// 状態を即座に復元する（戻る機能用、トランジションなし）。
    /// </summary>
    void RestoreState(DialogueStateSnapshot snapshot);

    /// <summary>
    /// 全UI要素を非表示にする。
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

    /// <summary>
    /// 主人公の精神属性を表示する（ディノイドモードのアイコン下）。nullで非表示。
    /// </summary>
    void SetProtagonistSpiritualProperty(SpiritualProperty? property);
}
