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
    [Header("命中補正")]
    /// <summary>
    /// 通常の命中補正
    /// </summary>
    [SerializeField]
    int _skillHitPer;
    /// <summary>
    /// スキルの命中補正 int 百分率
    /// </summary>
    public int SkillHitPer
    {
        get
        {
            //スキルレベルが有限範囲なら
            if(FixedSkillLevelData.Count > _nowSkillLevel)
            {
                //-1でないならあるので返す
                if(FixedSkillLevelData[_nowSkillLevel].OptionSkillHitPer != -1)
                {
                    // 現在のレベルから逆向きに検索して最初に有効な値を見つける
                    for(int i = _nowSkillLevel; i >= 0; i--) {
                        if(FixedSkillLevelData[i].OptionSkillHitPer != -1) {
                            return FixedSkillLevelData[i].OptionSkillHitPer;
                        }
                    }
                }
            }
            //当然有限リストは絶対に存在するので、
            //有限範囲以降なら、その最終値でオプションで指定されてるならそれを返す
            //有限リスト外の場合、最後の要素から逆向きに検索
            for(int i = FixedSkillLevelData.Count - 1; i >= 0; i--) {
                if(FixedSkillLevelData[i].OptionSkillHitPer != -1) {
                    return FixedSkillLevelData[i].OptionSkillHitPer;
                }
            }
            //そうでないなら設定値を返す
            return _skillHitPer;
        }
    }

    /// <summary>
    /// スキルにより補正された最終命中率
    /// 引数の命中凌駕はIsReactHitで使われるものなので、キャラ同士の命中回避計算が
    /// 必要ないものであれば、引数を指定しなくていい(デフォ値)
    /// またHitResultの引数は事前の命中回避計算等でどういうヒット結果になったかを渡して最終結果として返すため。
    /// スキル命中onlyならデフォルトで普通のHitが指定されるので渡さなくてOK
    /// </summary>
    /// <param name="supremacyBonus">命中ボーナス　主に命中凌駕用途</param>>
    public virtual HitResult SkillHitCalc(BaseStates target,float supremacyBonus = 0,HitResult hitResult = HitResult.Hit,bool PreliminaryMagicGrazeRoll = false)
    {
        /*schizoLog.AddLog("スキル命中計算が呼ばれた",true);
        // 関数の先頭で一時的に
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.Full);
        Debug.Log($"スキル命中計算が呼ばれた.\n{new System.Diagnostics.StackTrace(1, true)}");*/

        //割り込みカウンターなら確実
        if(Doer.HasPassive(1)) return hitResult;

        //通常計算
        var rndMin = RandomSource.NextInt(3);//ボーナスがある場合ランダムで三パーセント~0パーセント引かれる
        if(supremacyBonus>rndMin)supremacyBonus -= rndMin;

        var result = RandomSource.NextInt(100) < supremacyBonus + SkillHitPer ? hitResult : HitResult.CompleteEvade;
        //schizoLog.AddLog("スキル命中計算式-命中凌駕:" + supremacyBonus + ",スキル命中率:" + SkillHitPer + " " + result,true);

        if(result == HitResult.CompleteEvade && IsMagic)//もし発生しなかった場合、魔法スキルなら
        {
            //三分の一の確率でかする
            if(RandomSource.NextInt(3) == 0) result = HitResult.Graze;
        }

        if(PreliminaryMagicGrazeRoll)//事前魔法かすり判定がIsReactHitで行われていたら
        {
            result = hitResult;//かすりを入れる
        }
        //schizoLog.AddLog("SkillHitCalc-" + result,true);

        return result;
    }
    /// <summary>
    /// Manual系のスキルでの用いるスキルごとの独自効果
    /// </summary>
    /// <param name="target"></param>
    public virtual void ManualSkillEffect(BaseStates target,HitResult hitResult)
    {
        
    }

}
