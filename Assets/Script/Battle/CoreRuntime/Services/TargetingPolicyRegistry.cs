using System;
using System.Collections.Generic;

public interface ITargetingPolicy
{
    bool TrySelect(TargetingPolicyContext context, out List<BaseStates> targets);
}

public sealed class TargetingPolicyContext
{
    public BaseStates Acter { get; }
    public Faction ActerFaction { get; }
    public BattleGroup AllyGroup { get; }
    public BattleGroup EnemyGroup { get; }
    public UnderActersEntryList Unders { get; }
    public Action<string> AppendTopMessage { get; }
    public BattleGroup SelectGroup { get; }
    public BattleGroup OurGroup { get; }
    public SkillZoneTrait RangeWill { get; }
    public TargetingPlan Plan { get; }
    public IBattleRandom Random { get; }

    public TargetingPolicyContext(
        BaseStates acter,
        Faction acterFaction,
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        UnderActersEntryList unders,
        Action<string> appendTopMessage,
        BattleGroup selectGroup,
        BattleGroup ourGroup,
        IBattleRandom random)
    {
        Acter = acter;
        ActerFaction = acterFaction;
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;
        Unders = unders;
        AppendTopMessage = appendTopMessage;
        SelectGroup = selectGroup;
        OurGroup = ourGroup;
        RangeWill = acter != null ? acter.RangeWill : 0;
        Plan = TargetingPlan.FromRangeWill(RangeWill);
        Random = random ?? new SystemBattleRandom();
    }
}

public sealed class TargetingPolicyRegistry
{
    private readonly List<ITargetingPolicy> policies = new();

    public static TargetingPolicyRegistry CreateDefault()
    {
        var registry = new TargetingPolicyRegistry();
        registry.Register(new SingleCandidateTargetingPolicy());
        registry.Register(new AllTargetingPolicy());
        registry.Register(new RandomSingleTargetingPolicy());
        registry.Register(new RandomMultiTargetingPolicy());
        return registry;
    }

    public void Register(ITargetingPolicy policy)
    {
        if (policy == null) return;
        if (policies.Contains(policy)) return;
        policies.Add(policy);
    }

    public bool TrySelectTargets(TargetingPolicyContext context, out List<BaseStates> targets)
    {
        for (var i = 0; i < policies.Count; i++)
        {
            if (!policies[i].TrySelect(context, out targets)) continue;
            if (targets == null || targets.Count == 0) continue;
            return true;
        }
        targets = null;
        return false;
    }
}

internal static class TargetingPolicyUtilities
{
    public static bool HasOnlyMainTrait(SkillZoneTrait rangeWill, SkillZoneTrait trait)
    {
        var main = rangeWill & SkillZoneTraitGroups.MainSelectTraits;
        return main == trait;
    }

    public static List<BaseStates> CreateSelectionPool(TargetingPolicyContext context)
    {
        var targets = new List<BaseStates>();
        if (context?.SelectGroup?.Ours != null)
        {
            targets.AddRange(context.SelectGroup.Ours);
        }
        if (context?.OurGroup?.Ours != null)
        {
            targets.AddRange(context.OurGroup.Ours);
        }
        return targets;
    }
}

/// <summary>
/// 例: 候補が1体しかいない場合は即決するポリシー。
/// 登録だけで挙動を差し替えられることを示す最小の例。
/// </summary>
public sealed class SingleCandidateTargetingPolicy : ITargetingPolicy
{
    public bool TrySelect(TargetingPolicyContext context, out List<BaseStates> targets)
    {
        targets = null;
        if (context?.SelectGroup?.Ours == null) return false;
        var candidates = new List<BaseStates>(context.SelectGroup.Ours);
        if (context.OurGroup?.Ours != null)
        {
            candidates.AddRange(context.OurGroup.Ours);
        }
        if (candidates.Count != 1) return false;
        targets = new List<BaseStates> { candidates[0] };
        return true;
    }
}

/// <summary>
/// 例: 全体対象のスキルは対象プールをそのまま返す。
/// </summary>
public sealed class AllTargetingPolicy : ITargetingPolicy
{
    public bool TrySelect(TargetingPolicyContext context, out List<BaseStates> targets)
    {
        targets = null;
        if (context == null) return false;
        if (!TargetingPolicyUtilities.HasOnlyMainTrait(context.RangeWill, SkillZoneTrait.AllTarget)) return false;
        targets = TargetingPolicyUtilities.CreateSelectionPool(context);
        return targets.Count > 0;
    }
}

/// <summary>
/// 例: ランダム単体のスキルは対象プールから1体を選ぶ。
/// </summary>
public sealed class RandomSingleTargetingPolicy : ITargetingPolicy
{
    public bool TrySelect(TargetingPolicyContext context, out List<BaseStates> targets)
    {
        targets = null;
        if (context == null) return false;
        if (!TargetingPolicyUtilities.HasOnlyMainTrait(context.RangeWill, SkillZoneTrait.RandomSingleTarget)) return false;
        var pool = TargetingPolicyUtilities.CreateSelectionPool(context);
        if (pool.Count == 0) return false;
        var picked = context.Random.GetItem(pool);
        targets = new List<BaseStates> { picked };
        return true;
    }
}

/// <summary>
/// 例: ランダム複数対象のスキルは1〜N体をランダムに選ぶ。
/// </summary>
public sealed class RandomMultiTargetingPolicy : ITargetingPolicy
{
    public bool TrySelect(TargetingPolicyContext context, out List<BaseStates> targets)
    {
        targets = null;
        if (context == null) return false;
        if (!TargetingPolicyUtilities.HasOnlyMainTrait(context.RangeWill, SkillZoneTrait.RandomMultiTarget)) return false;
        var pool = TargetingPolicyUtilities.CreateSelectionPool(context);
        if (pool.Count == 0) return false;
        var count = context.Random.NextInt(1, pool.Count + 1);
        targets = new List<BaseStates>();
        for (var i = 0; i < count; i++)
        {
            var item = context.Random.GetItem(pool);
            targets.Add(item);
            pool.Remove(item);
        }
        return targets.Count > 0;
    }
}
