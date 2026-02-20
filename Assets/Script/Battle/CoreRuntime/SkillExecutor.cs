using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Effects.Integration;
using UnityEngine;

public sealed class SkillExecutor
{
    private readonly BattleActionContext _context;
    private readonly BattlePresentation _presentation;
    private readonly TurnExecutor _turnExecutor;

    // キャッシュ（毎回Find/FindFirstObjectByTypeを呼ばないように）
    private RectTransform _cachedViewportRT;
    private PlayersUIRefs _cachedUIRefs;

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

        TryPlayWeaponSlash(skill);
        PlaySkillVisualEffects(skill);

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

    private void PlaySkillVisualEffects(BaseSkill skill)
    {
        if (!string.IsNullOrEmpty(skill.CasterEffectName))
        {
            var casterIcon = _context.Acter.BattleIcon;
            if (casterIcon != null)
                EffectManager.Play(skill.CasterEffectName, casterIcon);
        }

        if (!string.IsNullOrEmpty(skill.TargetEffectName))
        {
            for (int i = 0; i < _context.Unders.Count; i++)
            {
                var targetIcon = _context.Unders.GetAtCharacter(i).BattleIcon;
                if (targetIcon != null)
                    EffectManager.Play(skill.TargetEffectName, targetIcon);
            }
        }

        if (!string.IsNullOrEmpty(skill.FieldEffectName))
        {
            EffectManager.PlayField(skill.FieldEffectName);
        }
    }

    private void TryPlayWeaponSlash(BaseSkill skill)
    {
        if (_context.ActerFaction != Faction.Ally) return;

        var weapon = _context.Acter.NowUseWeapon;
        if (weapon == null || !weapon.IsBlade) return;

        // 武器スキルまたは掛け合わせスキルか判定
        bool isWeaponSkill = weapon.WeaponSkill != null
            && ReferenceEquals(skill, weapon.WeaponSkill);
        bool isCombinationSkill = !isWeaponSkill
            && weapon.CombinationEntries != null
            && weapon.CombinationEntries.Exists(e =>
                e?.combinedSkill != null && ReferenceEquals(skill, e.combinedSkill));

        if (!isWeaponSkill && !isCombinationSkill) return;

        // ViewportArea を取得（FieldEffectLayer の親）— キャッシュ活用
        if (_cachedViewportRT == null)
        {
            var fieldLayerGo = GameObject.Find("FieldEffectLayer");
            if (fieldLayerGo == null) return;
            _cachedViewportRT = fieldLayerGo.transform.parent as RectTransform;
        }
        if (_cachedViewportRT == null) return;

        // ターゲット位置を viewport ローカル座標で収集
        var targetPositions = new List<Vector2>();
        for (int i = 0; i < _context.Unders.Count; i++)
        {
            var icon = _context.Unders.GetAtCharacter(i).BattleIcon;
            if (icon == null) continue;
            var iconRT = icon.GetComponent<RectTransform>();
            if (iconRT == null) continue;
            Vector2 localPos = _cachedViewportRT.InverseTransformPoint(iconRT.position);
            targetPositions.Add(localPos);
        }

        if (targetPositions.Count == 0) return;

        float slashScale = 1f;
        float slashSpeed = 1f;
        if (_cachedUIRefs == null)
            _cachedUIRefs = UnityEngine.Object.FindFirstObjectByType<PlayersUIRefs>();
        if (_cachedUIRefs != null)
        {
            slashScale = _cachedUIRefs.WeaponSlashScale;
            slashSpeed = _cachedUIRefs.WeaponSlashSpeed;
        }

        WeaponSlashAnimator.PlayAsync(
            _cachedViewportRT, targetPositions, weapon.SlashColor, slashScale, slashSpeed
        ).Forget();
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
