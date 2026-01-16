using System;
using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using RandomExtensions.Linq;
using UnityEngine;
using UnityEngine.UI;
using static CommonCalc;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

/// <summary>
/// 敵UI配置タイプ
/// </summary>
public enum EnemyUIPlacementType
{
    RandomInArea,    // エリア内ランダム配置
    DirectPosition   // 直接位置指定
}


/// <summary>
///     通常の敵
/// </summary>
[Serializable]
public class NormalEnemy : BaseStates
{
    [SerializeField] BattleAIBrain _brain;

    [Header("復活歩数設定 基本生命キャラにだけ設定して、\n-1だと復活しないです。0だと即復活します。")]
    /// <summary>
    ///     この敵キャラクターの復活する歩数
    ///     手動だが基本的に生命キャラクターのみにこの歩数は設定する? 
    /// </summary>
    public int RecovelySteps = -1;

    /// <summary>
    /// 復活するかどうか
    /// </summary>
    public bool Reborn
    {
        get { return RecovelySteps >= 0; }//復活歩数がゼロ以上なら復活する敵　つまり-1に設定すると復活しない
    }

    [Header("割り込みカウンター有効/無効\n頭のいいキャラなら戦闘時AIAPIで変更する")]
    [SerializeField]
    private bool _interruptCounterActive = true;

    public override bool IsInterruptCounterActive => _interruptCounterActive;


    
    /// <summary>
    /// 実際の再復活カウンター　初回は-1
    /// このカウントは死んだ状態でバトルが終わると、入る　Death()ではまだバトル内で復活する恐れがあるから。
    /// </summary>
    private int _recovelyStepCount = -1;

    /// <summary>
    /// 最後にエンカウントした時の歩数 再復活用 要は敵が死んだとき"のみ"に記録される。
    /// -1なら初回
    /// </summary>
    private int _lastEncountProgressForReborn = -1;
    /// <summary>
    /// 復活歩数をカウント変数にセットする。
    /// </summary>
    public void ReadyRecovelyStep(int globalSteps)
    {
        _lastEncountProgressForReborn = globalSteps;//今回の進行度を保存する。
        _recovelyStepCount = RecovelySteps;
    }

    /// <summary>
    /// "一回死んだ復活可能者"が復活するかどうか　要は今回復活するかまたはそもそも生きてるかどうか
    /// </summary>
    public bool CanRebornWhatHeWill(int globalSteps)
    {
        if (_recovelyStepCount <= 0)
        {
            Debug.Log($"{CharacterName}はrecovelyStepCountがゼロなので復活可能です。");
            return true;//既にカウントがゼロなら生きてるってこと。　復活カウントもせずに返す
        }

        var distanceTraveled = Math.Abs(globalSteps - _lastEncountProgressForReborn);//差の絶対値 = 移動距離 を取得

        if((_recovelyStepCount -= distanceTraveled) <= 0)//復活までのカウントから(前エリアと今の進行度の差)を引いて0以下になったら
        {
            _recovelyStepCount = 0;//逃げてもまた出てくる　殺さないとまたrecovelyStepは設定されない。
            OnReborn();//復活コールバック
            return true;//復活する。
        }

        _lastEncountProgressForReborn = globalSteps;//もしカウントが終わってなかったら今回の進行度を保存する

        return false;
    }
    /// <summary>
    /// 最後にエンカウントした時の歩数  会ってさえいればとりあえず記録 -1なら初回
    /// </summary>
    private int _lastEncounterProgress = -1;

    
    /// <summary>
    /// 敵の実体スキルリスト
    /// </summary>
    [SerializeReference,SelectableSerializeReference] List<EnemySkill> EnemySkillList = new();
    /// <summary>
    /// 敵は成長ポイントが-1に指定されたスキルのみ、実体スキルリストとして返す
    /// </summary>
    public override IReadOnlyList<BaseSkill> SkillList => EnemySkillList.Where(skill => skill.growthPoint <= -1).ToList();
    /// <summary>
    /// まだ有効化されてないスキルのリスト
    /// </summary>
    public List<EnemySkill> SkillListNotEnabled => EnemySkillList.Where(skill => skill.growthPoint > -1).ToList();
    /// <summary>
    /// 全スキルの印象構造の十日能力値の総和
    /// </summary>
    float AllSkillTenDays => SkillList.Sum(s => s.TenDayValuesSum);
    /// <summary>
    /// 全スキルの印象構造の十日能力値の平均値
    /// </summary>
    float AverageSkillTenDays => AllSkillTenDays / SkillList.Count;
    
