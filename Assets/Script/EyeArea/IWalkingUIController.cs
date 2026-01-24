using UnityEngine;

/// <summary>
/// 歩行システム関連のUI制御を担当するコントローラーのインターフェース。
/// Phase 4: WatchUIUpdateから歩行UI機能を分離。
/// </summary>
public interface IWalkingUIController
{
    /// <summary>
    /// ノードUIを適用（ステージ名表示、テーマカラー設定等）
    /// </summary>
    void ApplyNodeUI(string displayName, NodeUIHints hints);

    /// <summary>
    /// サイドオブジェクトの配置ルート
    /// </summary>
    RectTransform SideObjectRoot { get; }

    /// <summary>
    /// ステージ名テキストを直接設定
    /// </summary>
    void SetStageText(string text);
}
