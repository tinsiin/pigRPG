using System;
using Cysharp.Threading.Tasks;

public sealed class SkillExecutor
{
    private readonly BattleActionContext _context;
    private readonly BattlePresentation _presentation;
    private readonly TurnExecutor _turnExecutor;

    public SkillExecutor(
        BattleActionContext context,
        BattlePresentation presentation,
        TurnExecutor turnExecutor)
    {
        _context = context;
        _presentation = presentation;
        _turnExecutor = turnExecutor;
    }

    public async UniTask<TabState> SkillACT()
    {
        _context.Logger.Log("Skill act execution");
        var acter = _context.Acter;
        var skill = acter.NowUseSkill;

        // 分散計算のためにスキルを設定
        _context.Unders.SetCurrentSkill(skill);

        var singleTarget = _context.Acts.TryPeek(out var entry) ? entry.SingleTarget : null;
        if (singleTarget != null)
        {
            acter.Target = DirectedWill.One;
            _context.Unders.CharaAdd(singleTarget);
        }

        if (skill.HasZoneTrait(SkillZoneTrait.RandomRange))
        {
            DetermineRangeRandomly();
        }

        if (acter.RangeWill == 0)
        {
            acter.RangeWill = SkillZoneTraitNormalizer.NormalizeForInitial(skill.ZoneTrait);
        }

        _context.Targeting.SelectTargets(
            acter,
            _context.ActerFaction,
            _context.AllyGroup,
            _context.EnemyGroup,
            _context.Unders,
            _presentation.AppendTopMessage);

        BeVanguardSkillACT();

        if (_context.Unders.Count < 1)
        {
            _context.Logger.LogError("No targets before AttackChara; unders is empty.");
        }

        await _context.Effects.ResolveSkillEffectsAsync(
            acter,
            _context.ActerFaction,
            _context.Unders,
            _context.AllyGroup,
            _context.EnemyGroup,
            _context.Acts,
            _context.BattleTurnCount,
            _presentation.CreateBattleMessage);

        if (skill.NextConsecutiveATK())
        {
            if (skill.HasConsecutiveType(SkillConsecutiveType.SameTurnConsecutive))
            {
                _turnExecutor.NextTurn(false);
                _context.Acts.Add(acter, _context.ActerFaction);
                acter.FreezeSkill();
            }
            else if (skill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
            {
                _turnExecutor.NextTurn(true);
                acter.FreezeSkill();
                acter.SetFreezeRangeWill(SkillZoneTraitNormalizer.NormalizeForInitial(skill.ZoneTrait));
            }
        }
        else
        {
            acter.Defrost();
            acter.RecovelyCountTmpAdd(skill.SKillDidWaitCount);
            acter.NowUseSkill.ResetStock();
            _turnExecutor.NextTurn(true);
        }

        _context.ResetUnders();
        acter.RangeWill = 0;
        acter.Target = 0;

        return _turnExecutor.ACTPop();
    }

    private void BeVanguardSkillACT()
    {
        var skill = _context.Acter.NowUseSkill;
        if (skill != null && skill.AggressiveOnExecute.isAggressiveCommit)
        {
            _context.BeVanguard(_context.Acter);
        }
    }

    private void DetermineRangeRandomly()
    {
        var acter = _context.Acter;
        var skill = acter.NowUseSkill;

        if (skill.HasZoneTrait(SkillZoneTrait.SelfSkill)) return;

        if (acter.Target != 0 || acter.RangeWill != 0)
        {
            var randomCalculatedPer = 35f;
            if (skill.HasZoneTrait(SkillZoneTrait.ControlByThisSituation))
            {
                randomCalculatedPer = 14f;
            }
            if (!RollPercent(randomCalculatedPer)) return;

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
            switch (_context.Random.NextInt(3))
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
            switch (_context.Random.NextInt(2))
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
            switch (_context.Random.NextInt(2))
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
            switch (_context.Random.NextInt(2))
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

    private bool RollPercent(float percentage)
    {
        if (percentage < 0) percentage = 0;
        return _context.Random.NextFloat(100) < percentage;
    }
}
