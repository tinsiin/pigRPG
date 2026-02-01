using System.Reflection;
using NUnit.Framework;

/// <summary>
/// EscapeHandler unit tests for escape rate calculation.
/// </summary>
[TestFixture]
public class EscapeHandlerTests
{
    private static readonly MethodInfo GetRunOutRateMethod = typeof(EscapeHandler)
        .GetMethod("GetRunOutRateByCharacterImpression", BindingFlags.NonPublic | BindingFlags.Static);

    private static float GetRunOutRate(SpiritualProperty property)
    {
        return (float)GetRunOutRateMethod.Invoke(null, new object[] { property });
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_LiminalWhiteTile_Returns55()
    {
        var rate = GetRunOutRate(SpiritualProperty.liminalwhitetile);
        Assert.AreEqual(55f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_Kindergarden_Returns80()
    {
        var rate = GetRunOutRate(SpiritualProperty.kindergarden);
        Assert.AreEqual(80f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_Sacrifaith_Returns5()
    {
        var rate = GetRunOutRate(SpiritualProperty.sacrifaith);
        Assert.AreEqual(5f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_Cquiest_Returns25()
    {
        var rate = GetRunOutRate(SpiritualProperty.cquiest);
        Assert.AreEqual(25f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_Devil_Returns40()
    {
        var rate = GetRunOutRate(SpiritualProperty.devil);
        Assert.AreEqual(40f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_Doremis_Returns40()
    {
        var rate = GetRunOutRate(SpiritualProperty.doremis);
        Assert.AreEqual(40f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_Pillar_Returns10()
    {
        var rate = GetRunOutRate(SpiritualProperty.pillar);
        Assert.AreEqual(10f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_Godtier_Returns50()
    {
        var rate = GetRunOutRate(SpiritualProperty.godtier);
        Assert.AreEqual(50f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_Baledrival_Returns60()
    {
        var rate = GetRunOutRate(SpiritualProperty.baledrival);
        Assert.AreEqual(60f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_Pysco_Returns100()
    {
        var rate = GetRunOutRate(SpiritualProperty.pysco);
        Assert.AreEqual(100f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_None_Returns0()
    {
        var rate = GetRunOutRate(SpiritualProperty.none);
        Assert.AreEqual(0f, rate);
    }

    [Test]
    public void GetRunOutRateByCharacterImpression_UnknownValue_Returns0()
    {
        var rate = GetRunOutRate((SpiritualProperty)999);
        Assert.AreEqual(0f, rate);
    }
}
