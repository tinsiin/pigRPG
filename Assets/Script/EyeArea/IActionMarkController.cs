using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ActionMark（行動対象マーカー）の制御インターフェース。
/// バトル中に現在の行動者を示すマーカーの移動・表示を管理する。
/// </summary>
public interface IActionMarkController
{
    /// <summary>指定アイコンへマーカーを移動</summary>
    void MoveToIcon(RectTransform targetIcon, bool immediate = false);

    /// <summary>指定アイコンへスケール考慮で移動</summary>
    void MoveToIconScaled(RectTransform targetIcon, bool immediate = false);

    /// <summary>指定キャラクターのアイコンへ移動</summary>
    void MoveToActor(BaseStates actor, bool immediate = false);

    /// <summary>指定キャラクターのアイコンへスケール考慮で移動（アニメーション待機可能）</summary>
    UniTask MoveToActorScaled(BaseStates actor, bool immediate = false, bool waitAnimations = true);

    /// <summary>マーカーを表示</summary>
    void Show();

    /// <summary>マーカーを非表示</summary>
    void Hide();

    /// <summary>スポーン位置からマーカーを表示（拡大アニメーション用）</summary>
    void ShowFromSpawn(bool zeroSize = true);
}
