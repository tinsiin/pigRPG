/// <summary>
/// ターゲットへのダメージ分散比率を計算するインターフェース。
/// AttackDistributionType ごとに実装を持つ。
/// </summary>
public interface ITargetDistributionCalculator
{
    /// <summary>
    /// 分散比率を計算する
    /// </summary>
    /// <param name="spreadValues">スキルの分散値配列</param>
    /// <param name="frontIndex">前方からの消費インデックス（vanguard用）</param>
    /// <param name="backIndex">後方からの消費インデックス（backline用）</param>
    /// <param name="isVanguard">対象が前のめり状態か</param>
    /// <param name="totalTargets">全ターゲット数</param>
    /// <returns>分散比率と次のfront/backインデックス</returns>
    (float ratio, int nextFrontIndex, int nextBackIndex) Calculate(
        float[] spreadValues,
        int frontIndex,
        int backIndex,
        bool isVanguard,
        int totalTargets);
}