    /// <summary>
    /// スキルとキャラステータスなど初期化
    /// </summary>
    public override void OnInitializeSkillsAndChara()
    {
        EnsureBaseTenDayValues();
        if (NowUseWeapon == null && WeaponManager.Instance != null)
        {
            ApplyWeapon(InitWeaponID);
        }
        foreach (var skill in EnemySkillList)
        {
            skill.OnInitialize(this);
        }
        if(RecovelySteps <= 0)
        {
            if(RecovelySteps != -1) 
            {
                Debug.LogWarning($"敵キャラの復活歩数が0以下であり、-1でないので、倒しても一発で復活します。\n設定は合ってますか？{CharacterName}");
            }
        }
    }    
    /// <summary>
    /// 敵キャラクターはAttackCharaにてこの関数を通じてNowUseSkillを決める
    /// スキル実行の際に選択可能なオプションがあればここで決める
    /// </summary>
    public virtual void SkillAI()
    {
        _brain.SkillActRun();
    }
    public virtual async void BattleEndSkillAI()
    {
        await _brain.PostBattleActRun(this);
    }

    //スキル成長の処理など------------------------------------------------------------------------------------------------------スキル成長の処理などーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーー
    /// <summary>
    /// 成長した十日能力とそれに近い順に成長予定スキルを並び替えたリストを返す。
    /// </summary>
    /// <param name="GrowTenDays">成長した十日能力</param>
    List<EnemySkill> GetGrowSkillsSortedByDistance(TenDayAbilityDictionary GrowTenDays)
    {
        var result = new List<EnemySkill>();

        //成長予定スキル
        var growSkills = new List<EnemySkill>(SkillListNotEnabled);

        //今回成長した十日能力に各成長予定スキルの距離を計算し、その近い順に成長予定スキルのリストを並び替える。
        growSkills.Sort((s1, s2) =>//sort関数は基本照準ソートのイメージ　小さい方をインデクスの小さい方に移す。
        {//負数なら左辺は右辺より小さいので左辺が上にいき、右辺が大きいから下に行く感じ。　その照準ソートを逆にしたいならReturnのcomParetoを逆にするって感じ
        //正数ならs1のがでかいからs2が小さいからs2が上に行く　昇順ソートならこれで小さい順に並ぶ。

            // 各スキルと成長した十日能力との距離を計算
            float distance1 = CalculateTenDaysDistance(s1.TenDayValues(), GrowTenDays);
            float distance2 = CalculateTenDaysDistance(s2.TenDayValues(), GrowTenDays);
            return distance1.CompareTo(distance2);//CompareToに渡した物より大きいなら正の値が返り、小さければ負の値が返るってイメージ。
            //dis1主体で比較してんだからdis1の方が多ければ正の値、当然だよなって感じ。
        });
        
        return growSkills;
    }
    /// <summary>
    /// 有効化されてないスキルたちが成長する処理
    /// この処理の前に行われた勝利時ブーストで成長した十日能力が倍化したこと前提
    /// </summary>
    void GrowSkillsNotEnabledOnWin()
    {
        //成長した十日能力
        var growTenDays = new TenDayAbilityDictionary(battleGain);
        //成長した十日能力に近い順に並び替えた有効化されてないスキルのリスト
        var growSkillsSortedByNearGrowTenDays = GetGrowSkillsSortedByDistance(growTenDays);
        

        //近い順88%が成長する
        int availableWin = growSkillsSortedByNearGrowTenDays.Count;
        if (availableWin == 0) return; // 対象が無ければ処理なし
        int growSkillCount = Mathf.Max(1, Mathf.FloorToInt(availableWin * 0.88f));
        growSkillCount = Mathf.Min(growSkillCount, availableWin);
        var growSkills = growSkillsSortedByNearGrowTenDays.GetRange(0, growSkillCount);//今回成長するスキルたち

        //成長する
        foreach (var growSkill in growSkills)//今回有効化に至る成長をするスキルたち
        {
            //スキルの成長ポイント
            var growPoint = 0f;
            
            foreach (var skillTenDay in growSkill.TenDayValues())//スキルの持つ全ての十日能力
            {
                //成長した十日能力と一致したものならば、乗算して成長ポイントに足す
                if (growTenDays.TryGetValue(skillTenDay.Key, out float growValue))
                {
                    growPoint += skillTenDay.Value * growValue;
                }
            }

            growSkill.growthPoint -= growPoint;//減算
        }
    }
    /// <summary>
    /// 有効化されてないスキルたちが成長する処理
    /// 逃げ出した時の成長処理
    /// </summary>
    void GrowSkillsNotEnabledOnRunOut()
    {
        //成長した十日能力
        var growTenDays = new TenDayAbilityDictionary(battleGain);
        //成長した十日能力に近い順に並び替えた有効化されてないスキルのリスト
        var growSkillsSortedByNearGrowTenDays = GetGrowSkillsSortedByDistance(growTenDays);
        
        //近い順33%が成長する
        int availableRun = growSkillsSortedByNearGrowTenDays.Count;
        if (availableRun == 0) return; // 対象が無ければ処理なし
        int growSkillCount = Mathf.Max(1, Mathf.FloorToInt(availableRun * 0.33f));
        growSkillCount = Mathf.Min(growSkillCount, availableRun);
        //今回成長するスキルたち
        var growSkills = growSkillsSortedByNearGrowTenDays.GetRange(0, growSkillCount);

        //成長量
        var growAmount  = growTenDays.Sum(kvp => kvp.Value) / 5;//成長した十日能力の総量の5分の1

        foreach(var growSkill in growSkills)
        {
            //勝利時と違い、十日能力とスキルの印象構造十日能力の比較がない。
            growSkill.growthPoint -= growAmount;
        }
    }
    /// <summary>
    /// 有効化されてないスキルたちが成長する処理
    /// 主人公たちが逃げ出した時の成長処理
    /// </summary>
    void GrowSkillsNotEnabledOnAllyRunOut()
    {
        //成長した十日能力
        var growTenDays = new TenDayAbilityDictionary(battleGain);
        //成長した十日能力に近い順に並び替えた有効化されてないスキルのリスト
        var growSkillsSortedByNearGrowTenDays = GetGrowSkillsSortedByDistance(growTenDays);
        
        //近い順66%が成長する
        int availableAllyRun = growSkillsSortedByNearGrowTenDays.Count;
        if (availableAllyRun == 0) return; // 対象が無ければ処理なし
        int growSkillCount = Mathf.Max(1, Mathf.CeilToInt(availableAllyRun * 0.66f));
        growSkillCount = Mathf.Min(growSkillCount, availableAllyRun);
        //今回成長するスキルたち
        var growSkills = growSkillsSortedByNearGrowTenDays.GetRange(0, growSkillCount);

        //成長量
        var allGrowTenDays = growTenDays.Sum(kvp => kvp.Value);
        var growAmount  = allGrowTenDays * RandomEx.Shared.NextFloat(1 / 5f, 3 / 5f);//成長した十日能力の総量の5分の1 ~ 5分の3

        foreach(var growSkill in growSkills)
        {
            //勝利時と違い、十日能力とスキルの印象構造十日能力の比較がない。
            growSkill.growthPoint -= growAmount;
        }
    }
    /// <summary>
    /// 有効化されてないスキルたちが成長する処理
    /// 再遭遇時　所謂歩行時
    /// </summary>
    void GrowSkillsNotEnabledOnReEncount(float distanceTraveled)
    {
        //全有効化されてないスキルの中に0の成長ポイントがあれば-1にする。
        foreach(var unLockSkill in SkillListNotEnabled)
        {
            if(unLockSkill.growthPoint == 0)
            {
                unLockSkill.growthPoint = -1;
            }
        }


        //成長するスキルの選別
        var growSkills = new List<EnemySkill>(SkillListNotEnabled);
        growSkills.Shuffle();//中身をシャッフルする。
        //全有効化してないスキルのランダム30%分が今回成長する
        int growSkillCount = Mathf.Max(1, Mathf.CeilToInt(growSkills.Count * 0.3f));
        growSkills = growSkills.Take(growSkillCount).ToList();

        //成長ポイントの算出
        var growPoint = AverageSkillTenDays / 4.2f;//全スキルの印象構造の十日能力値の平均値を 更に割った数
        growPoint *= distanceTraveled;//距離差分分を掛ける
        foreach(var growSkill in growSkills)
        {//成長するスキルを全ての成長ポイントで減算する。
            growSkill.growthPoint -= growPoint;
        }

        
        
    }
    
