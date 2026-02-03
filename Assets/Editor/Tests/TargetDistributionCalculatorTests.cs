using NUnit.Framework;

/// <summary>
/// ITargetDistributionCalculator 実装のユニットテスト
/// </summary>
[TestFixture]
public class TargetDistributionCalculatorTests
{
    #region ExplosionDistributionCalculator Tests

    [Test]
    public void Explosion_VanguardGetsFirstValue()
    {
        var calc = new ExplosionDistributionCalculator();
        var spread = new float[] { 1.0f, 0.5f };

        var (ratio, front, back) = calc.Calculate(spread, 0, 0, isVanguard: true, totalTargets: 2);

        Assert.AreEqual(1.0f, ratio);
        Assert.AreEqual(0, front); // インデックス消費なし
        Assert.AreEqual(0, back);
    }

    [Test]
    public void Explosion_BacklineGetsSecondValue()
    {
        var calc = new ExplosionDistributionCalculator();
        var spread = new float[] { 1.0f, 0.5f };

        var (ratio, front, back) = calc.Calculate(spread, 0, 0, isVanguard: false, totalTargets: 2);

        Assert.AreEqual(0.5f, ratio);
        Assert.AreEqual(0, front);
        Assert.AreEqual(0, back);
    }

    [Test]
    public void Explosion_BacklineFallsBackToFirstWhenOnlyOneValue()
    {
        var calc = new ExplosionDistributionCalculator();
        var spread = new float[] { 0.8f };

        var (ratio, _, _) = calc.Calculate(spread, 0, 0, isVanguard: false, totalTargets: 1);

        Assert.AreEqual(0.8f, ratio);
    }

    [Test]
    public void Explosion_ReturnsOneForEmptyArray()
    {
        var calc = new ExplosionDistributionCalculator();

        var (ratio, _, _) = calc.Calculate(new float[0], 0, 0, isVanguard: true, totalTargets: 1);

        Assert.AreEqual(1f, ratio);
    }

    #endregion

    #region BeamDistributionCalculator Tests

    [Test]
    public void Beam_VanguardConsumesFromFront()
    {
        var calc = new BeamDistributionCalculator();
        var spread = new float[] { 1.0f, 0.8f, 0.6f };

        var (ratio1, front1, back1) = calc.Calculate(spread, 0, 0, isVanguard: true, totalTargets: 3);
        var (ratio2, front2, back2) = calc.Calculate(spread, front1, back1, isVanguard: true, totalTargets: 3);

        Assert.AreEqual(1.0f, ratio1);
        Assert.AreEqual(1, front1);
        Assert.AreEqual(0, back1);

        Assert.AreEqual(0.8f, ratio2);
        Assert.AreEqual(2, front2);
        Assert.AreEqual(0, back2);
    }

    [Test]
    public void Beam_BacklineConsumesFromBack()
    {
        var calc = new BeamDistributionCalculator();
        var spread = new float[] { 1.0f, 0.8f, 0.6f };

        var (ratio1, front1, back1) = calc.Calculate(spread, 0, 0, isVanguard: false, totalTargets: 3);
        var (ratio2, front2, back2) = calc.Calculate(spread, front1, back1, isVanguard: false, totalTargets: 3);

        Assert.AreEqual(0.6f, ratio1); // 末尾から
        Assert.AreEqual(0, front1);
        Assert.AreEqual(1, back1);

        Assert.AreEqual(0.8f, ratio2); // 末尾から2番目
        Assert.AreEqual(0, front2);
        Assert.AreEqual(2, back2);
    }

