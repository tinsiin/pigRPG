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
        get { return RecovelySteps >= 0; }//復活歩数がゼロ以上なら復活する敵　つまり-1に設定すると復活しない
    }

    

    private int _recovelyStepCount;//実際のカウント
    //このカウントは死んだ状態でバトルが終わると、入る　Death()ではまだバトル内で復活する恐れがあるから。

    /// <summary>
    /// 最後にエンカウントした時の歩数 再復活用 要は敵が死んだとき"のみ"に記録される。
    /// </summary>
    private int _lastEncountProgressForReborn;
    /// <summary>
    /// 復活歩数をカウント変数にセットする。　
    /// </summary>
    public void ReadyRecovelyStep(int nowProgress)
    {
        _lastEncountProgressForReborn = nowProgress;//今回の進行度を保存する。
        _recovelyStepCount = RecovelySteps;
    }

    /// <summary>
    /// "一回死んだ復活可能者"が復活するかどうか　要は今回復活するかまたはそもそも生きてるかどうか
    /// </summary>
    public bool CanRebornWhatHeWill(int nowProgress)
    {
        if (_recovelyStepCount <= 0) return true;//既にカウントがゼロなら生きてるってこと。　復活カウントもせずに返す

        var distanceTraveled = Math.Abs(nowProgress - _lastEncountProgressForReborn);//差の絶対値 = 移動距離 を取得

        if((_recovelyStepCount -= distanceTraveled) <= 0)//復活までのカウントから(前エリアと今の進行度の差)を引いて0以下になったら
        {
            _recovelyStepCount = 0;//逃げてもまた出てくる　殺さないとまたrecovelyStepは設定されない。

            return true;//復活する。
        }

        _lastEncountProgressForReborn = nowProgress;//もしカウントが終わってなかったら今回の進行度を保存する

        return false;
    }
    /// <summary>
    /// 最後にエンカウントした時の歩数  会ってさえいればとりあえず記録 -1なら初回
    /// </summary>
    private int _lastEncounterProgress = -1;

    /// <summary>
    /// 敵キャラクターはAttackCharaにてこの関数を通じてNowUseSkillを決める
    /// スキル実行の際に選択可能なオプションがあればここで決める
    /// </summary>
    public virtual void SkillAI()
    {
        //まず使うスキル使えるスキルを選ぶ

        //そのスキル上のオプションを選ぶ
    }
    /// <summary>
    /// パッシブとか自信ブーストなどの、
    // 歩行に変化のあるものは敵グループはここら辺で一気に処理をする。
    /// </summary>
    public void ReEncountCallback()
    {
        bool isFirstEncounter = false;
        var distanceTraveled = 0;

        //遭遇したら遭遇地点記録
        if(_lastEncounterProgress == -1)
        {//もし初回遭遇なら
            isFirstEncounter = true;
        }else{
            //二回目以降の遭遇なら移動距離を取得
            distanceTraveled = Math.Abs(PlayersStates.Instance.NowProgress - _lastEncounterProgress);//移動距離取得
        }

        //二回目以降の遭遇の処理
        if(!isFirstEncounter)
        {
            //自信ブーストの再遭遇減衰処理
            FadeConfidenceBoostByWalking(distanceTraveled);

            //パッシブ歩行効果
            for(var i = 0 ; i < distanceTraveled ; i++)
            {
                AllPassiveWalkEffect();//歩行効果
                UpdateWalkAllPassiveSurvival();//歩行によるパッシブ残存処理
            }          
        }

        //遭遇地点を記録する。
        _lastEncounterProgress = PlayersStates.Instance.NowProgress;
    }


        /// <summary>
        ///     NormalEnemy を深いコピーする
        ///     BaseStates のフィールドも含めてコピーする
        /// </summary>
    public NormalEnemy DeepCopy()
    {
        // 1. 新しい NormalEnemy インスタンスを生成
        var clone = new NormalEnemy();

        // 2. BaseStates のフィールドをコピー
        InitBaseStatesDeepCopy(clone);

        // 3. NormalEnemy 独自フィールドをコピー
        clone.RecovelySteps = this.RecovelySteps;
        clone.broken = this.broken;
        clone._recovelyStepCount = this._recovelyStepCount;
        clone._lastEncountProgressForReborn = this._lastEncountProgressForReborn;

        // 4. 戻り値
        return clone;
    }

}