using System;
using System.Collections.Generic;
using System.Linq;
using static CommonCalc;

public sealed class TargetingService
{
    private readonly bool debugSelectLog;
    private readonly TargetingPolicyRegistry policyRegistry;
    private readonly IBattleRandom _random;
    private readonly IBattleLogger _logger;

    public TargetingService(
        bool debugSelectLog = false,
        TargetingPolicyRegistry policyRegistry = null,
        IBattleRandom random = null,
        IBattleLogger logger = null)
    {
        this.debugSelectLog = debugSelectLog;
        this.policyRegistry = policyRegistry;
        _random = random ?? new SystemBattleRandom();
        _logger = logger ?? new NoOpBattleLogger();
    }

    public void SelectTargets(
        BaseStates acter,
        Faction acterFaction,
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        UnderActersEntryList unders,
        Action<string> appendTopMessage)
    {
        var context = new TargetingContext(
            acter,
            acterFaction,
            allyGroup,
            enemyGroup,
            unders,
            appendTopMessage);

        if (acter.HasRangeWill(SkillZoneTrait.SelfSkill))
        {
            unders.CharaAdd(acter);
            return;
        }

        AssignSelectionGroups(context);
        RemoveDeathTargetsIfNeeded(context);

        if (acter.Target == DirectedWill.One)
        {
            return;
        }

        if (policyRegistry != null)
        {
            var policyContext = new TargetingPolicyContext(
                acter,
                acterFaction,
                allyGroup,
                enemyGroup,
                unders,
                appendTopMessage,
                context.SelectGroup,
                context.OurGroup,
                _random);
            if (policyRegistry.TrySelectTargets(policyContext, out var policyTargets))
            {
                // 従来の動作との一貫性のため、ポリシー選択後もシャッフルを適用
                var shuffled = policyTargets.ToList();
                _random.Shuffle(shuffled);
                unders.SetList(shuffled);
                return;
            }
        }

        var targets = SelectTargetsInternal(context);
        if (unders.Count < 1)
        {
            _random.Shuffle(targets);
            unders.SetList(targets);
        }
    }

    private sealed class TargetingContext
    {
        public BaseStates Acter { get; }
        public Faction ActerFaction { get; }
        public BattleGroup AllyGroup { get; }
        public BattleGroup EnemyGroup { get; }
        public UnderActersEntryList Unders { get; }
        public Action<string> AppendTopMessage { get; }
        public BattleGroup SelectGroup { get; set; }
        public BattleGroup OurGroup { get; set; }

        public TargetingContext(
            BaseStates acter,
            Faction acterFaction,
            BattleGroup allyGroup,
            BattleGroup enemyGroup,
            UnderActersEntryList unders,
            Action<string> appendTopMessage)
        {
            Acter = acter;
            ActerFaction = acterFaction;
            AllyGroup = allyGroup;
            EnemyGroup = enemyGroup;
            Unders = unders;
            AppendTopMessage = appendTopMessage;
        }

        public List<BaseStates> CreateSelectionPoolCopy()
        {
            var selects = new List<BaseStates>(SelectGroup.Ours);
            if (OurGroup != null)
            {
                selects.AddRange(OurGroup.Ours);
            }
            return selects;
        }
    }

