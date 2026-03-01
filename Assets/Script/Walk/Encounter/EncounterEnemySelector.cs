using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using UnityEngine;
using static CommonCalc;

/// <summary>
/// 敵グループ選択クラス
/// </summary>
public class EncounterEnemySelector
{
    private readonly IEnemyRebornManager _rebornManager;
    private readonly IEnemyMatchCalculator _matchCalc;
    private readonly FriendshipComboRegistry _comboRegistry;

    /// <summary>結成中にsympathyが発動したかの追跡フラグ</summary>
    private bool _anyMemberSympathy;

    /// <summary>
    /// テスト用コンストラクタ（依存性注入）
    /// </summary>
    public EncounterEnemySelector(
        IEnemyRebornManager rebornManager,
        IEnemyMatchCalculator matchCalc,
        FriendshipComboRegistry comboRegistry = null)
    {
        _rebornManager = rebornManager ?? EnemyRebornManager.Instance;
        _matchCalc = matchCalc ?? EnemyCollectManager.Instance;
        _comboRegistry = comboRegistry;
    }

    /// <summary>
    /// デフォルトコンストラクタ（フォールバック）
    /// </summary>
    public EncounterEnemySelector() : this(EnemyRebornManager.Instance, EnemyCollectManager.Instance, null)
    {
    }

    #region 互換性維持用 Static メソッド

    /// <summary>
    /// 敵グループを選択する（互換性維持用 static ラッパー）
    /// </summary>
    public static BattleGroup SelectGroup(
        IReadOnlyList<NormalEnemy> enemies,
        int globalSteps,
        int number = -1,
        IEnemyMatchCalculator matchCalc = null,
        IEnemyRebornManager rebornManager = null,
        FriendshipComboRegistry comboRegistry = null)
    {
        var selector = new EncounterEnemySelector(rebornManager, matchCalc, comboRegistry);
        return selector.Select(enemies, globalSteps, number);
    }

    #endregion

    /// <summary>
    /// 敵グループを選択する
    /// </summary>
    /// <param name="enemies">候補となる敵リスト</param>
    /// <param name="globalSteps">現在のグローバルステップ数</param>
    /// <param name="number">指定人数（-1で自動）</param>
    public BattleGroup Select(
        IReadOnlyList<NormalEnemy> enemies,
        int globalSteps,
        int number = -1)
    {
        if (enemies == null || enemies.Count == 0) return null;

        _anyMemberSympathy = false;

        var validEnemies = FilterEligibleEnemies(enemies, globalSteps);
        ApplyReencountCallbacks(validEnemies, globalSteps);
        if (!validEnemies.Any()) return null;

        var referenceOne = SelectLeader(validEnemies);

        // === コンビ分岐: リーダーがコンビメンバーなら再結成 ===
        if (_comboRegistry != null && _comboRegistry.AllCombos.Count > 0)
        {
            var combo = _comboRegistry.FindComboByMemberGuid(referenceOne.EnemyGuid);
            if (combo != null)
            {
                return AssembleComboGroup(referenceOne, combo, validEnemies);
            }
            // フリーリーダー: コンビメンバーを候補から除外して通常フローへ
            ExcludeComboMembers(validEnemies);
        }

        // 通常フロー
        var resultList = new List<NormalEnemy> { referenceOne };

        var manualCount = number >= 1;
        var targetCount = manualCount ? Mathf.Clamp(number, 1, 3) : -1;

        if (manualCount && targetCount == 1)
        {
            return CreateBattleGroup(resultList, GetPartyImpression(resultList));
        }

        if (!manualCount &&
            (_matchCalc.LonelyMatchUp(resultList[0].MyImpression) || validEnemies.Count <= 0))
        {
            return CreateBattleGroup(resultList, GetPartyImpression(resultList));
        }

        while (true)
        {
            if (TryResolveManualEnd(manualCount, targetCount, resultList, validEnemies, out var manualImpression))
            {
                return CreateBattleGroup(resultList, manualImpression);
            }

            TryAddCompatibleTarget(resultList, validEnemies);

            if (TryResolveAutoEnd(manualCount, resultList, validEnemies, out var autoImpression))
            {
                return CreateBattleGroup(resultList, autoImpression);
            }
        }
    }

    /// <summary>
    /// 有効な敵をフィルタリングする
    /// </summary>
    internal List<NormalEnemy> FilterEligibleEnemies(IReadOnlyList<NormalEnemy> enemies, int globalSteps)
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

            if (ene.Reborn && _rebornManager.CanReborn(ene, globalSteps))
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

    internal bool TryResolveManualEnd(
        bool manualCount,
        int targetCount,
        List<NormalEnemy> resultList,
        List<NormalEnemy> validEnemies,
        out PartyProperty impression)
    {
        impression = default;
        if (!manualCount) return false;
        if (resultList.Count < targetCount && validEnemies.Count >= 1) return false;
        impression = GetPartyImpression(resultList);
        return true;
    }

