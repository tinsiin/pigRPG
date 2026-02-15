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
    /// <summary>
    /// 初期化コールバック関数 初期化なので起動時の最初の一回しか使わないような処理しか書かないようにして
    /// </summary>
    public void OnInitialize(BaseStates owner)
    {
        Debug.Log($"スキル{SkillName}の初期化 (owner: {owner?.CharacterName})");
        IsInitialized = true;
        ResetStock();//_nowstockは最初は0になってるので、初期化でdefaultstockと同じ数にする。
    }
    /// <summary>
    /// 行使者 doerが死亡したときdoer側で呼ばれるコールバック
    /// </summary>
    public void OnBattleDeath()
    {
        ResetStock();
        ResetAtkCountUp();
        ReturnTrigger();
    }
    public void OnBattleStart()
    {
        //カウントが専ら参照されるので、バグ出ないようにとりあえず仮のムーブセットを決めておく。
        DecideNowMoveSet_A0_B1(0);
    }
    /// <summary>
    /// スキルの"一時保存"系プロパティをリセットする　BattleManager終了時など
    /// </summary>
    public void OnBattleEnd()
    {
        _doCount = 0;
        _doConsecutiveCount = 0;
        _hitCount = 0;
        _hitConsecutiveCount = 0;
        _cradleSkillLevel = -1;//ゆりかご用スキルレベルをエラーにする。
        ResetAtkCountUp();
        ReturnTrigger();//発動カウントはカウントダウンするから最初っから
        _tmpSkillUseTurn = -1;//前回とのターン比較用の変数をnullに
        ResetStock();

        ///スキルパッシブの終了時の処理
        foreach(var pas in ReactiveSkillPassiveList.Where(pas => pas.DurationWalkTurn < 0))
        {
            RemoveSkillPassive(pas);
        }
    }


}