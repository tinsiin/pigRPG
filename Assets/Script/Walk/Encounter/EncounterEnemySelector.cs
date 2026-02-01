using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using UnityEngine;
using static CommonCalc;

public static class EncounterEnemySelector
{
    /// <summary>
    /// 敵グループを選択する
    /// </summary>
    /// <param name="enemies">候補となる敵リスト</param>
    /// <param name="globalSteps">現在のグローバルステップ数</param>
    /// <param name="number">指定人数（-1で自動）</param>
    /// <param name="matchCalc">相性計算インターフェース（null時はEnemyCollectManager.Instance）</param>
    public static BattleGroup SelectGroup(
        IReadOnlyList<NormalEnemy> enemies,
        int globalSteps,
        int number = -1,
        IEnemyMatchCalculator matchCalc = null)
    {
        if (enemies == null || enemies.Count == 0) return null;

        // Phase 3c: DI注入を優先、フォールバックでEnemyCollectManager.Instance
        var calc = matchCalc ?? EnemyCollectManager.Instance;

        var resultList = new List<NormalEnemy>();
        var validEnemies = FilterEligibleEnemies(enemies, globalSteps);
        ApplyReencountCallbacks(validEnemies, globalSteps);
        if (!validEnemies.Any()) return null;

        var referenceOne = SelectLeader(validEnemies);
        resultList.Add(referenceOne);

        var manualCount = number >= 1;
        var targetCount = manualCount ? Mathf.Clamp(number, 1, 3) : -1;

        if (manualCount && targetCount == 1)
        {
            return CreateBattleGroup(resultList, GetPartyImpression(resultList, calc), calc);
        }

        if (!manualCount &&
            (calc.LonelyMatchUp(resultList[0].MyImpression) || validEnemies.Count <= 0))
        {
            return CreateBattleGroup(resultList, GetPartyImpression(resultList, calc), calc);
        }

        while (true)
        {
            if (TryResolveManualEnd(manualCount, targetCount, resultList, validEnemies, calc, out var manualImpression))
            {
                return CreateBattleGroup(resultList, manualImpression, calc);
            }

            TryAddCompatibleTarget(resultList, validEnemies, calc);

            if (TryResolveAutoEnd(manualCount, resultList, validEnemies, calc, out var autoImpression))
            {
                return CreateBattleGroup(resultList, autoImpression, calc);
            }
        }
    }

    private static List<NormalEnemy> FilterEligibleEnemies(IReadOnlyList<NormalEnemy> enemies, int globalSteps)
    {
        var validEnemies = new List<NormalEnemy>();
        for (var i = 0; i < enemies.Count; i++)
        {
            var ene = enemies[i];
            if (ene == null) continue;
            if (!ene.Death())
            {
                validEnemies.Add(ene);
                continue;
            }
            if (ene.broken) continue;

            if (ene.Reborn && EnemyRebornManager.Instance.CanReborn(ene, globalSteps))
            {
                validEnemies.Add(ene);
            }
        }
        return validEnemies;
    }

    private static void ApplyReencountCallbacks(List<NormalEnemy> validEnemies, int globalSteps)
    {
        for (var i = 0; i < validEnemies.Count; i++)
        {
            var ene = validEnemies[i];
            EnsureInitialized(ene);
            ene.ReEncountCallback(globalSteps);
        }
    }

    private static NormalEnemy SelectLeader(List<NormalEnemy> validEnemies)
    {
        var rndIndex = RandomEx.Shared.NextInt(0, validEnemies.Count);
        var referenceOne = validEnemies[rndIndex];
        referenceOne.InitializeMyImpression();
        validEnemies.RemoveAt(rndIndex);
        return referenceOne;
    }

    private static bool TryResolveManualEnd(
        bool manualCount,
        int targetCount,
        List<NormalEnemy> resultList,
        List<NormalEnemy> validEnemies,
        IEnemyMatchCalculator calc,
        out PartyProperty impression)
    {
        impression = default;
        if (!manualCount) return false;
        if (resultList.Count < targetCount && validEnemies.Count >= 1) return false;
        impression = GetPartyImpression(resultList, calc);
        return true;
    }

