using R3;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
public partial class BaseSkill
{
    [Header("スキルパッシブ設定")]
    
    /// <summary>
    /// スキルに掛ってるパッシブリスト
    /// このスキルが感染してる病気ってこと
    /// </summary>
    public List<BaseSkillPassive> ReactiveSkillPassiveList = new();
    /// <summary>
    /// このスキルがスキルパッシブ付与スキルとして実行される際の、装弾されたスキルパッシブです
    /// 除去スキルはスキルパッシブごとに判別しないため、これは付与スキルパッシブのみのリストです
    /// </summary>
    public List<BaseSkillPassive> AggressiveSkillPassiveList = new();
    /// <summary>
    /// スキルパッシブをリストから除去する
    /// </summary>
    public void RemoveSkillPassive(BaseSkillPassive passive)
    {
        ReactiveSkillPassiveList.Remove(passive);
    }
    /// <summary>
    /// スキル効果により、一気にスキルパッシブを抹消する関数
    /// スキルパッシブの性質によるもの
    /// </summary>
    public void SkillRemoveSkillPassive()
    {
        ReactiveSkillPassiveList.Clear();
    }
    /// <summary>
    /// スキルパッシブを付与する。
    /// </summary>
    /// <param name="passive"></param>
    public void ApplySkillPassive(BaseSkillPassive passive)
    {
        ReactiveSkillPassiveList.Add(passive);
    }

    /// <summary>
    /// スキルパッシブバージョンの付与処理用バッファーリスト
    /// </summary>
    List<BaseSkillPassive> BufferApplyingSkillPassiveList = new();
    /// <summary>
    /// バッファのスキルパッシブを追加する。
    /// </summary>
    public void ApplyBufferApplyingSkillPassive()
    {
        foreach(var passive in BufferApplyingSkillPassiveList)
        {
            ApplySkillPassive(passive);
        }
        BufferApplyingSkillPassiveList.Clear();//追加したからバッファ消す
    }
    /// <summary>
    /// 戦闘中のスキルパッシブ追加は基本バッファに入れよう
    /// </summary>
    public void ApplySkillPassiveBufferInBattle(BaseSkillPassive passive)
    {
        BufferApplyingSkillPassiveList.Add(passive);
    }



    /// <summary>
    /// 付与するスキルを選択する方式。
    /// </summary>
    public SkillPassiveTargetSelection TargetSelection;

    /// <summary>
    /// スキルパッシブ付与スキルである際の、反応式の対象キャラとスキル
    /// </summary>
    public List<SkillPassiveReactionCharaAndSkill> ReactionCharaAndSkillList = new();

    /// <summary>
    /// スキルパッシブ付与スキルだとして、何個まで付与できるか。
    /// </summary>
    public int SkillPassiveEffectCount = 1;
    /// <summary>
    /// スキルパッシブ付与スキルのスキルの区別フィルター
    /// </summary>
    [SerializeField] SkillFilter _skillPassiveGibeSkill_SkillFilter = new();