    //スキル成長------------------------------------------------------------------------------------------------------スキル成長ーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーー

    /// <summary>
    ///初期精神属性決定関数(基本は印象を持ってるスキルリストとデフォルト精神属性から適当に選び出す
    /// </summary>
    public virtual void InitializeMyImpression()
    {
        SpiritualProperty that;

        if (SkillList != null)
        {
            var rndList = SkillList.Select(s => s.SkillSpiritual).ToList();
            rndList.AddRange(new List<SpiritualProperty>{DefaultImpression,DefaultImpression});
            that = RandomEx.Shared.GetItem(rndList.ToArray()); //スキルの精神属性を抽出
            MyImpression = that; //印象にセット
        }
        else
        {
            Debug.Log(CharacterName + " のスキルが空です。");
        }
    }
    void EneVictoryBoost()
    {
        //まず主人公グループと敵グループの強さの倍率(敵視点でね)
        var ratio = manager.AllyGroup.OurTenDayPowerSum(false) / manager.EnemyGroup.OurTenDayPowerSum(false);        
        VictoryBoost(ratio);       
    }
    public void OnWin()
    {
        EneVictoryBoost();//勝利時十日能力ブースト成長
        GrowSkillsNotEnabledOnWin();//敵の非有効化スキル成長　勝利時ブーストの後に行うこと前提
        ResolveDivergentSkillOutcome();//乖離スキル過多使用による苦悩システム　十日能力低下
        HP += MaxHP * 0.15f;//HPの自然回復
    }
    public void OnRunOut()
    {
        //逃げ出した時のスキル成長
        GrowSkillsNotEnabledOnRunOut();
    }
    public void OnAllyRunOut()
    {
        //主人公たちが逃げ出した時のスキル成長
        GrowSkillsNotEnabledOnAllyRunOut();
        HP += MaxHP * 0.3f;//HPの自然回復

    }
    /// <summary>
    /// 再遭遇時コールバックとは違い、復活者限定の処理
    /// </summary>
    public void OnReborn()
    {
        //復活時の処理
        
        Angel();
        Heal(99999999999999);

        //精神HPは戦闘開始時にmaxになる
        //思えの値は死んだらリセットされてるし、パワーは再遭遇時コールバックで歩行変化するから。
    }
    /// <summary>
    /// 再遭遇時コールバック。パッシブとか自信ブーストなどの、
    // 歩行に変化のあるものは敵グループはここら辺で一気に処理をする。
    //敵の初回エンカウント時のコールバックでもある。
    /// </summary>
    public void ReEncountCallback(int globalSteps)
    {
        bool isFirstEncounter = false;
        var distanceTraveled = 0;//距離差分

        //遭遇したら遭遇地点記録
        if(_lastEncounterProgress == -1)
        {//もし初回遭遇なら
            isFirstEncounter = true;
        }else{
            //二回目以降の遭遇なら移動距離を取得
            distanceTraveled = Math.Abs(globalSteps - _lastEncounterProgress);//移動距離取得
        }

        //二回目以降の遭遇の処理
        if(!isFirstEncounter)
        {
            //自信ブーストの再遭遇減衰処理
            //FadeConfidenceBoostByWalking(distanceTraveled);
            //AllyClassの方で整合性を取るために一歩分の関数にした　残りは後で考える。

            //パッシブ歩行効果
            for(var i = 0 ; i < distanceTraveled ; i++)
            {
                AllPassiveWalkEffect();//歩行効果
                UpdateWalkAllPassiveSurvival();//歩行によるパッシブ残存処理
                UpdateAllSkillPassiveWalkSurvival();//スキルパッシブの歩行残存処理
                ResonanceHealingOnWalking();//歩行時思えの値回復
                //RecoverMentalHPOnWalk();//歩行時精神HP回復 基本的に歩行時の精神hp回復はポイント用だし、それ抜きにしたら戦闘開始時に精神HPはmaxになるし。
                //RecoverPointOnWalk();//歩行時ポイント回復 敵には外でスキル使うとかないから、戦闘時のポイント初期化で十分なので歩行回復なし
            }

            //歩行時の有効化されてないスキルの成長処理
            GrowSkillsNotEnabledOnReEncount(distanceTraveled);

        }
        //「精神属性はEnemyCollectAI」　つまり敵グループの収集に必須なので、このコールバックの前の収集関数内で既に決まってます。

        //初回遭遇も含めて全ての遭遇時の処理
        TransitionPowerOnWalkByCharacterImpression();//パワー変化　精神属性で変化するがその精神属性は既に決まっているので

        //遭遇地点を記録する。
        _lastEncounterProgress = globalSteps;

        //死亡判定
            if (Death())//死亡時コールバックも呼ばれる
            {
                if(Reborn && !broken)//復活するタイプであり、壊れてないものだけ
                {
                    ReadyRecovelyStep(globalSteps);//復活歩数準備
                }
            }
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
        clone._interruptCounterActive = this._interruptCounterActive;//割り込みカウンターActive設定。
        foreach(var skill in this.EnemySkillList)
        {
            clone.EnemySkillList.Add(skill.InitEnemyDeepCopy());
        }

        // UI設定をコピー
        clone._uiPlacementType = this._uiPlacementType;
        // グラフィック（スプライト）をコピー（未コピーだとUIが白表示になる）
        clone.EnemyGraphicSprite = this.EnemyGraphicSprite;
        
        // 敵UIオブジェクトのコピー（UIコンポーネントは参照コピー）

        clone._brain = this._brain;            // AIをコピー

        /*clone.broken = this.broken;
        clone._recovelyStepCount = this._recovelyStepCount;
        clone._lastEncountProgressForReborn = this._lastEncountProgressForReborn;
        clone._lastEncounterProgress = this._lastEncounterProgress;*/
        //全て戦闘の時に使われる前提の値だからコメントアウト

        // 4. 戻り値
        return clone;
    }