    internal void TryAddCompatibleTarget(
        List<NormalEnemy> resultList,
        List<NormalEnemy> validEnemies)
    {
        if (validEnemies.Count < 1) return;

        var targetIndex = RandomEx.Shared.NextInt(0, validEnemies.Count);
        var target = validEnemies[targetIndex];
        var okCount = 0;
        var sympathy = HasSympathy(resultList, target);
        if (sympathy) _anyMemberSympathy = true;

        for (var i = 0; i < resultList.Count; i++)
        {
            var ene = resultList[i];
            if (_matchCalc.TypeMatchUp(ene.MyType, target.MyType))
            {
                if (_matchCalc.ImpressionMatchUp(ene.MyImpression, target.MyImpression, sympathy))
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

    internal bool TryResolveAutoEnd(
        bool manualCount,
        List<NormalEnemy> resultList,
        List<NormalEnemy> validEnemies,
        out PartyProperty impression)
    {
        impression = default;
        if (manualCount) return false;

        if (resultList.Count == 1)
        {
            if (RandomEx.Shared.NextInt(100) < 88)
            {
                if (_matchCalc.LonelyMatchUp(resultList[0].MyImpression))
                {
                    impression = _matchCalc.EnemyLonelyPartyImpression[resultList[0].MyImpression];
                    return true;
                }
            }
        }

        if (resultList.Count == 2)
        {
            if (RandomEx.Shared.NextInt(100) < 65)
            {
                impression = _matchCalc.calculatePartyProperty(resultList);
                return true;
            }
        }

        if (resultList.Count >= 3)
        {
            impression = _matchCalc.calculatePartyProperty(resultList);
            return true;
        }

        if (validEnemies.Count < 1)
        {
            impression = _matchCalc.calculatePartyProperty(resultList);
            return true;
        }

        return false;
    }

    internal PartyProperty GetPartyImpression(List<NormalEnemy> resultList)
    {
        return resultList.Count == 1
            ? _matchCalc.EnemyLonelyPartyImpression[resultList[0].MyImpression]
            : _matchCalc.calculatePartyProperty(resultList);
    }

    internal static bool HasSympathy(List<NormalEnemy> resultList, NormalEnemy target)
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
            if (!skill.IsInitialized)
            {
                enemy.OnInitializeSkillsAndChara();
                return;
            }
        }
    }

    /// <summary>
    /// コンビ再結成: リーダーの相方を有効敵プールから集めてグループ化。
    /// 相性チェック不要（既に登録済みコンビのため）。
    /// </summary>
    private BattleGroup AssembleComboGroup(
        NormalEnemy leader,
        FriendshipComboSaveData combo,
        List<NormalEnemy> validEnemies)
    {
        var resultList = new List<NormalEnemy> { leader };

        for (var i = 0; i < combo.MemberGuids.Count; i++)
        {
            var guid = combo.MemberGuids[i];
            if (guid == leader.EnemyGuid) continue;

            // broken判定
            var state = _comboRegistry.GetEnemyState(guid);
            if (state != null && state.IsBroken) continue;

            var partner = FindByGuid(validEnemies, guid);
            if (partner != null)
            {
                partner.InitializeMyImpression();
                resultList.Add(partner);
            }
        }

        return CreateBattleGroup(resultList, GetPartyImpression(resultList));
    }

    /// <summary>
    /// 候補リストからコンビ登録済みメンバーを除外する。
    /// フリーリーダーが選ばれた場合に使用。
    /// </summary>
    private void ExcludeComboMembers(List<NormalEnemy> validEnemies)
    {
        for (var i = validEnemies.Count - 1; i >= 0; i--)
        {
            if (_comboRegistry.FindComboByMemberGuid(validEnemies[i].EnemyGuid) != null)
            {
                validEnemies.RemoveAt(i);
            }
        }
    }

    private static NormalEnemy FindByGuid(List<NormalEnemy> enemies, string guid)
    {
        for (var i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].EnemyGuid == guid)
                return enemies[i];
        }
        return null;
    }

    private BattleGroup CreateBattleGroup(List<NormalEnemy> resultList, PartyProperty ourImpression)
    {
        var compatibilityData = BuildCompatibilityData(resultList);
        // リーダー（resultList[0]）はTryAddCompatibleTargetを通らないのでここで判定
        var leaderSympathy = resultList.Count > 0 && resultList[0].HP <= resultList[0].MaxHP / 2;
        var sympathy = _anyMemberSympathy || leaderSympathy;
        return new BattleGroup(resultList.Cast<BaseStates>().ToList(), ourImpression,
            Faction.Enemy, compatibilityData, sympathy);
    }

    private Dictionary<(BaseStates, BaseStates), int> BuildCompatibilityData(List<NormalEnemy> resultList)
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
                var compatibilityValue = _matchCalc.GetImpressionMatchPercent(first.MyImpression, second.MyImpression);
                compatibilityData[(first, second)] = compatibilityValue;
                compatibilityValue = _matchCalc.GetImpressionMatchPercent(second.MyImpression, first.MyImpression);
                compatibilityData[(second, first)] = compatibilityValue;
            }
        }

        return compatibilityData;
    }
}