    /// <summary>
    /// スキルパッシブの付与対象となるスキルを対象者のスキルリストから選ぶ。
    /// </summary>
    public async UniTask<List<BaseSkill>> SelectSkillPassiveAddTarget(BaseStates target)
    {
        var targetSkills = target.SkillList.ToList();//ターゲットの現在解放されてるスキル
        if(targetSkills.Count == 0)
        {
        Debug.LogError("スキルパッシブの対象スキルの選別を試みましたが、\n対象者のスキルリストが空です");
        return null;}

        //直接選択式(UI)
        if(TargetSelection == SkillPassiveTargetSelection.Select)
        {
            //敵ならAIで 未実装
            if(manager.GetCharacterFaction(Doer) == allyOrEnemy.Enemyiy)
            {
                
            }

            //味方はUI選択
            if(manager.GetCharacterFaction(Doer) == allyOrEnemy.alliy)
            {
                if(_skillPassiveGibeSkill_SkillFilter != null && _skillPassiveGibeSkill_SkillFilter.HasAnyCondition)//フィルタ条件があるなら絞り込む
                {
                    //フィルタで絞り込む
                    targetSkills = targetSkills.Where(s => s.MatchFilter(_skillPassiveGibeSkill_SkillFilter)).ToList();
                    // 絞り込み後に候補が0件の場合
                    if(targetSkills.Count == 0)
                    {
                        Debug.LogWarning("フィルタ条件に合致するスキルがありませんでした。UI選択をキャンセルします。");
                        return null;
                    }
                }

                //選択ボタンエリア生成と受け取り
                var skillUi = Doer?.SkillUI;
                if (skillUi == null)
                {
                    Debug.LogError("BaseSkill.SkillPassive: SkillUI が null です");
                    return null;
                }
                var result = await skillUi.GoToSelectSkillPassiveTargetSkillButtonsArea(targetSkills, SkillPassiveEffectCount);


                if(result.Count == 0)
                {
                    Debug.LogError("スキルパッシブの対象スキルを直接選択しましたが何も返ってきません");
                    return null;
                }
                return result;
            }

        }

        //反応式
        if(TargetSelection == SkillPassiveTargetSelection.Reaction)
        {
            var correctReactSkills = new List<BaseSkill>();

            //そのキャラのターゲットスキルに対して反応する「キャラとスキルの」リストが一致したら
            foreach(var targetSkill in targetSkills)
            {
                foreach(var hold in ReactionCharaAndSkillList)//反応するキャラとスキルのリスト
                {
                    //そもそもキャラ名が違っていたら、飛ばす
                    if(target.CharacterName != hold.CharaName) continue;
                    
                    if(targetSkill.SkillName == hold.SkillName)//スキル名まで一致したら
                    {
                        correctReactSkills.Add(targetSkill);//今回のターゲットスキルを入れる。
                        break;//スキルが一致して、他のスキルネームで検証する必要がなくなったので、次の対象スキルへ
                    }
                }
            }
            if(correctReactSkills.Count == 0)
            {
                Debug.Log("スキルパッシブ付与スキルはどのスキルにも反応しませんでした。");
                return null;
            }
            return correctReactSkills;
        }


        //ランダム方式(フィルタ条件による区切り実装済み)
        if(TargetSelection == SkillPassiveTargetSelection.Random)
        {
            var randomSkills = new List<BaseSkill>();

            if(_skillPassiveGibeSkill_SkillFilter != null && _skillPassiveGibeSkill_SkillFilter.HasAnyCondition)
            {
                // フィルタ条件がある場合：絞り込んでから抽選
                var candidates = targetSkills.Where(s => s.MatchFilter(_skillPassiveGibeSkill_SkillFilter)).ToList();
                if(candidates.Count == 0)
                {
                    Debug.LogWarning("フィルタ条件に合致するスキルがありませんでした。");
                    return null;
                }
                
                int selectCount = Math.Min(SkillPassiveEffectCount, candidates.Count);
                for(int i = 0; i < selectCount; i++)// 絞り込んだスキルリスト からランダム選択
                {
                    var  item = RandomEx.Shared.GetItem(candidates.ToArray());
                    randomSkills.Add(item);
                    candidates.Remove(item);
                }
            }
            else
            {//全体からrandomに一つ(指定個数分)選ぶ単純な方式
                //付与対象のスキル数が指定個数より少ない場合、ループが終わる
                int selectCount = Math.Min(SkillPassiveEffectCount, targetSkills.Count);
                for(int i = 0; i < selectCount; i++)
                {
                    var  item = RandomEx.Shared.GetItem(targetSkills.ToArray());//ランダムに選んで
                    randomSkills.Add(item);//追加
                    targetSkills.Remove(item);//重複を防ぐため削除
                }
            }

            return randomSkills;
        }
        return null;
    }

    public bool MatchFilter(SkillFilter filter)
    {
        if (filter == null || !filter.HasAnyCondition) return true;

        // 基本方式の判定
        if (filter.Impressions.Count > 0 && !filter.Impressions.Contains(Impression))//スキル印象
            return false;
        if (filter.MotionFlavors.Count > 0 && !filter.MotionFlavors.Contains(MotionFlavor))//動作的雰囲気
            return false;
        if (filter.MentalAttrs.Count > 0 && !filter.MentalAttrs.Contains(SkillSpiritual))//精神属性
            return false;
        if (filter.PhysicalAttrs.Count > 0 && !filter.PhysicalAttrs.Contains(SkillPhysical))//物理属性
            return false;
        if (filter.AttackTypes.Count > 0 && !filter.AttackTypes.Contains(DistributionType))//スキル分散性質
            return false;

        // b方式の判定
        //十日能力
        if (filter.TenDayAbilities.Count > 0 && 
            !SkillFilterUtil.CheckContain(EnumerateTenDayAbilities(), filter.TenDayAbilities, filter.TenDayMode))
            return false;

        //スキルの攻撃性質
        if (filter.SkillTypes.Count > 0 && 
            !SkillFilterUtil.CheckContain(EnumerateSkillTypes(), filter.SkillTypes, filter.SkillTypeMode))
            return false;

        //スキル特殊判別性質
        if (filter.SpecialFlags.Count > 0 && 
            !SkillFilterUtil.CheckContain(EnumerateSpecialFlags(), filter.SpecialFlags, filter.SpecialFlagMode))
            return false;

        return true;
    }
        
