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
    int TriggerCountMax => FixedSkillLevelData[_levelIndex].TriggerCountMax;

    public bool CanCancelTrigger => FixedSkillLevelData[_levelIndex].CanCancelTrigger;

    int TriggerRollBackCount => FixedSkillLevelData[_levelIndex].TriggerRollBackCount;


    private int _triggerCount;//発動への−カウント　このカウント分連続でやらないと発動しなかったりする　重要なのは連続でやらなくても　一気にまたゼロからになるかはスキル次第


    /// <summary>
    /// スキル実行に必要なカウント　-1で実行される。
    /// </summary>
    public virtual int TrigerCount()
    {
        if (TriggerCountMax > 0)//1回以上設定されてたら
        {
            _triggerCount--;
            return _triggerCount;
        }

        //発動カウントが0に設定されている場合、そのまま実行される。
        return -1;
    }
    /// <summary>
    /// トリガーカウントをスキルの巻き戻しカウント数に応じて巻き戻す処理
    /// </summary>
    public void RollBackTrigger()
    {
        _triggerCount += TriggerRollBackCount;
        if (_triggerCount > TriggerCountMax)_triggerCount = TriggerCountMax;//最大値を超えないようにする
    }

    /// <summary>
    /// 発動カウントが実行中かどうかを判定する
    /// </summary>
    /// <returns>発動カウントが開始済みで、まだカウント中ならtrue、それ以外はfalse</returns>
    public bool IsTriggering
    {
        get{
            // 発動カウントが0以下の場合は即時実行なのでfalse
            if (TriggerCountMax <= 0) return false;

            // カウントが開始されていない場合はfalse
            // カウントが開始されると_triggerCountはTriggerCountMaxより小さくなる
            if (_triggerCount >= TriggerCountMax) return false;

            // カウントが開始済みで、まだカウントが残っている場合はtrue
            return _triggerCount  > -1;
        }
    }

    /// <summary>
    /// 実行に成功した際の発動カウントのリセット 0ばら
    /// </summary>
    public virtual void ReturnTrigger()
    {
        _triggerCount = TriggerCountMax;//基本的にもう一回最初から
    }

}
