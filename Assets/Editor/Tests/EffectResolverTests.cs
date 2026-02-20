using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// EffectResolver のワイヤリングテスト
/// </summary>
[TestFixture]
public class EffectResolverTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly FieldInfo PipelineField =
        typeof(EffectResolver).GetField("_pipeline", PrivateInstance);

    private static readonly FieldInfo QueryServiceField =
        typeof(EffectResolver).GetField("_queryService", PrivateInstance);

    [Test]
    public void SetQueryService_InitializesPipeline()
    {
        var resolver = new EffectResolver();
        var queryService = new MockBattleQueryService();

        // Before SetQueryService
        var pipelineBefore = PipelineField?.GetValue(resolver);
        Assert.IsNull(pipelineBefore, "Pipeline should be null before SetQueryService");

        // Call SetQueryService
        resolver.SetQueryService(queryService);

        // After SetQueryService
        var pipelineAfter = PipelineField?.GetValue(resolver);
        Assert.IsNotNull(pipelineAfter, "Pipeline should be initialized after SetQueryService");
    }

    [Test]
    public void SetQueryService_StoresQueryService()
    {
        var resolver = new EffectResolver();
        var queryService = new MockBattleQueryService();

        resolver.SetQueryService(queryService);

        var storedService = QueryServiceField?.GetValue(resolver);
        Assert.AreSame(queryService, storedService, "QueryService should be stored");
    }

    [Test]
    public void NewEffectResolver_HasNullPipeline()
    {
        var resolver = new EffectResolver();

        var pipeline = PipelineField?.GetValue(resolver);

        Assert.IsNull(pipeline, "New EffectResolver should have null pipeline");
    }

    [Test]
    public void NewEffectResolver_HasNullQueryService()
    {
        var resolver = new EffectResolver();

        var service = QueryServiceField?.GetValue(resolver);

        Assert.IsNull(service, "New EffectResolver should have null query service");
    }

    /// <summary>
    /// テスト用のモック QueryService
    /// </summary>
    private sealed class MockBattleQueryService : IBattleQueryService
    {
        public bool IsVanguard(BaseStates chara) => false;
        public BattleGroup GetGroupForCharacter(BaseStates chara) => null;
        public Faction GetCharacterFaction(BaseStates chara) => Faction.Ally;
        public List<BaseStates> GetOtherAlliesAlive(BaseStates chara) => new List<BaseStates>();
        public bool IsFriend(BaseStates chara1, BaseStates chara2) => true;
        public BattleGroup FactionToGroup(Faction faction) => null;
    }
}