    private static void AssignSelectionGroups(TargetingContext context)
    {
        var acter = context.Acter;
        var canSelectMyself = acter.HasRangeWill(SkillZoneTrait.CanSelectMyself);

        if (context.ActerFaction == Faction.Ally)
        {
            if (acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
            {
                context.SelectGroup = new BattleGroup(context.AllyGroup.Ours, context.AllyGroup.OurImpression, context.AllyGroup.which);
                if (!canSelectMyself)
                {
                    context.SelectGroup.Ours.Remove(acter);
                }
            }
            else
            {
                context.SelectGroup = new BattleGroup(context.EnemyGroup.Ours, context.EnemyGroup.OurImpression, context.EnemyGroup.which);

                if (acter.HasRangeWill(SkillZoneTrait.CanSelectAlly))
                {
                    context.OurGroup = new BattleGroup(context.AllyGroup.Ours, context.AllyGroup.OurImpression, context.AllyGroup.which);
                    if (!canSelectMyself)
                    {
                        context.OurGroup.Ours.Remove(acter);
                    }
                }
                else if (canSelectMyself)
                {
                    context.OurGroup = new BattleGroup(new List<BaseStates> { acter }, context.AllyGroup.OurImpression, context.AllyGroup.which);
                }
            }
        }
        else
        {
            if (acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
            {
                context.SelectGroup = new BattleGroup(context.EnemyGroup.Ours, context.EnemyGroup.OurImpression, context.EnemyGroup.which);
                if (!canSelectMyself)
                {
                    context.SelectGroup.Ours.Remove(acter);
                }
            }
            else
            {
                context.SelectGroup = new BattleGroup(context.AllyGroup.Ours, context.AllyGroup.OurImpression, context.AllyGroup.which);
                if (acter.HasRangeWill(SkillZoneTrait.CanSelectAlly))
                {
                    context.OurGroup = new BattleGroup(context.EnemyGroup.Ours, context.EnemyGroup.OurImpression, context.EnemyGroup.which);
                    if (!canSelectMyself)
                    {
                        context.OurGroup.Ours.Remove(acter);
                    }
                }
                else if (canSelectMyself)
                {
                    context.OurGroup = new BattleGroup(new List<BaseStates> { acter }, context.EnemyGroup.OurImpression, context.EnemyGroup.which);
                }
            }
        }
    }

    private static void RemoveDeathTargetsIfNeeded(TargetingContext context)
    {
        if (context.Acter.HasRangeWill(SkillZoneTrait.CanSelectDeath))
        {
            return;
        }

        context.SelectGroup.SetCharactersList(RemoveDeathCharacters(context.SelectGroup.Ours));
        if (context.OurGroup != null)
        {
            context.OurGroup.SetCharactersList(RemoveDeathCharacters(context.OurGroup.Ours));
        }
    }

    private List<BaseStates> SelectTargetsInternal(TargetingContext context)
    {
        var acter = context.Acter;
        var selectGroup = context.SelectGroup;
        var targets = new List<BaseStates>();

        if (selectGroup.Ours.Count < 2)
        {
            _logger.Log("敵に一人しかいません");
            targets.Add(selectGroup.Ours[0]);
            return targets;
        }

        if (acter.HasRangeWill(SkillZoneTrait.CanSelectSingleTarget))
        {
            SelectSingleTarget(context, targets);
            return targets;
        }
        if (acter.HasRangeWill(SkillZoneTrait.RandomSingleTarget))
        {
            SelectRandomSingleTarget(context, targets);
            return targets;
        }
        if (acter.HasRangeWill(SkillZoneTrait.ControlByThisSituation))
        {
            SelectControlBySituation(context, targets);
            return targets;
        }
        if (acter.HasRangeWill(SkillZoneTrait.CanSelectMultiTarget))
        {
            SelectMultiTarget(context, targets);
            return targets;
        }
        if (acter.HasRangeWill(SkillZoneTrait.RandomSelectMultiTarget))
        {
            SelectRandomSelectMultiTarget(context, targets);
            return targets;
        }
        if (acter.HasRangeWill(SkillZoneTrait.RandomMultiTarget))
        {
            SelectRandomMultiTarget(context, targets);
            return targets;
        }
        if (acter.HasRangeWill(SkillZoneTrait.AllTarget))
        {
            SelectAllTarget(context, targets);
        }

        return targets;
    }

    private void SelectSingleTarget(TargetingContext context, List<BaseStates> targets)
    {
        var acter = context.Acter;
        var selectGroup = context.SelectGroup;

        if (selectGroup.InstantVanguard == null)
        {
            targets.AddRange(TargetSelectionHelper.SelectByPassiveAndRandom(selectGroup.Ours, 2, _random, _logger, 23, debugSelectLog));
            return;
        }

        if (acter.Target == DirectedWill.InstantVanguard)
        {
            targets.Add(selectGroup.InstantVanguard);
            _logger.Log(acter.CharacterName + "は前のめりしてる奴を狙った");
        }
        else if (acter.Target == DirectedWill.BacklineOrAny)
        {
            if (ComparePressureAndRedirect(acter, selectGroup.InstantVanguard))
            {
                context.AppendTopMessage?.Invoke("テラーズヒット");
                targets.Add(selectGroup.InstantVanguard);
                _logger.Log(acter.CharacterName + "は後衛を狙ったが前のめりしてる奴に阻まれた");
            }
            else
            {
                List<BaseStates> backLines = new List<BaseStates>(selectGroup.Ours.Where(member => member != selectGroup.InstantVanguard));

                targets.AddRange(TargetSelectionHelper.SelectByPassiveAndRandom(backLines, 1, _random, _logger, debugSelectLog: debugSelectLog));
                acter.SetSpecialModifier("少し遠いよ", StatModifier.Eye, 0.7f);
                _logger.Log(acter.CharacterName + "は後衛を狙った");
            }
        }
        else
        {
            _logger.LogError("CanSelectSingleTargetの処理では前のめりか後衛以外の意志を受け付けていません。");
        }
    }

    private void SelectRandomSingleTarget(TargetingContext context, List<BaseStates> targets)
    {
        var selects = context.CreateSelectionPoolCopy();
        targets.Add(_random.GetItem(selects));
    }

    private void SelectControlBySituation(TargetingContext context, List<BaseStates> targets)
    {
        var acter = context.Acter;
        var selectGroup = context.SelectGroup;

        _logger.Log("ControlByThisSituationのスキル分岐(SelectTargetFromWill)");
        if (selectGroup.InstantVanguard == null)
        {
            _logger.Log("ControlByThisSituationのスキル分岐(SelectTargetFromWill)で前のめりがいない");

            bool isAccident = false;

            if (acter.HasRangeWill(SkillZoneTrait.RandomSingleTarget))
            {
                _logger.Log("ランダムシングル事故");
                var selects = context.CreateSelectionPoolCopy();

                targets.AddRange(TargetSelectionHelper.SelectByPassiveAndRandom(selects, 1, _random, _logger, debugSelectLog: debugSelectLog));
                isAccident = true;
            }

            if (acter.HasRangeWill(SkillZoneTrait.AllTarget))
            {
                _logger.Log("全範囲事故");
                var selects = context.CreateSelectionPoolCopy();

                targets.AddRange(selects);
                isAccident = true;
            }
            if (acter.HasRangeWill(SkillZoneTrait.RandomMultiTarget))
            {
                _logger.Log("ランダム範囲事故");
                var selects = context.CreateSelectionPoolCopy();

                int want = _random.NextInt(1, selects.Count + 1);
                _logger.Log($"ランダム範囲事故対象者数(パッシブ判定前) : {want}");
                var charas = TargetSelectionHelper.SelectByPassiveAndRandom(selects, want, _random, _logger, debugSelectLog: debugSelectLog);
                targets.AddRange(charas);
                isAccident = true;
                _logger.Log($"ランダム範囲事故対象者数(パッシブ判定後) : {charas.Count}");
            }

            if (!isAccident)
            {
                _logger.LogAssertion("ControlByThisSituationによるNon前のめり事故が起きなかった\n事故用に範囲意志をスキルに設定する必要があります。");
            }
        }
        else
        {
            targets.Add(selectGroup.InstantVanguard);
        }
    }

    private void SelectMultiTarget(TargetingContext context, List<BaseStates> targets)
    {
        var acter = context.Acter;
        var selectGroup = context.SelectGroup;

        if (selectGroup.InstantVanguard == null)
        {
            targets.AddRange(TargetSelectionHelper.SelectByPassiveAndRandom(selectGroup.Ours, 2, _random, _logger, debugSelectLog: debugSelectLog));
            return;
        }

        if (acter.Target == DirectedWill.InstantVanguard)
        {
            targets.Add(selectGroup.InstantVanguard);
            _logger.Log(acter.CharacterName + "は前のめりしてる奴を狙った");
        }
        else if (acter.Target == DirectedWill.BacklineOrAny)
        {
            if (ComparePressureAndRedirect(acter, selectGroup.InstantVanguard))
            {
                context.AppendTopMessage?.Invoke("テラーズヒット");
                targets.Add(selectGroup.InstantVanguard);
                _logger.Log(acter.CharacterName + "は後衛を狙ったが前のめりしてる奴に阻まれた");
            }
            else
            {
                List<BaseStates> backLines = new List<BaseStates>(selectGroup.Ours.Where(member => member != selectGroup.InstantVanguard));

                targets.AddRange(backLines);
                acter.SetSpecialModifier("ほんの少し狙いにくい", StatModifier.Eye, 0.9f);
                _logger.Log(acter.CharacterName + "は後衛を狙った");
            }
        }
        else
        {
            _logger.LogError("CanSelectMultiTargetの処理では前のめりか後衛以外の意志を受け付けていません。");
        }
    }

    private void SelectRandomSelectMultiTarget(TargetingContext context, List<BaseStates> targets)
    {
        var selectGroup = context.SelectGroup;
        var selectVanguard = _random.NextBool();

        if (selectGroup.InstantVanguard == null)
        {
            var counter = 0;
            _random.Shuffle(selectGroup.Ours);
            foreach (var one in selectGroup.Ours)
            {
                targets.Add(one);
                counter++;
                if (counter >= 2) break;
            }
        }
        else
        {
            if (selectVanguard)
            {
                targets.Add(selectGroup.InstantVanguard);
                _logger.Log(context.Acter.CharacterName + "の技は前のめりしてる奴に向いた");
            }
            else
            {
                List<BaseStates> backLines = new List<BaseStates>(selectGroup.Ours.Where(member => member != selectGroup.InstantVanguard));

                targets.AddRange(backLines);
                _logger.Log(context.Acter.CharacterName + "の技は後衛に向いた");
            }
        }
    }

    private void SelectRandomMultiTarget(TargetingContext context, List<BaseStates> targets)
    {
        List<BaseStates> selects = context.SelectGroup.Ours;
        if (context.OurGroup != null)
            selects.AddRange(context.OurGroup.Ours);

        var count = selects.Count;
        count = _random.NextInt(1, count + 1);

        for (int i = 0; i < count; i++)
        {
            var item = _random.GetItem(selects);
            targets.Add(item);
            selects.Remove(item);
        }
    }

    private static void SelectAllTarget(TargetingContext context, List<BaseStates> targets)
    {
        var selects = context.CreateSelectionPoolCopy();
        targets.AddRange(selects);
    }

    private bool ComparePressureAndRedirect(BaseStates attacker, BaseStates vanguard)
    {
        var vanguardPressure = vanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.Glory);
        var attackerResilience = attacker.TenDayValues(false).GetValueOrZero(TenDayAbility.JoeTeeth)
                                 + attacker.TenDayValues(false).GetValueOrZero(TenDayAbility.WaterThunderNerve) * 0.5f;

        return vanguardPressure > _random.NextFloat(vanguardPressure + attackerResilience);
    }
}
