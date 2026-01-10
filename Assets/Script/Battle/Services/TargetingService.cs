using System;
using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using RandomExtensions.Collections;
using RandomExtensions.Linq;
using UnityEngine;
using static CommonCalc;

public sealed class TargetingService
{
    private readonly bool debugSelectLog;

    public TargetingService(bool debugSelectLog = false)
    {
        this.debugSelectLog = debugSelectLog;
    }

    public void SelectTargets(
        BaseStates acter,
        allyOrEnemy acterFaction,
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        UnderActersEntryList unders,
        Action<string> appendTopMessage)
    {
        if (acter.HasRangeWill(SkillZoneTrait.SelfSkill))
        {
            unders.CharaAdd(acter);
            return;
        }

        BattleGroup selectGroup;
        BattleGroup ourGroup = null;
        List<BaseStates> ua = new();

        if (acterFaction == allyOrEnemy.alliy)
        {
            if (acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
            {
                selectGroup = new BattleGroup(allyGroup.Ours, allyGroup.OurImpression, allyGroup.which);
                if (!acter.HasRangeWill(SkillZoneTrait.CanSelectMyself))
                {
                    selectGroup.Ours.Remove(acter);
                }
            }
            else
            {
                selectGroup = new BattleGroup(enemyGroup.Ours, enemyGroup.OurImpression, enemyGroup.which);

                if (acter.HasRangeWill(SkillZoneTrait.CanSelectAlly))
                {
                    ourGroup = new BattleGroup(allyGroup.Ours, allyGroup.OurImpression, allyGroup.which);
                    if (!acter.HasRangeWill(SkillZoneTrait.CanSelectMyself))
                    {
                        ourGroup.Ours.Remove(acter);
                    }
                }
                else if (acter.HasRangeWill(SkillZoneTrait.CanSelectMyself))
                {
                    ourGroup = new BattleGroup(new List<BaseStates> { acter }, allyGroup.OurImpression, allyGroup.which);
                }
            }
        }
        else
        {
            if (acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
            {
                selectGroup = new BattleGroup(enemyGroup.Ours, enemyGroup.OurImpression, enemyGroup.which);
                if (!acter.HasRangeWill(SkillZoneTrait.CanSelectMyself))
                {
                    selectGroup.Ours.Remove(acter);
                }
            }
            else
            {
                selectGroup = new BattleGroup(allyGroup.Ours, allyGroup.OurImpression, allyGroup.which);
                if (acter.HasRangeWill(SkillZoneTrait.CanSelectAlly))
                {
                    ourGroup = new BattleGroup(enemyGroup.Ours, enemyGroup.OurImpression, enemyGroup.which);
                    if (!acter.HasRangeWill(SkillZoneTrait.CanSelectMyself))
                    {
                        ourGroup.Ours.Remove(acter);
                    }
                }
                else if (acter.HasRangeWill(SkillZoneTrait.CanSelectMyself))
                {
                    ourGroup = new BattleGroup(new List<BaseStates> { acter }, enemyGroup.OurImpression, enemyGroup.which);
                }
            }
        }

        if (!acter.HasRangeWill(SkillZoneTrait.CanSelectDeath))
        {
            selectGroup.SetCharactersList(RemoveDeathCharacters(selectGroup.Ours));
            if (ourGroup != null)
            {
                ourGroup.SetCharactersList(RemoveDeathCharacters(ourGroup.Ours));
            }
        }

        if (acter.Target != DirectedWill.One)
        {
            if (selectGroup.Ours.Count < 2)
            {
                Debug.Log("敵に一人しかいません");
                ua.Add(selectGroup.Ours[0]);
            }
            else
            {
                if (acter.HasRangeWill(SkillZoneTrait.CanSelectSingleTarget))
                {
                    if (selectGroup.InstantVanguard == null)
                    {
                        ua.AddRange(SelectByPassiveAndRandom(selectGroup.Ours, 2, 23));
                    }
                    else
                    {
                        if (acter.Target == DirectedWill.InstantVanguard)
                        {
                            ua.Add(selectGroup.InstantVanguard);
                            Debug.Log(acter.CharacterName + "は前のめりしてる奴を狙った");
                        }
                        else if (acter.Target == DirectedWill.BacklineOrAny)
                        {
                            if (ComparePressureAndRedirect(acter, selectGroup.InstantVanguard))
                            {
                                appendTopMessage?.Invoke("テラーズヒット");
                                ua.Add(selectGroup.InstantVanguard);
                                Debug.Log(acter.CharacterName + "は後衛を狙ったが前のめりしてる奴に阻まれた");
                            }
                            else
                            {
                                List<BaseStates> backLines = new List<BaseStates>(selectGroup.Ours.Where(member => member != selectGroup.InstantVanguard));

                                ua.AddRange(SelectByPassiveAndRandom(backLines, 1));
                                acter.SetSpecialModifier("少し遠いよ", whatModify.eye, 0.7f);
                                Debug.Log(acter.CharacterName + "は後衛を狙った");
                            }
                        }
                        else
                        {
                            Debug.LogError("CanSelectSingleTargetの処理では前のめりか後衛以外の意志を受け付けていません。");
                        }
                    }
                }
                else if (acter.HasRangeWill(SkillZoneTrait.RandomSingleTarget))
                {
                    var selects = new List<BaseStates>(selectGroup.Ours);
                    if (ourGroup != null)
                        selects.AddRange(ourGroup.Ours);

                    ua.Add(RandomEx.Shared.GetItem(selects.ToArray()));
                }
                else if (acter.HasRangeWill(SkillZoneTrait.ControlByThisSituation))
                {
                    Debug.Log("ControlByThisSituationのスキル分岐(SelectTargetFromWill)");
                    if (selectGroup.InstantVanguard == null)
                    {
                        Debug.Log("ControlByThisSituationのスキル分岐(SelectTargetFromWill)で前のめりがいない");

                        bool isAccident = false;

                        if (acter.HasRangeWill(SkillZoneTrait.RandomSingleTarget))
                        {
                            Debug.Log("ランダムシングル事故");
                            var selects = new List<BaseStates>(selectGroup.Ours);
                            if (ourGroup != null)
                                selects.AddRange(ourGroup.Ours);

                            ua.AddRange(SelectByPassiveAndRandom(selects, 1));
                            isAccident = true;
                        }

                        if (acter.HasRangeWill(SkillZoneTrait.AllTarget))
                        {
                            Debug.Log("全範囲事故");
                            var selects = new List<BaseStates>(selectGroup.Ours);
                            if (ourGroup != null)
                                selects.AddRange(ourGroup.Ours);

                            ua.AddRange(selects);
                            isAccident = true;
                        }
                        if (acter.HasRangeWill(SkillZoneTrait.RandomMultiTarget))
                        {
                            Debug.Log("ランダム範囲事故");
                            var selects = new List<BaseStates>(selectGroup.Ours);
                            if (ourGroup != null)
                                selects.AddRange(ourGroup.Ours);

                            int want = RandomEx.Shared.NextInt(1, selects.Count + 1);
                            Debug.Log($"ランダム範囲事故対象者数(パッシブ判定前) : {want}");
                            var charas = SelectByPassiveAndRandom(selects, want);
                            ua.AddRange(charas);
                            isAccident = true;
                            Debug.Log($"ランダム範囲事故対象者数(パッシブ判定後) : {charas.Count}");
                        }

                        if (!isAccident)
                        {
                            Debug.LogAssertion("ControlByThisSituationによるNon前のめり事故が起きなかった\n事故用に範囲意志をスキルに設定する必要があります。");
                        }
                    }
                    else
                    {
                        ua.Add(selectGroup.InstantVanguard);
                    }
                }
                else if (acter.HasRangeWill(SkillZoneTrait.CanSelectMultiTarget))
                {
                    if (selectGroup.InstantVanguard == null)
                    {
                        ua.AddRange(SelectByPassiveAndRandom(selectGroup.Ours, 2));
                    }
                    else
                    {
                        if (acter.Target == DirectedWill.InstantVanguard)
                        {
                            ua.Add(selectGroup.InstantVanguard);
                            Debug.Log(acter.CharacterName + "は前のめりしてる奴を狙った");
                        }
                        else if (acter.Target == DirectedWill.BacklineOrAny)
                        {
                            if (ComparePressureAndRedirect(acter, selectGroup.InstantVanguard))
                            {
                                appendTopMessage?.Invoke("テラーズヒット");
                                ua.Add(selectGroup.InstantVanguard);
                                Debug.Log(acter.CharacterName + "は後衛を狙ったが前のめりしてる奴に阻まれた");
                            }
                            else
                            {
                                List<BaseStates> backLines = new List<BaseStates>(selectGroup.Ours.Where(member => member != selectGroup.InstantVanguard));

                                ua.AddRange(backLines);
                                acter.SetSpecialModifier("ほんの少し狙いにくい", whatModify.eye, 0.9f);
                                Debug.Log(acter.CharacterName + "は後衛を狙った");
                            }
                        }
                        else
                        {
                            Debug.LogError("CanSelectMultiTargetの処理では前のめりか後衛以外の意志を受け付けていません。");
                        }
                    }
                }
                else if (acter.HasRangeWill(SkillZoneTrait.RandomSelectMultiTarget))
                {
                    var selectVanguard = RandomEx.Shared.NextBool();

                    if (selectGroup.InstantVanguard == null)
                    {
                        var counter = 0;
                        selectGroup.Ours.Shuffle();
                        foreach (var one in selectGroup.Ours)
                        {
                            ua.Add(one);
                            counter++;
                            if (counter >= 2) break;
                        }
                    }
                    else
                    {
                        if (selectVanguard)
                        {
                            ua.Add(selectGroup.InstantVanguard);
                            Debug.Log(acter.CharacterName + "の技は前のめりしてる奴に向いた");
                        }
                        else
                        {
                            List<BaseStates> backLines = new List<BaseStates>(selectGroup.Ours.Where(member => member != selectGroup.InstantVanguard));

                            ua.AddRange(backLines);
                            Debug.Log(acter.CharacterName + "の技は後衛に向いた");
                        }
                    }
                }
                else if (acter.HasRangeWill(SkillZoneTrait.RandomMultiTarget))
                {
                    List<BaseStates> selects = selectGroup.Ours;
                    if (ourGroup != null)
                        selects.AddRange(ourGroup.Ours);

                    var count = selects.Count;
                    count = RandomEx.Shared.NextInt(1, count + 1);

                    for (int i = 0; i < count; i++)
                    {
                        var item = RandomEx.Shared.GetItem(selects.ToArray());
                        ua.Add(item);
                        selects.Remove(item);
                    }
                }
                else if (acter.HasRangeWill(SkillZoneTrait.AllTarget))
                {
                    var selects = new List<BaseStates>(selectGroup.Ours);
                    if (ourGroup != null)
                        selects.AddRange(ourGroup.Ours);

                    ua.AddRange(selects);
                }
            }

            if (unders.Count < 1)
            {
                ua.Shuffle();
                unders.SetList(ua);
            }
        }
    }

    private bool ComparePressureAndRedirect(BaseStates attacker, BaseStates vanguard)
    {
        var vanguardPressure = vanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.Glory);
        var attackerResilience = attacker.TenDayValues(false).GetValueOrZero(TenDayAbility.JoeTeeth)
                                 + attacker.TenDayValues(false).GetValueOrZero(TenDayAbility.WaterThunderNerve) * 0.5f;

        return vanguardPressure > RandomEx.Shared.NextFloat(vanguardPressure + attackerResilience);
    }

    private List<BaseStates> SelectByPassiveAndRandom(IEnumerable<BaseStates> candidates, int want, int nextChancePercent = 100)
    {
        var pool = candidates.ToList();
        var winners = new List<BaseStates>();
        if (want <= 0 || pool.Count == 0)
        {
            if (debugSelectLog)
                Debug.Log($"[SelectByPassiveAndRandom] 早期終了: 要求数={want}, 候補数={pool.Count}, 当選数={winners.Count}（選出不要または候補なし）");
            return winners;
        }

        var positives = pool
            .Where(u => u.PassivesTargetProbability() > 0)
            .OrderByDescending(u => u.PassivesTargetProbability())
            .ToList();
        foreach (var u in positives)
        {
            if (winners.Count >= want) break;
            if (rollper(u.PassivesTargetProbability()))
                winners.Add(u);
        }
        if (winners.Count >= want)
        {
            if (debugSelectLog)
                Debug.Log($"[SelectByPassiveAndRandom] Positive選抜で終了: 要求数={want}, 当選数={winners.Count}/{pool.Count}");
            return winners;
        }

        var negatives = pool.Where(u => u.PassivesTargetProbability() < 0).ToList();
        var excludedByNegative = new HashSet<BaseStates>();
        foreach (var u in negatives)
        {
            if (rollper(-u.PassivesTargetProbability()))
                excludedByNegative.Add(u);
        }
        if (debugSelectLog)
            Debug.Log($"[SelectByPassiveAndRandom] Negative除外結果: ネガ対象数={negatives.Count}, 除外確定数={excludedByNegative.Count}");

        var others = pool
            .Except(winners)
            .Where(u => !excludedByNegative.Contains(u))
            .ToList();
        if (others.Count == 0 && winners.Count < want)
        {
            if (debugSelectLog)
                Debug.Log($"[SelectByPassiveAndRandom] ランダム抽選スキップ: 候補0, 当選数={winners.Count}, 要求数={want}");
        }
        if (others.Count > 0 && winners.Count < want)
        {
            var weighted = new WeightedList<BaseStates>();
            foreach (var u in others)
            {
                var w = u.PassivesTargetProbability();
                if (w <= 0) w = 1;
                weighted.Add(u, w);
            }
            if (debugSelectLog)
                Debug.Log($"[SelectByPassiveAndRandom] ランダム抽選開始: 候補={others.Count}, 重み数={weighted.Count}, 当選数={winners.Count}, 要求数={want}, 継続率={nextChancePercent}%");
            do
            {
                if (weighted.Count == 0) break;
                weighted.RemoveRandom(out var item);
                winners.Add(item);
            } while (weighted.Count > 0 && winners.Count < want && RandomEx.Shared.NextInt(100) <= nextChancePercent);
            if (debugSelectLog)
                Debug.Log($"[SelectByPassiveAndRandom] ランダム抽選終了: 当選数={winners.Count}, 残り重み={weighted.Count}");
        }

        if (winners.Count < want && nextChancePercent == 100)
        {
            var negPicked = negatives.Where(excludedByNegative.Contains).ToList();
            negPicked.Shuffle();
            var before = winners.Count;
            foreach (var u in negPicked)
            {
                if (winners.Count >= want) break;
                winners.Add(u);
            }
            if (debugSelectLog)
                Debug.Log($"[SelectByPassiveAndRandom] ネガ補充: 追加数={winners.Count - before}, 当選数={winners.Count}, 要求数={want}");
        }
        if (debugSelectLog)
            Debug.Log($"[SelectByPassiveAndRandom] 最終返却: 要求数={want}, 当選数={winners.Count}/{pool.Count}, 継続率={nextChancePercent}%");
        return winners;
    }
}
