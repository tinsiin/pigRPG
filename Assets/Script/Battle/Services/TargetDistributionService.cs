using System.Collections.Generic;

/// <summary>
/// ターゲットへのダメージ分散を管理するサービス。
/// AttackDistributionType に応じた計算を行う。
/// </summary>
public sealed class TargetDistributionService
{
    private static readonly ITargetDistributionCalculator ExplosionCalc = new ExplosionDistributionCalculator();
    private static readonly ITargetDistributionCalculator BeamCalc = new BeamDistributionCalculator();
    private static readonly ITargetDistributionCalculator ThrowCalc = new ThrowDistributionCalculator();
    private static readonly ITargetDistributionCalculator RandomCalc = new RandomDistributionCalculator();
    private static readonly ITargetDistributionCalculator DefaultCalc = new DefaultDistributionCalculator();

    /// <summary>
    /// 分散タイプに応じた計算機を取得
    /// </summary>
    public static ITargetDistributionCalculator GetCalculator(AttackDistributionType type)
    {
        return type switch
        {
            AttackDistributionType.Explosion => ExplosionCalc,
            AttackDistributionType.Beam => BeamCalc,
            AttackDistributionType.Throw => ThrowCalc,
            AttackDistributionType.Random => RandomCalc,
            _ => DefaultCalc,
        };
    }

    /// <summary>
    /// ターゲットリストに対する分散比率を計算
    /// </summary>
    /// <param name="targets">ターゲットリスト</param>
    /// <param name="spreadValues">スキルの分散値配列</param>
    /// <param name="distributionType">分散タイプ</param>
    /// <param name="queryService">前のめり判定用サービス</param>
    /// <returns>各ターゲットの分散比率リスト</returns>
    public static List<float> CalculateDistribution(
        List<BaseStates> targets,
        float[] spreadValues,
        AttackDistributionType distributionType,
        IBattleQueryService queryService)
    {
        var result = new List<float>(targets.Count);
        var calculator = GetCalculator(distributionType);
        var frontIndex = 0;
        var backIndex = 0;

        foreach (var target in targets)
        {
            var isVanguard = queryService?.IsVanguard(target) ?? false;
            var (ratio, nextFront, nextBack) = calculator.Calculate(
                spreadValues, frontIndex, backIndex, isVanguard, targets.Count);
            result.Add(ratio);
            frontIndex = nextFront;
            backIndex = nextBack;
        }

        return result;
    }
}
