using System;
using System.Collections.Generic;

/// <summary>
///     通常の敵
/// </summary>
[Serializable]
public class NormalEnemy : BaseStates
{
    /// <summary>
    ///     この敵キャラクターの復活する歩数
    ///     手動だが基本的に生命キャラクターのみにこの歩数は設定する?
    /// </summary>
    public int RecovelySteps;

    /// <summary>
    /// 復活するかどうか
    /// </summary>
    public bool Reborn
    {
        get { return RecovelySteps > 0; }//復活歩数がゼロより上なら復活するタイプ。
    }

    /// <summary>
    /// 完全死滅してるかどうか。
    /// </summary>
    public bool broken = false;　

    private int _recovelyStepCount;//実際のカウント
    //このカウントは死んだ状態でバトルが終わると、入る　Death()ではまだバトル内で復活する恐れがあるから。

    private int _lastEncountProgress;//最後にエンカウントした時の歩数
    /// <summary>
    /// 復活歩数をカウント変数にセットする。　
    /// </summary>
    public void ReadyRecovelyStep(int nowProgress)
    {
        _lastEncountProgress = nowProgress;//今回の進行度を保存する。
        _recovelyStepCount = RecovelySteps;
    }

    /// <summary>
    /// "一回死んだ復活可能者"が復活するかどうか
    /// </summary>
    public bool CanRebornWhatHeWill(int nowProgress)
    {
        if (_recovelyStepCount <= 0) return false;//既にカウントがゼロなら生きてるってこと。　復活カウントもせずに返す

        var difference = Math.Abs(nowProgress - _lastEncountProgress);//差の絶対値を取得

        if((_recovelyStepCount -= difference) <= 0)//復活までのカウントから(前エリアと今の進行度の差)を引いて0以下になったら
        {
            _recovelyStepCount = 0;//逃げてもまた出てくる　殺さないとまたrecovelyStepは設定されない。
            return true;//復活する。
        }

        _lastEncountProgress = nowProgress;//もしカウントが終わってなかったら今回の進行度を保存する

        return false;
    }
    /// <summary>
    /// 敵キャラクターはAttackCharaにてこの関数を通じてNowUseSkillを決める
    /// </summary>
    public virtual void SkillAI()
    {

    }
    public override string AttackChara(BaseStates UnderAttacker)
    {
        SkillAI();
        return base.AttackChara(UnderAttacker);
    }




}