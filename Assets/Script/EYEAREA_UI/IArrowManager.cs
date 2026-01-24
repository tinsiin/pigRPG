using UnityEngine;

/// <summary>
/// バトル矢印UIの管理を抽象化するインターフェース。
/// Phase 3d: BattleSystemArrowManager.Instanceへの直接依存を解消するため。
/// </summary>
public interface IArrowManager
{
    /// <summary>
    /// 矢印要求をキューに追加
    /// </summary>
    void Enqueue(BaseStates actor, BaseStates target);

    /// <summary>
    /// 矢印要求をキューに追加（太さ指定）
    /// </summary>
    void Enqueue(BaseStates actor, BaseStates target, float thicknessPercent01);

    /// <summary>
    /// 次のグループに進む
    /// </summary>
    void Next();

    /// <summary>
    /// キューをクリア
    /// </summary>
    void ClearQueue();

    /// <summary>
    /// 全矢印の色を設定
    /// </summary>
    void SetColorsForAll(Color? colorMain = null, Color? colorSub = null);

    /// <summary>
    /// ステージテーマカラーを適用
    /// </summary>
    void ApplyStageThemeColors(Color main, Color sub);
}