    [Test]
    public void Beam_MixedVanguardAndBackline()
    {
        var calc = new BeamDistributionCalculator();
        var spread = new float[] { 1.0f, 0.8f, 0.6f, 0.4f };

        // Vanguard first
        var (ratio1, front1, back1) = calc.Calculate(spread, 0, 0, isVanguard: true, totalTargets: 4);
        // Backline second
        var (ratio2, front2, back2) = calc.Calculate(spread, front1, back1, isVanguard: false, totalTargets: 4);
        // Vanguard third
        var (ratio3, front3, back3) = calc.Calculate(spread, front2, back2, isVanguard: true, totalTargets: 4);

        Assert.AreEqual(1.0f, ratio1); // front[0]
        Assert.AreEqual(0.4f, ratio2); // back[3]
        Assert.AreEqual(0.8f, ratio3); // front[1]

        Assert.AreEqual(2, front3);
        Assert.AreEqual(1, back3);
    }

    [Test]
    public void Beam_ReturnsOneWhenIndicesCollide()
    {
        var calc = new BeamDistributionCalculator();
        var spread = new float[] { 1.0f, 0.5f };

        // Consume front
        var (_, front1, back1) = calc.Calculate(spread, 0, 0, isVanguard: true, totalTargets: 2);
        // Consume back
        var (_, front2, back2) = calc.Calculate(spread, front1, back1, isVanguard: false, totalTargets: 2);
        // Try to consume more (should return 1f)
        var (ratio3, _, _) = calc.Calculate(spread, front2, back2, isVanguard: true, totalTargets: 2);

        Assert.AreEqual(1f, ratio3);
    }

    #endregion

    #region ThrowDistributionCalculator Tests

    [Test]
    public void Throw_VanguardConsumesFromBack()
    {
        var calc = new ThrowDistributionCalculator();
        var spread = new float[] { 1.0f, 0.8f, 0.6f };

        var (ratio1, front1, back1) = calc.Calculate(spread, 0, 0, isVanguard: true, totalTargets: 3);
        var (ratio2, front2, back2) = calc.Calculate(spread, front1, back1, isVanguard: true, totalTargets: 3);

        Assert.AreEqual(0.6f, ratio1); // 末尾から
        Assert.AreEqual(0, front1);
        Assert.AreEqual(1, back1);

        Assert.AreEqual(0.8f, ratio2); // 末尾から2番目
        Assert.AreEqual(0, front2);
        Assert.AreEqual(2, back2);
    }

    [Test]
    public void Throw_BacklineConsumesFromFront()
    {
        var calc = new ThrowDistributionCalculator();
        var spread = new float[] { 1.0f, 0.8f, 0.6f };

        var (ratio1, front1, back1) = calc.Calculate(spread, 0, 0, isVanguard: false, totalTargets: 3);
        var (ratio2, front2, back2) = calc.Calculate(spread, front1, back1, isVanguard: false, totalTargets: 3);

        Assert.AreEqual(1.0f, ratio1); // 先頭から
        Assert.AreEqual(1, front1);
        Assert.AreEqual(0, back1);

        Assert.AreEqual(0.8f, ratio2); // 先頭から2番目
        Assert.AreEqual(2, front2);
        Assert.AreEqual(0, back2);
    }

    [Test]
    public void Throw_MixedVanguardAndBackline()
    {
        var calc = new ThrowDistributionCalculator();
        var spread = new float[] { 1.0f, 0.8f, 0.6f, 0.4f };

        // Vanguard first (from back)
        var (ratio1, front1, back1) = calc.Calculate(spread, 0, 0, isVanguard: true, totalTargets: 4);
        // Backline second (from front)
        var (ratio2, front2, back2) = calc.Calculate(spread, front1, back1, isVanguard: false, totalTargets: 4);
        // Vanguard third (from back)
        var (ratio3, front3, back3) = calc.Calculate(spread, front2, back2, isVanguard: true, totalTargets: 4);

        Assert.AreEqual(0.4f, ratio1); // back[3]
        Assert.AreEqual(1.0f, ratio2); // front[0]
        Assert.AreEqual(0.6f, ratio3); // back[2]

        Assert.AreEqual(1, front3);
        Assert.AreEqual(2, back3);
    }

