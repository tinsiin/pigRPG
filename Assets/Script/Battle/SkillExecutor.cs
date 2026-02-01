using System;
using Cysharp.Threading.Tasks;
using RandomExtensions;
using UnityEngine;
using static CommonCalc;

public sealed class SkillExecutor
{
    private readonly BattleManager _manager;
    private readonly TargetingService _targetingService;
    private readonly EffectResolver _effectResolver;
    private readonly Action<string> _appendUniqueTopMessage;
    private readonly Action<string> _createBattleMessage;

    public SkillExecutor(
        BattleManager manager,
        TargetingService targetingService,
        EffectResolver effectResolver,
        Action<string> appendUniqueTopMessage,
        Action<string> createBattleMessage)
    {
        _manager = manager;
        _targetingService = targetingService;
        _effectResolver = effectResolver;
        _appendUniqueTopMessage = appendUniqueTopMessage;
        _createBattleMessage = createBattleMessage;
    }

    public async UniTask<TabState> SkillACT()
    {
        Debug.Log("Skill act execution");
        var acter = _manager.Acter;
        var skill = acter.NowUseSkill;

        var singleTarget = _manager.Acts.TryPeek(out var entry) ? entry.SingleTarget : null;
        if (singleTarget != null)
        {
            acter.Target = DirectedWill.One;
            _manager.unders.CharaAdd(singleTarget);
        }

        if (skill.HasZoneTrait(SkillZoneTrait.RandomRange))
        {
            DetermineRangeRandomly();
        }

        if (acter.RangeWill == 0)
        {
            acter.RangeWill = SkillZoneTraitNormalizer.NormalizeForInitial(skill.ZoneTrait);
        }

        _targetingService.SelectTargets(
            acter,
            _manager.CurrentActerFaction,
            _manager.AllyGroup,
            _manager.EnemyGroup,
            _manager.unders,
            _appendUniqueTopMessage);

        BeVanguardSkillACT();

        if (_manager.unders.Count < 1)
        {
            Debug.LogError("No targets before AttackChara; unders is empty.");
        }

        await _effectResolver.ResolveSkillEffectsAsync(
            acter,
            _manager.CurrentActerFaction,
            _manager.unders,
            _manager.AllyGroup,
            _manager.EnemyGroup,
            _manager.Acts,
            _manager.BattleTurnCount,
            _createBattleMessage);

        if (skill.NextConsecutiveATK())
        {
            if (skill.HasConsecutiveType(SkillConsecutiveType.SameTurnConsecutive))
            {
                _manager.NextTurn(false);
                _manager.Acts.Add(acter, _manager.CurrentActerFaction);
                acter.FreezeSkill();
            }
            else if (skill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
            {
                _manager.NextTurn(true);
                acter.FreezeSkill();
                acter.SetFreezeRangeWill(SkillZoneTraitNormalizer.NormalizeForInitial(skill.ZoneTrait));
            }
        }
        else
        {
            acter.Defrost();
            acter.RecovelyCountTmpAdd(skill.SKillDidWaitCount);
            acter.NowUseSkill.ResetStock();
            _manager.NextTurn(true);
        }

        _manager.ResetUnders();
        acter.RangeWill = 0;
        acter.Target = 0;

        return _manager.ACTPop();
    }

    private void BeVanguardSkillACT()
    {
        var skill = _manager.Acter.NowUseSkill;
        if (skill != null && skill.IsAggressiveCommit)
        {
            _manager.BeVanguard(_manager.Acter);
        }
    }

    private void DetermineRangeRandomly()
    {
        var acter = _manager.Acter;
        var skill = acter.NowUseSkill;

        if (skill.HasZoneTrait(SkillZoneTrait.SelfSkill)) return;

        if (acter.Target != 0 || acter.RangeWill != 0)
        {
            var randomCalculatedPer = 35f;
            if (skill.HasZoneTrait(SkillZoneTrait.ControlByThisSituation))
            {
                randomCalculatedPer = 14f;
            }
            if (!rollper(randomCalculatedPer)) return;

            acter.Target = 0;
            acter.RangeWill = 0;
        }
        acter.SkillCalculatedRandomRange = true;

        acter.RangeWill = skill.ZoneTrait;
        acter.RangeWill = acter.RangeWill.Remove(SkillZoneTraitGroups.RandomBranchTraits);
        acter.RangeWill = acter.RangeWill.Remove(SkillZoneTraitGroups.ActualRangeTraits);
        acter.RangeWill = acter.RangeWill.Remove(SkillZoneTraitGroups.MainSelectTraits);

        if (skill.HasZoneTrait(SkillZoneTrait.RandomTargetALLSituation))
        {
            switch (RandomEx.Shared.NextInt(3))
            {
                case 0:
                    acter.RangeWill |= SkillZoneTrait.AllTarget;
                    break;
                case 1:
                    if (acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
                    {
                        acter.RangeWill |= SkillZoneTrait.RandomMultiTarget;
                    }
                    else
                    {
                        acter.RangeWill |= SkillZoneTrait.RandomSelectMultiTarget;
                    }
                    break;
                case 2:
                    acter.RangeWill |= SkillZoneTrait.RandomSingleTarget;
                    break;
            }
        }
        if (skill.HasZoneTrait(SkillZoneTrait.RandomTargetALLorMulti))
        {
            switch (RandomEx.Shared.NextInt(2))
            {
                case 0:
                    acter.RangeWill |= SkillZoneTrait.AllTarget;
                    break;
                case 1:
                    if (acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
                    {
                        acter.RangeWill |= SkillZoneTrait.RandomMultiTarget;
                    }
                    else
                    {
                        acter.RangeWill |= SkillZoneTrait.RandomSelectMultiTarget;
                    }
                    break;
            }
        }
        if (skill.HasZoneTrait(SkillZoneTrait.RandomTargetALLorSingle))
        {
            switch (RandomEx.Shared.NextInt(2))
            {
                case 0:
                    acter.RangeWill |= SkillZoneTrait.AllTarget;
                    break;
                case 1:
                    acter.RangeWill |= SkillZoneTrait.RandomSingleTarget;
                    break;
            }
        }
        if (skill.HasZoneTrait(SkillZoneTrait.RandomTargetMultiOrSingle))
        {
            switch (RandomEx.Shared.NextInt(2))
            {
                case 0:
                    if (acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
                    {
                        acter.RangeWill |= SkillZoneTrait.RandomMultiTarget;
                    }
                    else
                    {
                        acter.RangeWill |= SkillZoneTrait.RandomSelectMultiTarget;
                    }
                    break;
                case 1:
                    acter.RangeWill |= SkillZoneTrait.RandomSingleTarget;
                    break;
            }
        }
    }
}