    private static void TryAddCompatibleTarget(
        List<NormalEnemy> resultList,
        List<NormalEnemy> validEnemies,
        IEnemyMatchCalculator calc)
    {
        if (validEnemies.Count < 1) return;

        var targetIndex = RandomEx.Shared.NextInt(0, validEnemies.Count);
        var target = validEnemies[targetIndex];
        var okCount = 0;
        var sympathy = HasSympathy(resultList, target);

        for (var i = 0; i < resultList.Count; i++)
        {
            var ene = resultList[i];
            if (calc.TypeMatchUp(ene.MyType, target.MyType))
            {
                if (calc.ImpressionMatchUp(ene.MyImpression, target.MyImpression, sympathy))
                {
                    okCount++;
                }
            }
        }

        if (okCount == resultList.Count)
        {
            target.InitializeMyImpression();
            resultList.Add(target);
            validEnemies.Remove(target);
        }
    }

    private static bool TryResolveAutoEnd(
        bool manualCount,
        List<NormalEnemy> resultList,
        List<NormalEnemy> validEnemies,
        IEnemyMatchCalculator calc,
        out PartyProperty impression)
    {
        impression = default;
        if (manualCount) return false;

        if (resultList.Count == 1)
        {
            if (RandomEx.Shared.NextInt(100) < 88)
            {
                if (calc.LonelyMatchUp(resultList[0].MyImpression))
                {
                    impression = calc.EnemyLonelyPartyImpression[resultList[0].MyImpression];
                    return true;
                }
            }
        }

        if (resultList.Count == 2)
        {
            if (RandomEx.Shared.NextInt(100) < 65)
            {
                impression = calc.calculatePartyProperty(resultList);
                return true;
            }
        }

        if (resultList.Count >= 3)
        {
            impression = calc.calculatePartyProperty(resultList);
            return true;
        }

        if (validEnemies.Count < 1)
        {
            impression = calc.calculatePartyProperty(resultList);
            return true;
        }

        return false;
    }

    private static PartyProperty GetPartyImpression(List<NormalEnemy> resultList, IEnemyMatchCalculator calc)
    {
        return resultList.Count == 1
            ? calc.EnemyLonelyPartyImpression[resultList[0].MyImpression]
            : calc.calculatePartyProperty(resultList);
    }

    private static bool HasSympathy(List<NormalEnemy> resultList, NormalEnemy target)
    {
        var sympathy = false;
        for (var i = 0; i < resultList.Count; i++)
        {
            var ene = resultList[i];
            if (ene.HP <= ene.MaxHP / 2)
            {
                sympathy = true;
                break;
            }
        }
        if (!sympathy && target.HP <= target.MaxHP / 2)
        {
            sympathy = true;
        }
        return sympathy;
    }

    private static void EnsureInitialized(NormalEnemy enemy)
    {
        if (enemy == null) return;
        if (enemy.NowUseWeapon == null)
        {
            enemy.OnInitializeSkillsAndChara();
            return;
        }
        var skills = enemy.SkillList;
        if (skills == null) return;
        for (var i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            if (skill == null) continue;
            if (skill.Doer == null)
            {
                enemy.OnInitializeSkillsAndChara();
                return;
            }
        }
    }

    private static BattleGroup CreateBattleGroup(List<NormalEnemy> resultList, PartyProperty ourImpression, IEnemyMatchCalculator calc)
    {
        var compatibilityData = BuildCompatibilityData(resultList, calc);
        return new BattleGroup(resultList.Cast<BaseStates>().ToList(), ourImpression, allyOrEnemy.Enemyiy, compatibilityData);
    }

    private static Dictionary<(BaseStates, BaseStates), int> BuildCompatibilityData(List<NormalEnemy> resultList, IEnemyMatchCalculator calc)
    {
        var compatibilityData = new Dictionary<(BaseStates, BaseStates), int>();
        if (resultList == null || resultList.Count < 2) return compatibilityData;

        for (var i = 0; i < resultList.Count; i++)
        {
            for (var j = i + 1; j < resultList.Count; j++)
            {
                var first = resultList[i];
                var second = resultList[j];
                if (first == null || second == null) continue;
                var compatibilityValue = calc.GetImpressionMatchPercent(first.MyImpression, second.MyImpression);
                compatibilityData[(first, second)] = compatibilityValue;
                compatibilityValue = calc.GetImpressionMatchPercent(second.MyImpression, first.MyImpression);
                compatibilityData[(second, first)] = compatibilityValue;
            }
        }

        return compatibilityData;
    }
}