    #endregion

    #region RandomDistributionCalculator Tests

    [Test]
    public void Random_AlwaysConsumesFromBack()
    {
        var calc = new RandomDistributionCalculator();
        var spread = new float[] { 1.0f, 0.8f, 0.6f };

        var (ratio1, front1, back1) = calc.Calculate(spread, 0, 0, isVanguard: true, totalTargets: 3);
        var (ratio2, front2, back2) = calc.Calculate(spread, front1, back1, isVanguard: false, totalTargets: 3);

        Assert.AreEqual(0.6f, ratio1); // 末尾から（vanguard でも）
        Assert.AreEqual(0, front1);    // frontIndex は変わらない
        Assert.AreEqual(1, back1);

        Assert.AreEqual(0.8f, ratio2); // 末尾から2番目（backline でも同じ）
        Assert.AreEqual(0, front2);
        Assert.AreEqual(2, back2);
    }

    [Test]
    public void Random_IgnoresVanguardStatus()
    {
        var calc = new RandomDistributionCalculator();
        var spread = new float[] { 1.0f, 0.5f };

        var (ratioVanguard, _, _) = calc.Calculate(spread, 0, 0, isVanguard: true, totalTargets: 2);
        var (ratioBackline, _, _) = calc.Calculate(spread, 0, 0, isVanguard: false, totalTargets: 2);

        Assert.AreEqual(ratioVanguard, ratioBackline);
    }

    #endregion

    #region DefaultDistributionCalculator Tests

    [Test]
    public void Default_AlwaysReturnsOne()
    {
        var calc = new DefaultDistributionCalculator();

        var (ratio1, front1, back1) = calc.Calculate(new float[] { 0.5f, 0.3f }, 0, 0, isVanguard: true, totalTargets: 2);
        var (ratio2, front2, back2) = calc.Calculate(new float[] { 0.5f, 0.3f }, front1, back1, isVanguard: false, totalTargets: 2);

        Assert.AreEqual(1f, ratio1);
        Assert.AreEqual(1f, ratio2);
        Assert.AreEqual(0, front1);
        Assert.AreEqual(0, back1);
        Assert.AreEqual(0, front2);
        Assert.AreEqual(0, back2);
    }

    [Test]
    public void Default_DoesNotConsumeIndices()
    {
        var calc = new DefaultDistributionCalculator();

        var (_, front, back) = calc.Calculate(new float[] { 0.5f }, 5, 3, isVanguard: true, totalTargets: 1);

        Assert.AreEqual(5, front);
        Assert.AreEqual(3, back);
    }

    #endregion

    #region Null/Empty Array Tests

    [Test]
    public void AllCalculators_HandleNullArray()
    {
        var calculators = new ITargetDistributionCalculator[]
        {
            new ExplosionDistributionCalculator(),
            new BeamDistributionCalculator(),
            new ThrowDistributionCalculator(),
            new RandomDistributionCalculator(),
            new DefaultDistributionCalculator()
        };

        foreach (var calc in calculators)
        {
            var (ratio, front, back) = calc.Calculate(null, 0, 0, isVanguard: true, totalTargets: 1);
            Assert.AreEqual(1f, ratio, $"{calc.GetType().Name} should return 1f for null array");
        }
    }

    [Test]
    public void AllCalculators_HandleEmptyArray()
    {
        var calculators = new ITargetDistributionCalculator[]
        {
            new ExplosionDistributionCalculator(),
            new BeamDistributionCalculator(),
            new ThrowDistributionCalculator(),
            new RandomDistributionCalculator(),
            new DefaultDistributionCalculator()
        };

        foreach (var calc in calculators)
        {
            var (ratio, front, back) = calc.Calculate(new float[0], 0, 0, isVanguard: true, totalTargets: 1);
            Assert.AreEqual(1f, ratio, $"{calc.GetType().Name} should return 1f for empty array");
        }
    }

    #endregion
}
