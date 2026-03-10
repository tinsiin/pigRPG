using System;
using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using UnityEngine;
using UnityEngine.Serialization;
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
    [SerializeField] private GrowthSettings _growthSettings;

    // AI用戦闘記憶（個体ごとに保持、ランタイム専用）
    [NonSerialized] private BattleMemory _aiMemory;
    public override BattleMemory AIMemory => _aiMemory ??= new BattleMemory();

    // 相性値変動の永続化（個体ごと、ランタイム専用）
    // キー = 相手のEnemyGuid, 値 = CSV基礎値からの累積変動量（+/-）
    [NonSerialized] private Dictionary<string, int> _bondDeltas = new();

    public void RecordBondDelta(string otherGuid, int delta)
    {
        if (string.IsNullOrEmpty(otherGuid) || delta == 0) return;
        _bondDeltas.TryGetValue(otherGuid, out var current);
        _bondDeltas[otherGuid] = current + delta;
    }

    public int GetBondDelta(string otherGuid)
    {
        if (string.IsNullOrEmpty(otherGuid)) return 0;
        _bondDeltas.TryGetValue(otherGuid, out var delta);
        return delta;
    }

    private readonly Dictionary<GrowthStrategyType, IGrowthStrategy> _growthStrategies = new();

    /// <summary>直前のReEncountCallbackで計算されたdistanceTraveled。パッシブ蓄積用。</summary>
    private int _lastDistanceTraveled;
    public int LastDistanceTraveled => _lastDistanceTraveled;

    /// <summary>
    /// 個体識別用GUID。ランタイムコピー時に自動発行。友情コンビ登録で個体を追跡するために使用。
    /// </summary>
    private string _enemyGuid;
    public string EnemyGuid
    {
        get
        {
            if (string.IsNullOrEmpty(_enemyGuid))
                _enemyGuid = System.Guid.NewGuid().ToString();
            return _enemyGuid;
        }
    }

    /// <summary>
    /// 外部からGUIDを復元する（セーブデータからのロード時）
    /// </summary>
    public void RestoreGuid(string guid)
    {
        if (!string.IsNullOrEmpty(guid))
            _enemyGuid = guid;
    }

    [Header("復活歩数設定 基本生命キャラにだけ設定して、\n-1だと復活しないです。0だと即復活します。")]
    [FormerlySerializedAs("RecovelySteps")]
    /// <summary>
    ///     この敵キャラクターの復活する歩数
    ///     手動だが基本的に生命キャラクターのみにこの歩数は設定する?
    /// </summary>
    public int RebornSteps = -1;

    /// <summary>
    /// 復活するかどうか
    /// </summary>
    public bool Reborn
    {
        get { return RebornSteps >= 0; }//復活歩数がゼロ以上なら復活する敵　つまり-1に設定すると復活しない
    }

    [Header("割り込みカウンター有効/無効\n頭のいいキャラなら戦闘時AIAPIで変更する")]
    [SerializeField]
    private bool _interruptCounterActive = true;

    public override bool IsInterruptCounterActive => _interruptCounterActive;


    
    /// <summary>
    /// 復活歩数をカウント変数にセットする。
    /// </summary>
    /// <param name="globalSteps">グローバルステップ数</param>
    /// <param name="rebornManager">復活マネージャ（null時はEnemyRebornManager.Instance）</param>
    public void PrepareReborn(int globalSteps, IEnemyRebornManager rebornManager = null)
    {
        var manager = rebornManager ?? EnemyRebornManager.Instance;
        manager.PrepareReborn(this, globalSteps);
    }

    /// <summary>
    /// "一回死んだ復活可能者"が復活するかどうか　要は今回復活するかまたはそもそも生きてるかどうか
    /// </summary>
    /// <param name="globalSteps">グローバルステップ数</param>
    /// <param name="rebornManager">復活マネージャ（null時はEnemyRebornManager.Instance）</param>
    public bool CanRebornWhatHeWill(int globalSteps, IEnemyRebornManager rebornManager = null)
    {
        var manager = rebornManager ?? EnemyRebornManager.Instance;
        return manager.CanReborn(this, globalSteps);
    }
    /// <summary>
    /// 最後にエンカウントした時の歩数  会ってさえいればとりあえず記録 -1なら初回
    /// </summary>
    private int _lastEncounterProgress = -1;

    /// <summary>
    /// 復活時にエンカウント記録をリセットする。
    /// Reborn復活用: 死亡期間の距離を無効化し、distanceTraveled=0で再出発させる。
    /// </summary>
    public void ResetEncounterProgress(int globalSteps)
    {
        _lastEncounterProgress = globalSteps;
    }

    
    /// <summary>
    /// 敵の実体スキルリスト
    /// </summary>
    [SerializeReference,SelectableSerializeReference] List<EnemySkill> EnemySkillList = new();
    /// <summary>
    /// 敵は成長ポイントが-1に指定されたスキルのみ、実体スキルリストとして返す
    /// </summary>
    private List<BaseSkill> _skillListCache;
    private bool _skillListDirty = true;
    public override IReadOnlyList<BaseSkill> SkillList
    {
        get
        {
            if (_skillListDirty || _skillListCache == null)
            {
                _skillListCache = EnemySkillList.Where(skill => skill.growthPoint <= -1).Cast<BaseSkill>().ToList();
                _skillListDirty = false;
            }
            return _skillListCache;
        }
    }
    /// <summary>
    /// スキルリストキャッシュを無効化する（growthPoint変更後に呼ぶ）
    /// </summary>
    public void InvalidateSkillListCache() => _skillListDirty = true;
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
        if(RebornSteps <= 0)
        {
            if(RebornSteps != -1)
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
        _brain.SkillActRun(manager);
    }

    public void BindBrainContext(IBattleContext context)
    {
        _brain?.BindBattleContext(context);
    }
    public virtual async UniTaskVoid BattleEndSkillAI()
    {
        try
        {
            await _brain.PostBattleActRun(this, manager);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    //スキル成長の処理など------------------------------------------------------------------------------------------------------スキル成長の処理などーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーー
    private GrowthSettings ResolvedGrowthSettings => _growthSettings != null ? _growthSettings : GrowthSettings.Default;

    private void EnsureGrowthStrategies()
    {
        if (_growthStrategies.Count > 0) return;
        _growthStrategies[GrowthStrategyType.Win] = new WinGrowthStrategy();
        _growthStrategies[GrowthStrategyType.RunOut] = new RunOutGrowthStrategy();
        _growthStrategies[GrowthStrategyType.AllyRunOut] = new AllyRunOutGrowthStrategy();
        _growthStrategies[GrowthStrategyType.ReEncount] = new ReEncountGrowthStrategy();
    }

    private void ApplyGrowth(GrowthStrategyType type, int distanceTraveled = 0)
    {
        EnsureGrowthStrategies();
        if (!_growthStrategies.TryGetValue(type, out var strategy)) return;
        var random = manager?.Random ?? new SystemBattleRandom();
        var context = new EnemyGrowthContext(
            this,
            ResolvedGrowthSettings,
            new TenDayAbilityDictionary(battleGain),
            SkillListNotEnabled,
            AverageSkillTenDays,
            distanceTraveled,
            random);
        strategy.Apply(context);
        InvalidateSkillListCache();
    }

    public virtual void InitializeMyImpression()
    {
        SpiritualProperty that;

        if (SkillList != null)
        {
            var rndList = SkillList.Select(s => s.SkillSpiritual).ToList();
            rndList.AddRange(new List<SpiritualProperty>{DefaultImpression,DefaultImpression});
            var random = manager?.Random ?? new SystemBattleRandom();
            that = random.GetItem(rndList); //スキルの精神属性を抽出
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
        var ratio = manager.AllyGroup.OurTenDayPowerSum() / manager.EnemyGroup.OurTenDayPowerSum();
        VictoryBoost(ratio);       
    }
    public void OnWin()
    {
        EneVictoryBoost();//勝利時十日能力ブースト成長
        ApplyGrowth(GrowthStrategyType.Win);//敵の非有効化スキル成長　勝利時ブーストの後に行うこと前提
        ResolveDivergentSkillOutcome();//乖離スキル過多使用による苦悩システム　十日能力低下
        HP += MaxHP * 0.15f;//HPの自然回復
    }
    public void OnRunOut()
    {
        //逃げ出した時のスキル成長
        ApplyGrowth(GrowthStrategyType.RunOut);
    }
    public void OnAllyRunOut()
    {
        //主人公たちが逃げ出した時のスキル成長
        ApplyGrowth(GrowthStrategyType.AllyRunOut);
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
    /// 再エンカウント時のHP自然回復。
    /// 歩数が多いほど回復の上限が上がるが、実際の回復量はランダム。
    /// </summary>
    const int FullRecoverySteps = 200;
    void NaturalHPRecovery(int distanceTraveled)
    {
        if(Death()) return;
        var recoveryRatio = Mathf.Min((float)distanceTraveled / FullRecoverySteps, 1f);
        var random = manager?.Random ?? new SystemBattleRandom();

        // 友情コンビなら回復の下限が上がる
        var lowerBound = 0f;
        var combo = GameContextHub.Current?.ComboRegistry?.FindComboByMemberGuid(EnemyGuid);
        if (combo != null)
            lowerBound = combo.LowerRatio * recoveryRatio;

        var heal = MaxHP * random.NextFloat(lowerBound, recoveryRatio);
        HP += heal;
    }

    /// <summary>
    /// 友情コンビのパッシブ蓄積。相方の味方向けパッシブ付与スキルを全て適用し、
    /// simulatedSteps分の歩行消化を新規パッシブのみに実行する。
    /// 既存の歩行消化（減らす側）と合わせてパッシブの新陳代謝を実現する。
    /// </summary>
    public void AccumulateComboPassives(NormalEnemy partner, int distanceTraveled)
    {
        if (partner == null || distanceTraveled <= 0 || Death()) return;

        var combo = GameContextHub.Current?.ComboRegistry?.FindComboByMemberGuid(EnemyGuid);
        if (combo == null) return;

        var provider = PassiveProvider ?? PassiveManager.Instance;
        if (provider == null) return;

        // 既存パッシブIDのスナップショット（新規判定用。重ね掛けは新規扱いしない）
        var existingIds = new HashSet<int>();
        foreach (var p in Passives) existingIds.Add(p.ID);

        // 相方のスキルから対象パッシブを列挙・付与
        foreach (var skill in partner.SkillList)
        {
            if (skill == null) continue;
            if (!skill.HasType(SkillType.addPassive)) continue;
            if (!skill.HasZoneTraitAny(SkillZoneTrait.SelectOnlyAlly, SkillZoneTrait.SelfSkill)) continue;

            foreach (var passiveId in skill.SubEffects)
            {
                var template = provider.GetAtID(passiveId);
                if (template == null || template.IsBad) continue;
                ApplyPassiveByID(passiveId, partner);
            }
        }

        // 新規パッシブを特定（重ね掛けされた既存パッシブは除外）
        var newPassives = new List<BasePassive>();
        foreach (var p in Passives)
        {
            if (!existingIds.Contains(p.ID))
                newPassives.Add(p);
        }
        if (newPassives.Count == 0) return;

        // simulatedSteps: 「相方がsimulatedSteps前にパッシブをかけた」前提
        var random = manager?.Random ?? new SystemBattleRandom();
        var lowerBound = (int)(combo.LowerRatio * distanceTraveled);
        var simulatedSteps = random.NextInt(lowerBound, distanceTraveled + 1);

        // 新規パッシブのみの歩行消化ループ
        for (var step = 0; step < simulatedSteps; step++)
        {
            foreach (var pas in newPassives.ToArray())
            {
                if (!Passives.Contains(pas)) continue;
                if (pas.DurationWalkCounter > 0)
                    pas.WalkEffect();
                pas.UpdateWalkSurvival(this);
            }
            newPassives.RemoveAll(p => !Passives.Contains(p));
            if (newPassives.Count == 0) break;
            if (Death()) break; // パッシブ蓄積中に死亡したらループ脱出
        }
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
        _lastDistanceTraveled = 0;

        //遭遇したら遭遇地点記録
        if(_lastEncounterProgress == -1)
        {//もし初回遭遇なら
            isFirstEncounter = true;
        }else{
            //二回目以降の遭遇なら移動距離を取得
            distanceTraveled = Math.Abs(globalSteps - _lastEncounterProgress);//移動距離取得
            _lastDistanceTraveled = distanceTraveled;
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
                if (Death()) break; // パッシブ致死: 死亡したらループ脱出
            }

            //歩行時の有効化されてないスキルの成長処理
            if (!Death()) ApplyGrowth(GrowthStrategyType.ReEncount, distanceTraveled);

            //HP自然回復（歩数ベース）
            NaturalHPRecovery(distanceTraveled);
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
                    PrepareReborn(globalSteps);//復活歩数準備
                }
            }
    }

    /// <summary>
    /// パッシブ致死時に、致死の手柄となるパッシブを返す。
    /// _grantor が設定されているパッシブからランダム選出（IsBad優先）。
    /// </summary>
    public BasePassive FindKillerPassive()
    {
        var withGrantor = Passives.FindAll(p => p._grantor != null);
        if (withGrantor.Count == 0) return null;

        var bad = withGrantor.FindAll(p => p.IsBad);
        var pool = bad.Count > 0 ? bad : withGrantor;
        return pool[RandomExtensions.RandomEx.Shared.NextInt(0, pool.Count)];
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
        clone.RebornSteps = this.RebornSteps;
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

        // 新個体として新しいGUIDを発行（テンプレートのGUIDは引き継がない）
        // _bondDeltas は意図的にコピーしない（新個体は空の関係性から始まる。GUIDも新規なので旧データは参照不可）
        clone._enemyGuid = System.Guid.NewGuid().ToString();

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
            if(IsTloaDirect)
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
