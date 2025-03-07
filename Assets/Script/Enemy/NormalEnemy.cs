using System;
using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using UnityEngine;

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

    
    /// <summary>
    /// 実際の再復活カウンター　初回は-1
    /// </summary>
    private int _recovelyStepCount = -1;
    //このカウントは死んだ状態でバトルが終わると、入る　Death()ではまだバトル内で復活する恐れがあるから。

    /// <summary>
    /// 最後にエンカウントした時の歩数 再復活用 要は敵が死んだとき"のみ"に記録される。
    /// -1なら初回
    /// </summary>
    private int _lastEncountProgressForReborn = -1;
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
    /// 敵の実体スキルリスト
    /// </summary>
    protected List<SkillAndGrowthPoint> EnemySkillList{ get; private set; } = new();
    /// <summary>
    /// 敵は成長ポイントが-1に指定されたスキルのみ、実体スキルリストとして返す
    /// </summary>
    public override IReadOnlyList<BaseSkill> SkillList => EnemySkillList.Where(skill => skill.growthPoint <= -1).Select(skill => skill.skill).ToList();
    public override void OnInitializeSkillsAndChara()
    {
        foreach (var skill in EnemySkillList)
        {
            skill.skill.OnInitialize(this);
        }
    }    /// <summary>
    /// 敵キャラクターはAttackCharaにてこの関数を通じてNowUseSkillを決める
    /// スキル実行の際に選択可能なオプションがあればここで決める
    /// </summary>
    public virtual void SkillAI()
    {
        //まず使うスキル使えるスキルを選ぶ

        //そのスキル上のオプションを選ぶ
    }

    /// <summary>
    ///初期精神属性決定関数(基本は印象を持ってるスキルリストから適当に選び出す
    /// </summary>
    public virtual void InitializeMyImpression()
    {
        SpiritualProperty that;

        if (SkillList != null)
        {
            var rnd = RandomEx.Shared.NextInt(0, SkillList.Count);
            that = SkillList[rnd].SkillSpiritual; //スキルの精神属性を抽出
            MyImpression = that; //印象にセット
        }
        else
        {
            Debug.Log(CharacterName + " のスキルが空です。");
        }
    }
    /// <summary>
    /// 再遭遇時コールバック。パッシブとか自信ブーストなどの、
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
        foreach(var skill in this.EnemySkillList)
        {
            clone.EnemySkillList.Add(new SkillAndGrowthPoint()
            {
                skill = skill.skill.InitDeepCopy(),
                growthPoint = skill.growthPoint
            });
        }

        /*clone.broken = this.broken;
        clone._recovelyStepCount = this._recovelyStepCount;
        clone._lastEncountProgressForReborn = this._lastEncountProgressForReborn;
        clone._lastEncounterProgress = this._lastEncounterProgress;*/
        //全て戦闘の時に使われる前提の値だからコメントアウト

        // 4. 戻り値
        return clone;
    }

}
/// <summary>
/// NormalEnemyで用いるスキルとその成長ポイントを保持するためのクラス
/// </summary>
public class SkillAndGrowthPoint
{
    public BaseSkill skill;
    /// <summary>
    /// -1で指定した場合、初期化時にそのまま有効化する
    /// </summary>
    public int growthPoint;
}