        /// <summary>
        /// SkillTypeを列挙可能にする
        /// </summary>
        public IEnumerable<SkillType> EnumerateSkillTypes()
        {
            return SkillFilterUtil.FlagsToEnumerable<SkillType>(SkillType);
        }

        /// <summary>
        /// SpecialFlagを列挙可能にする  
        /// </summary>
        public IEnumerable<SkillSpecialFlag> EnumerateSpecialFlags()
        {
            return SkillFilterUtil.FlagsToEnumerable<SkillSpecialFlag>(SpecialFlags);
        }

        /// <summary>
        /// TenDayAbilityを列挙可能にする
        /// </summary>
        public IEnumerable<TenDayAbility> EnumerateTenDayAbilities()
        {
            return TenDayValues().Keys;
        }

    }
    /// <summary>
    /// スキルの区別判定方式
    /// </summary>
    public enum ContainMode { Any, All }   // b方式のどちらで判定するか
    public static class SkillFilterUtil
    {
        // Flags列挙体として使われるビットフラグを個別の列挙体に分解
        public static IEnumerable<TEnum> FlagsToEnumerable<TEnum>(Enum flags)
        {
            foreach (TEnum val in Enum.GetValues(typeof(TEnum)))
                if (flags.HasFlag((Enum)(object)val)) yield return val;
        }

        // 条件判定
        public static bool CheckContain<T>(IEnumerable<T> skillValues, 
                                        List<T> filterValues, 
                                        ContainMode mode)
        {
            var filterSet = new HashSet<T>(filterValues);
            return mode == ContainMode.Any 
                ? skillValues.Any(filterSet.Contains)
                : filterSet.All(skillValues.Contains);
        }

    }
    /// <summary>
    /// スキル区別方式
    /// 特定のプロパティに応じてスキルを区分けするためのフィルタークラス
    /// </summary>
    [Serializable]
    public class SkillFilter
    {
        // —— 基本方式 ——            
    /// <summary>スキル印象の区切り</summary>
        public List<SkillImpression> Impressions = new();
    /// <summary>動作的雰囲気の区切り</summary>
        public List<MotionFlavorTag> MotionFlavors = new();
    /// <summary>精神属性の区切り</summary>
        public List<SpiritualProperty> MentalAttrs = new();
    /// <summary>物理属性の区切り</summary>
        public List<PhysicalProperty> PhysicalAttrs = new();
    /// <summary>スキルの分散割合タイプの区切り</summary>
        public List<AttackDistributionType> AttackTypes = new();

        // —— b方式（スキル側が複数値を持ち得るもの）——
    /// <summary>十日能力の区切り</summary>
        public List<TenDayAbility> TenDayAbilities = new();
        public ContainMode TenDayMode = ContainMode.Any;
        /// <summary>スキルの攻撃性質の区切り</summary>
        public List<SkillType> SkillTypes = new();
        public ContainMode SkillTypeMode = ContainMode.Any;
        /// <summary>スキルの特殊判別性質の区切り</summary>
        public List<SkillSpecialFlag> SpecialFlags = new();
        public ContainMode SpecialFlagMode = ContainMode.Any;

        

        /// <summary>
        /// 条件が 1 つもセットされていない場合は「フィルタ無し」とみなす
        /// </summary>
        public bool HasAnyCondition =>
            Impressions.Count + MotionFlavors.Count + MentalAttrs.Count + PhysicalAttrs.Count +
            AttackTypes.Count + TenDayAbilities.Count + SkillTypes.Count + SpecialFlags.Count > 0;
    }