    /// <summary>
    /// 戦闘UI配置設定
    /// </summary>
    [Header("戦闘UI配置設定")]
    [SerializeField] private EnemyUIPlacementType _uiPlacementType = EnemyUIPlacementType.RandomInArea;
    
    
    [Header("敵UIオブジェクト設定")]
    /// <summary>
    /// 敵グラフィック画像（メインの敵画像）
    /// </summary>
    public Sprite EnemyGraphicSprite;
    /// <summary>
    /// UI配置タイプ
    /// </summary>
    public EnemyUIPlacementType UIPlacementType => _uiPlacementType;
}

[Serializable]
public class EnemySkill : BaseSkill
{
    /// <summary>
    /// このスキルが有効化されるかどうかの成長ポイント
    /// 0以下になったら有効化されてるとみなす。 -1が有効化状態
    /// </summary>
    public float growthPoint = -1;
    /// <summary>
    /// スキルの成長ポイントを減算する。
    /// </summary>
    public void EnemySkillGrowthProgress(float growthPoint)
    {
        this.growthPoint -= growthPoint;

        if(this.growthPoint <= 0)//0以下になったら有効化
        {
            //有効化
            this.growthPoint = -1;
        }
    }

    /// <summary>
    /// 敵は初期スキルレベルを指定可能。
    /// つまり敵は使用回数に関係なく最初からスキルのレベルが上がってることを表現できるし、
    /// その上でちゃんと使用回数でも成長する。
    /// → obsidian スキルレベルの敵のスキルレベルについての章を読んで
    /// </summary>
    [SerializeField]
    int _initSkillLevel;
    protected override int _nowSkillLevel
    {
        get
        {
            if(IsTLOA)
            {
                return _recordDoCount / TLOA_LEVEL_DIVIDER + _initSkillLevel;
            }
            else
            {
                return _recordDoCount / NOT_TLOA_LEVEL_DIVIDER + _initSkillLevel;
            }
        }
    }


    public EnemySkill InitEnemyDeepCopy()
    {
        var clone = new EnemySkill();
        InitDeepCopy(clone);
        clone.growthPoint = growthPoint;
        clone._initSkillLevel = _initSkillLevel;
        return clone;
    }
}
