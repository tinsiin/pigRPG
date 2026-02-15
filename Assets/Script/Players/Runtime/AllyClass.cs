using RandomExtensions;
using RandomExtensions.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static CommonCalc;

[Serializable]
public class AllyClass : BaseStates
{
    private BattleOrchestrator _orchestrator;
    private BattleOrchestrator Orchestrator => _orchestrator ?? BattleOrchestratorHub.Current;
    public void InitializeOrchestrator(BattleOrchestrator orchestrator) => _orchestrator = orchestrator;

    /// <summary>
    /// このキャラクターのID
    /// </summary>
    public CharacterId CharacterId { get; private set; }

    /// <summary>
    /// CharacterId を設定する（初期化時に1回のみ呼ぶ）
    /// </summary>
    public void SetCharacterId(CharacterId id)
    {
        CharacterId = id;
    }

    /// <summary>
    /// 【旧システム - 使用されていない】
    /// CharacterDataSOからのインスタンス生成ではこのフィールドは常にnull。
    /// BattleIconUIはCharacterUIRegistry経由でバインドされる。
    /// </summary>
    [SerializeField, HideInInspector] BattleIconUI _uic;

    /// <summary>
    /// 戦闘中に表示するキャラクターアイコンスプライト。
    /// 規格: 476 x 722 px
    /// </summary>
    [Header("バトルアイコン")]
    [Tooltip("戦闘中に表示するキャラクターアイコン（規格: 476 x 722 px）")]
    [SerializeField] private Sprite _battleIconSprite;

    /// <summary>
    /// バトルアイコンスプライトを取得する
    /// </summary>
    public Sprite BattleIconSprite => _battleIconSprite;


    /// <summary>
    /// 主人公達の全所持スキルリスト
    /// </summary>
    [SerializeReference,SelectableSerializeReference]
    List<AllySkill> _skillALLList = new();
    /// <summary>
    /// IDでスキルを取得する
    /// </summary>
    AllySkill SkillByID(int ID)
    {
        return _skillALLList.Find(skill => skill.ID == ID);
    }

    /// <summary>
    /// 有効なスキルリスト
    /// </summary>
    public List<int> ValidSkillIDList = new();
    public override IReadOnlyList<BaseSkill> SkillList => _skillALLList.Where(skill => ValidSkillIDList.Contains(skill.ID)).ToList();
    public IReadOnlyList<AllySkill> AllSkills => _skillALLList;

    public override void OnInitializeSkillsAndChara()
    {
        foreach (var skill in _skillALLList)
        {
            skill.OnInitialize(this);
        }
    }
    /// <summary>
    /// 現在のTLOAの思い入れスキルID
    /// </summary>
    public int EmotionalAttachmentSkillID = 0;
    /// <summary>
    /// 現在のTLOAの思い入れスキル
    /// </summary>
    public AllySkill EmotionalAttachmentSkill => SkillByID(EmotionalAttachmentSkillID);
    /// <summary>
    /// 思い入れスキルの思い入れ量 (0~800)
    /// </summary>
    private float _emotionalAttachmentSkillQuantity = 0;
    /// <summary>
    /// 思い入れスキルの思い入れ量を取得・設定する
    /// </summary>
    public float EmotionalAttachmentSkillQuantity 
    { 
        get => _emotionalAttachmentSkillQuantity;
        set => _emotionalAttachmentSkillQuantity = Mathf.Clamp(value, 0, 800);
    }

    /// <summary>
    /// 思い入れスキルIDを変更する関数
    /// 思い入れスキル変更ボタンUIに渡す用のコールバック
    /// </summary>
    public void OnEmotionalAttachmentSkillIDChange(int NewSkillID)
    {
        var oldSkill = SkillByID(EmotionalAttachmentSkillID);
        var newSkill = SkillByID(NewSkillID);//まず入れ替えの前に

        EmotionalAttachmentSkillID = NewSkillID;//ID変更
        Debug.Log(NewSkillID + " :思い入れスキルIDを記録");

        //思い入れ量の弱体処理
        EmotionalAttachmentSkillQuantityChangeWeakening(oldSkill, newSkill);
        EmotionalAttachmentSkillQuantityChangeSkillWeakening(oldSkill);
    }
    /// <summary>
    /// 思い入れ量から熟練度倍率を計算 (1.2~12倍)
    /// </summary>
    /// <returns>熟練度倍率 (1.2~12)</returns>
    public float GetProficiencyMultiplierFromQuantity()
    {
        // 0~800を0~1に正規化
        float normalizedQuantity = Mathf.Clamp01(EmotionalAttachmentSkillQuantity / 800f);
        
        // 1.2~12の範囲にスケーリング
        return Mathf.Lerp(1.2f, 12f, normalizedQuantity);
    }
    /// <summary>
    /// 思い入れ量から現在HP固定加算倍率を計算 (0.01~0.3)
    /// </summary>
    /// <returns>現在HP固定加算倍率 (0.01~0.3)</returns>
    public float GetCurrentHPFixedAdditionMultiplier()
    {
        // 0~800を0~1に正規化
        float normalizedQuantity = Mathf.Clamp01(EmotionalAttachmentSkillQuantity / 800f);
        
        // 0.01~0.3の範囲にスケーリング
        return Mathf.Lerp(0.01f, 0.3f, normalizedQuantity);
    }
    /// <summary>
    /// 思い入れ量の弱体処理
    /// </summary>
    void EmotionalAttachmentSkillQuantityChangeWeakening(AllySkill oldSkill, AllySkill newSkill)
    {
        if(oldSkill.MotionFlavor == newSkill.MotionFlavor)
        {
            EmotionalAttachmentSkillQuantity *= 0.7f;//動作的雰囲気が一致してたら0.7倍
        }else
        {
            EmotionalAttachmentSkillQuantity = 0;//それ以外は0で最初から
        }
    }
    /// <summary>
    /// 思い入れスキルが変更された際、前に選ばれてたスキルに弱体化スキルパッシブを付与する
    /// 専用付与関数で二つ重複しない
    /// </summary>
    void EmotionalAttachmentSkillQuantityChangeSkillWeakening(AllySkill oldSkill)
    {
        var tuning = Tuning;
        if (tuning == null || tuning.EmotionalAttachmentSkillWeakeningPassiveRef == null)
        {
            Debug.LogError("EmotionalAttachmentSkillQuantityChangeSkillWeakening: Tuning が未設定です");
            return;
        }
        oldSkill.ApplyEmotionalAttachmentSkillQuantityChangeSkillWeakeningPassive//弱体化スキルパッシブ専用の付与関数
        (tuning.EmotionalAttachmentSkillWeakeningPassiveRef, this);
    }
    
    /// <summary>
    /// キャラクターのデフォルト精神属性を決定する関数　十日能力が変動するたびに決まる。
    /// </summary>
    public void DecideDefaultMyImpression()
    {
        //1~4の範囲で合致しきい値が決まる。
        var Threshold = RandomEx.Shared.NextInt(1,5);
        Debug.Log($"{CharacterName}の今回のデフォルト精神属性の合致しきい値:{Threshold}");

        //キャラクターの持つ十日能力を多い順に重み付き抽選リストに入れ、処理をする。
        var AbilityList = new WeightedList<TenDayAbility>();
        //linqで値の多い順にゲット
        foreach(var ability in TenDayValues(false).OrderByDescending(x => x.Value))
        {
            // 重みは整数として扱う前提のため、小数は切り上げし、0はスキップ
            var weight = Mathf.CeilToInt(ability.Value);
            if (weight > 0)
            {
                AbilityList.Add(ability.Key, weight);//キーが十日能力の列挙体　重みの部分に能力の値が入る。
            }
        }
        //Debug.Log($"[DecideDefault] {CharacterName} 重み>0 の十日能力数:{AbilityList.Count}");

        var SpiritualMatchCounts = new Dictionary<SpiritualProperty,int>()//一時保存用データ
        {
            {SpiritualProperty.doremis, 0},
            {SpiritualProperty.pillar, 0},
            {SpiritualProperty.kindergarden, 0},
            {SpiritualProperty.liminalwhitetile, 0},
            {SpiritualProperty.sacrifaith, 0},
            {SpiritualProperty.cquiest, 0},
            {SpiritualProperty.pysco, 0},
            {SpiritualProperty.godtier, 0},
            {SpiritualProperty.baledrival, 0},
            {SpiritualProperty.devil, 0}
        };
        TenDayAbility selectedAbility;//重み付き抽選リストから抜き出す"恐らく多い順に"出てくるであろうキャラクターの十日能力変数
        
        //ここからwhileループ
        var loopCount = 0;
        while(true)
        {
            if(AbilityList.Count <= 0)//十日能力の重み付き抽選リストが空　つまり十日能力がないなら
            {
                DefaultImpression = SpiritualProperty.none;
                Debug.Log($"{CharacterName}のDefaultImpressionが決定:{DefaultImpression}\n(十日能力がないため-DecideDefaultMyImpression)");
                if(loopCount>0)Debug.Log($"十日能力がないわけではなく、尽きるまでにデフォルト精神属性が決まり切らなかったため、noneとなった。\n(DecideDefaultMyImpression)");
                break;
            }
            AbilityList.RemoveRandom(out selectedAbility);//比較用能力値変数に重み付き抽選リストから消しながら抜き出し

            


            foreach(var map in SpritualTenDayAbilitysMap)
            {
                //現在回してる互換表の、「十日能力値の必要合致リスト」の添え字に一時保存している各精神属性の合致数を渡し、必要な十日能力を抜き出す。
                //順序による厳密一致ではなく、「含まれているか」で合致を判定する。合致リストの能力と今回の多い順から数えた能力値が合ってるかを比較。
                if (map.Value.Contains(selectedAbility))
                {
                    SpiritualMatchCounts[map.Key]++; // 合致したら合致数を1増やす
                }
            }

            //合致しきい値を超えた精神属性があるかどうかを確認する。
            //あるならその精神属性のデータをリストにまとめる。
            
            var SpOkList = new List<SpiritualProperty>();

            foreach(var sp in SpiritualMatchCounts)
            {
                if(sp.Value >= Threshold)
                {
                    SpOkList.Add(sp.Key);//超えている精神属性を記録。
                }
            }

            if(SpOkList.Count > 0)
            {
                //複数ダブっても、どれか一つをランダムで選ぶ
                DefaultImpression = RandomEx.Shared.GetItem(SpOkList.ToArray());
                Debug.Log($"{CharacterName}の今回のデフォルト精神属性が決定:{DefaultImpression}");
                break;
            }
            loopCount++;
        }
        

    }
    
    /// <summary>
    /// 隙だらけ補正
    /// 攻撃相手のターゲット率を引数に入れ、それに自分の十日能力補正を掛ける形で命中パーセンテージ補正を算出する
    /// 0未満なら使われない。
    /// </summary>
    public float GetExposureAccuracyPercentageBonus(float EneTargetProbability)
    {
        if(NowPower == ThePower.lowlow)return -1f;//パワーがたるい　だとそもそも発生しない。

        //ジョー歯÷4による基礎能力係数
        var BaseCoefficient = TenDayValues(false).GetValueOrZero(TenDayAbility.JoeTeeth) / 4;
        //馬鹿と烈火の乗算　された補正要素
        var BakaBlazeFireCoef = TenDayValues(false).GetValueOrZero(TenDayAbility.Baka) * 
        TenDayValues(false).GetValueOrZero(TenDayAbility.BlazingFire) / 30;
        //レインコートによる補正要素
        var RaincoatCoef = TenDayValues(false).GetValueOrZero(TenDayAbility.Raincoat) / 20;

        //レインコートと馬鹿烈火補正はパワーによって分岐
        switch(NowPower)
        {
            case ThePower.low://低いとなし
                BakaBlazeFireCoef = 0;
                RaincoatCoef = 0;
                break;
            case ThePower.medium://普通なら0.5倍
                BakaBlazeFireCoef *= 0.5f;
                RaincoatCoef *= 0.5f;
                break;
        }

        var finalTenDaysCoef = BaseCoefficient ;//最終的な十日能力補正にまず基礎の係数を

        //精神属性で分岐する
        switch(MyImpression)
        {
            case SpiritualProperty.pysco://サイコパス、キンダー、リーミナルホワイトはレインコート
            case SpiritualProperty.liminalwhitetile:
            case SpiritualProperty.kindergarden:
                finalTenDaysCoef += RaincoatCoef;
                break;
            case SpiritualProperty.doremis://ドレミスは二つとも
                finalTenDaysCoef += BakaBlazeFireCoef + RaincoatCoef;
                break;
            case SpiritualProperty.pillar:
            case SpiritualProperty.none:
                //加算なし
                break;
            default:
                //それ以外の精神属性は馬鹿烈火補正
                finalTenDaysCoef += BakaBlazeFireCoef;
                break;
        }

        //最終的な十日能力補正をターゲット率÷10 と掛ける　
        var rawModifier = finalTenDaysCoef * (EneTargetProbability / 10) / 10;

        //正か負かによって　1.〇倍か、-1.〇倍かにする
        //eyeModifier = eyeModifier > 0 ? 1 + eyeModifier :  eyeModifier - 1;
        //return eyeModifier * 0.67f;

        // 累乗 0.5（平方根）で増加に減衰をかけ、最低１倍を保証
        var finalModifier = Mathf.Max(1f, Mathf.Pow(rawModifier, 0.5f));
        return finalModifier;

    }
    
    /// <summary>
    /// スキルボタンからそのスキルの範囲や対象者の画面に移る
    /// </summary>
    /// <param name="skillListIndex"></param>
    public void OnSkillBtnCallBack(int skillListIndex)
    {
        var skill = SkillList[skillListIndex];
        var orchestrator = Orchestrator;
        if (orchestrator == null)
        {
            Debug.LogError("[CRITICAL] AllyClass.OnSkillBtnCallBack: BattleOrchestrator is not initialized");
            return;
        }

        var input = new ActionInput
        {
            Kind = ActionInputKind.SkillSelect,
            RequestId = orchestrator.CurrentChoiceRequest.RequestId,
            Actor = this,
            Skill = skill
        };
        var state = orchestrator.ApplyInput(input);
        var uiBridge = BattleUIBridge.Active;
        if (uiBridge != null)
        {
            uiBridge.SetUserUiState(state, false);
        }
        else
        {
            Debug.LogError("PlayersStates.OnSkillBtnCallBack: BattleUIBridge が null です");
        }
    }
    /// <summary>
    /// 武器スキルボタンから装備中の武器スキルを発動する
    /// </summary>
    public void OnWeaponSkillBtnCallBack()
    {
        var weapon = NowUseWeapon;
        if (weapon?.WeaponSkill == null)
        {
            Debug.LogWarning("AllyClass.OnWeaponSkillBtnCallBack: 武器スキルが設定されていません");
            return;
        }

        var orchestrator = Orchestrator;
        if (orchestrator == null)
        {
            Debug.LogError("[CRITICAL] AllyClass.OnWeaponSkillBtnCallBack: BattleOrchestrator is not initialized");
            return;
        }

        var input = new ActionInput
        {
            Kind = ActionInputKind.SkillSelect,
            RequestId = orchestrator.CurrentChoiceRequest.RequestId,
            Actor = this,
            Skill = weapon.WeaponSkill
        };
        var state = orchestrator.ApplyInput(input);
        var uiBridge = BattleUIBridge.Active;
        if (uiBridge != null)
        {
            uiBridge.SetUserUiState(state, false);
        }
        else
        {
            Debug.LogError("AllyClass.OnWeaponSkillBtnCallBack: BattleUIBridge が null です");
        }
    }
    /// <summary>
    /// スキル攻撃回数ストックボタンからはそのまま次のターンへ移行する(対象者選択や範囲選択などはない。)
    /// </summary>
    /// <param name="skillListIndex"></param>
    public void OnSkillStockBtnCallBack(int skillListIndex)
    {
        var skill = SkillList[skillListIndex];
        var orchestrator = Orchestrator;
        if (orchestrator == null)
        {
            Debug.LogError("[CRITICAL] AllyClass.OnSkillStockBtnCallBack: BattleOrchestrator is not initialized");
            return;
        }

        var input = new ActionInput
        {
            Kind = ActionInputKind.StockSkill,
            RequestId = orchestrator.CurrentChoiceRequest.RequestId,
            Actor = this,
            Skill = skill
        };
        var state = orchestrator.ApplyInput(input);
        var inputBridge = BattleUIBridge.Active;
        if (inputBridge != null)
        {
            inputBridge.SetUserUiState(state, false);
        }
        else
        {
            Debug.LogError("PlayersStates.OnSkillStockBtnCallBack: BattleUIBridge が null です");
        }
    }

    /// <summary>
    /// 前のめり（実行時）を選択できるスキルで選択したときのコールバック関数
    /// </summary>
    public void OnSkillSelectAggressiveCommitBtnCallBack(int toggleIndex, int skillID)
    {
        var skill = SkillList[skillID];
        if (!skill.AggressiveOnExecute.canSelect) return;
        skill.AggressiveOnExecute.isAggressiveCommit = (toggleIndex == 0);
        Debug.Log(toggleIndex == 0 ? "前のめりして攻撃する" : "そのままの位置から攻撃");
    }

    /// <summary>
    /// 前のめり（トリガー時）を選択できるスキルで選択したときのコールバック関数
    /// </summary>
    public void OnSkillAggressiveTriggerBtnCallBack(int toggleIndex, int skillID)
    {
        var skill = SkillList[skillID];
        if (!skill.AggressiveOnTrigger.canSelect) return;
        skill.AggressiveOnTrigger.isAggressiveCommit = (toggleIndex == 0);
        Debug.Log(toggleIndex == 0 ? "トリガー中に前のめりする" : "トリガー中はそのまま");
    }

    /// <summary>
    /// 前のめり（ストック時）を選択できるスキルで選択したときのコールバック関数
    /// </summary>
    public void OnSkillAggressiveStockBtnCallBack(int toggleIndex, int skillID)
    {
        var skill = SkillList[skillID];
        if (!skill.AggressiveOnStock.canSelect) return;
        skill.AggressiveOnStock.isAggressiveCommit = (toggleIndex == 0);
        Debug.Log(toggleIndex == 0 ? "ストック中に前のめりする" : "ストック中はそのまま");
    }
    /// <summary>
    /// UI処理用の割り込みカウンターのオンオフのbool
    /// </summary>
    bool IsInterruptCounterActive_UI = true;
    /// <summary>
    /// AllyClassの割り込みカウンターのオンオフのbool
    /// UI処理boolを受け取るために継承
    /// </summary>
    public override bool IsInterruptCounterActive => IsInterruptCounterActive_UI;
    /// <summary>
    /// キャラの標準のロジックの割り込みカウンターを行うかどうかのラジオボタン選択時のコールバック関数
    /// </summary>
    /// <param name="toggleIndex"></param>
    public void OnSelectInterruptCounterActiveBtnCallBack(int toggleIndex)
    {
        //割り込みカウンターをする
        if(toggleIndex == 0)
        {
            IsInterruptCounterActive_UI = true;
        }//しない
        else
        {
            IsInterruptCounterActive_UI = false;
        }
    }    
    /// <summary>
    /// 直接ボタンを押して、何もしないを選ぶボタンのコールバック
    /// </summary>
    public void OnSkillDoNothingBtnCallBack()
    {
        Debug.Log("何もしない");
        var orchestrator = Orchestrator;
        if (orchestrator != null)
        {
            var input = new ActionInput
            {
                Kind = ActionInputKind.DoNothing,
                RequestId = orchestrator.CurrentChoiceRequest.RequestId,
                Actor = this
            };
            var state = orchestrator.ApplyInput(input);
            var inputBridge = BattleUIBridge.Active;
            if (inputBridge != null)
            {
                inputBridge.SetUserUiState(state, false);
            }
            else
            {
                Debug.LogError("PlayersStates.OnSkillDoNothingBtnCallBack: BattleUIBridge が null です");
            }
            return;
        }
        var uiBridge = BattleUIBridge.Active;
        var battle = uiBridge?.BattleContext;
        if (battle != null)
        {
            battle.DoNothing = true;//ACTBranchingで何もしないようにするboolをtrueに。
        }

        if (uiBridge != null)
        {
            uiBridge.SetUserUiState(TabState.NextWait);//CharacterACTBranchingへ
        }
        else
        {
            Debug.LogError("PlayersStates.OnSkillDoNothingBtnCallBack: BattleUIBridge が null です");
        }
    }
    /// <summary>
    /// スキルの性質に基づいて、次に遷移すべき画面状態を判定する
    /// TargetingPlanに処理を委譲し、ロジックを一元化
    /// </summary>
    /// <param name="skill">判定対象のスキル</param>
    /// <returns>遷移先のTabState</returns>
    public static TabState DetermineNextUIState(BaseSkill skill)
    {
        if (skill == null) return TabState.NextWait;

        var plan = TargetingPlan.FromSkill(skill);
        return plan.ToTabState();
    }

    // ================================
    // 歩行ストリーク（1歩=+1）。OnWalkCallBackは1歩単位の契約。
    // ================================
    private int _walkCounter = 0;

    /// <summary>
    /// 歩行ストリークのリセット。
    /// </summary>
    public void ResetWalkCounter()
    {
        _walkCounter = 0;
        _walkPointRecoveryCounter = 0;//歩行のポイント回復用カウンターをゼロに
        _walkCountForTransitionToDefaultImpression = 0;//歩行の精神属性変化用カウンターをゼロに
    }


    /// <summary>
    /// 2歩ごとに回復するポイントカウンター
    /// </summary>
    private int _walkPointRecoveryCounter = 0;

    
    public override void OnBattleEndNoArgument()
    {
        base.OnBattleEndNoArgument();
        ResetWalkCounter();
    }
    public override void OnBattleStartNoArgument()
    {
        base.OnBattleStartNoArgument();
        
    }
     /// <summary>
    /// 歩行時にポイントを回復する処理
    /// この回復は外でスキルを使用する際の制限を表現する物。
    /// </summary>
    void RecoverPointOnWalk()
    {
        // 2歩ごとに処理
        _walkPointRecoveryCounter++;
        if (_walkPointRecoveryCounter >= 2)
        {
            _walkPointRecoveryCounter = 0;
            
            // 精神HPがマックスであることを前提に回復
            if (MentalHP >= MentalMaxHP)
            {
                // ポイント回復（回復量は調整可能）
                MentalNaturalRecovelyPont();
                
            }
        }
    }
    /// <summary>
    /// 歩行時に精神HPを回復する
    /// </summary>
    void RecoverMentalHPOnWalk()
    {
        if(MentalHP < MentalMaxHP)
        {
            MentalHP += TenDayValues(false).GetValueOrZero(TenDayAbility.Rain) + MentalMaxHP * 0.16f;
        }
        //ポイント回復用で結局は戦闘開始時にmaxになるんだし、こんぐらいの割合で丁度いいと思う
    }
    const int FULL_NEEDED_TRANSITION_TODEFAULTIMPREEION_WALK_COUNT = 12;
    /// <summary>
    /// 歩行時に精神属性をデフォルトに戻す用歩行カウンター変数
    /// </summary>
    int _walkCountForTransitionToDefaultImpression = 0;
    /// <summary>
    /// 精神属性は歩くとデフォルト精神属性に戻っていく処理。
    /// </summary>
    void ImpressionToDefaultTransition()
    {
        //既にデフォルト精神属性なら戻らない
        if(MyImpression == DefaultImpression) return;

        //思えの値の割合
        var ratio = NowResonanceValue / ResonanceValue;
        //必要歩数
        var neededWalkCount = (1-ratio) * FULL_NEEDED_TRANSITION_TODEFAULTIMPREEION_WALK_COUNT;
        //思えの値が削れてる = 思ってるほど、戻りにくい　= 必要歩数が増える

        _walkCountForTransitionToDefaultImpression++;//一歩進んだ
        if(_walkCountForTransitionToDefaultImpression >= neededWalkCount)
        {
            MyImpression = DefaultImpression;
            _walkCountForTransitionToDefaultImpression = 0;
        }
    }


    /// <summary>
    /// 味方キャラの歩く際に呼び出されるコールバック（1歩＝1回の契約）。
    /// 複数歩を処理したい場合は呼び出し側（PlayersOnWalks）で回数分呼ぶこと。
    /// </summary>
    public void OnWalkStepCallBack()
    {
        // 常に1歩分として扱う（契約の一貫性のため）。
        _walkCounter++;
        Debug.Log("歩数: " + _walkCounter);

        // 1. パッシブ/生存/状態遷移（逐次）
        AllPassiveWalkEffect();//全パッシブの歩行効果を呼ぶ
        UpdateWalkAllPassiveSurvival();
        UpdateAllSkillPassiveWalkSurvival();//スキルパッシブの歩行残存処理
        TransitionPowerOnWalkByCharacterImpression();

        // 2. 回復・共鳴（逐次）
        RecoverMentalHPOnWalk();//歩行時精神HP回復
        RecoverPointOnWalk();//歩行時ポイント回復　味方のみ（周期化するなら内部で閾値判定に変更）
        ResonanceHealingOnWalking();//歩行時思えの値回復

        // 3. 属性ポイントの歩行減衰（確率が歩数依存のため逐次）
        ApplyWalkingAttrPDecayStep(_walkCounter);

        // 4. ブーストのフェード（増分1歩）
        FadeConfidenceBoostByWalking();//歩行によって自信ブーストがフェードアウトする

        // 5. 精神属性をデフォルトへ戻す（逐次）
        ImpressionToDefaultTransition();//歩行によって精神属性がデフォルトに戻っていく

    }
    public void OnAllyWinCallBack()
    {
        TransitionPowerOnBattleWinByCharacterImpression();//パワー変化
        HP += MaxHP * 0.3f;//HPの自然回復
        AllyVictoryBoost();//勝利時の十日能力ブースト
        ResolveDivergentSkillOutcome();//乖離スキル使用により、十日能力値減少
    }
    public void OnAllyLostCallBack()
    {
        //敗北時パワー変化
        TransitionPowerOnBattleLostByCharacterImpression();
    }
    public void OnAllyRunOutCallBack()
    {
        //主人公達が逃げ出した時のパワー変化
        TransitionPowerOnBattleRunOutByCharacterImpression();
    }
    /// <summary>
    /// 勝利時の十日能力ブースト倍化処理
    /// </summary>
    public void AllyVictoryBoost()
    {
        //まず主人公グループと敵グループの強さの倍率
        var battle = BattleUIBridge.Active?.BattleContext;
        if (battle == null) return;
        var ratio = battle.EnemyGroup.OurTenDayPowerSum(false) / battle.AllyGroup.OurTenDayPowerSum(false);
        VictoryBoost(ratio);

    }

    

    /// <summary>
    /// キャラクターのパワーが勝利時にどう変化するか
    /// </summary>
    void TransitionPowerOnBattleWinByCharacterImpression()
    {
        switch(MyImpression)
        {
            case SpiritualProperty.doremis:
                switch(NowPower)
                {
                    case ThePower.lowlow:
                        NowPower = ThePower.medium;
                        break;
                }
                break;
            case SpiritualProperty.pillar:
                switch(NowPower)
                {
                    case ThePower.low:
                        NowPower =ThePower.medium;
                        break;
                    default:
                        NowPower = ThePower.high;
                        break;
                }
                break;
            case SpiritualProperty.kindergarden:
                NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium,ThePower.low,ThePower.lowlow,
                ThePower.high, ThePower.medium,ThePower.low,});//←三つは、lowlowの確率を下げるため
                break;
            case SpiritualProperty.liminalwhitetile:
            case SpiritualProperty.sacrifaith:
            case SpiritualProperty.cquiest:
                switch(NowPower)
                {
                    case ThePower.low:
                        NowPower = ThePower.medium;
                        break;
                    case ThePower.lowlow:
                        NowPower = ThePower.low;
                        break;
                }
                break;
            case SpiritualProperty.godtier:
                switch(NowPower)
                {
                    case ThePower.lowlow:
                        NowPower = ThePower.medium;
                        break;
                    default:
                        NowPower = ThePower.high;
                    break;
                }
                break;
            case SpiritualProperty.baledrival:
                NowPower = ThePower.high;
                break;
            case SpiritualProperty.devil:
                switch(NowPower)
                {
                    case ThePower.medium:
                        NowPower = ThePower.high;
                        break;
                    case ThePower.low:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium});
                        break;
                    case ThePower.lowlow:
                        NowPower = ThePower.medium;
                        break;
                }
                break;
        }
    }
    /// <summary>
    /// キャラクターのパワーが負けたときに(死んだときに)変化する関数
    /// </summary>
    void TransitionPowerOnBattleLostByCharacterImpression()
    {
        switch(MyImpression)
        {
            case SpiritualProperty.pillar:
                if (NowPower != ThePower.low)
                {
                    NowPower = ThePower.high;
                }
                break;
            case SpiritualProperty.kindergarden:
                NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium,ThePower.low,ThePower.lowlow,
                ThePower.high, ThePower.medium,ThePower.low,});//←三つは、lowlowの確率を下げるため
                break;
            case SpiritualProperty.liminalwhitetile:
                switch(NowPower)
                {
                    case ThePower.high:
                    case ThePower.medium:
                        NowPower = ThePower.low;
                        break;
                    case ThePower.low:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.lowlow, ThePower.low});
                        break;
                    case ThePower.lowlow:
                        NowPower = ThePower.low;
                    break;
                }
                break;
            case SpiritualProperty.sacrifaith:
                NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium});
                break;
            case SpiritualProperty.cquiest:
                switch(NowPower)
                {
                    case ThePower.high:
                        NowPower = ThePower.lowlow;
                        break;
                }
                break;
            case SpiritualProperty.baledrival:
                NowPower = ThePower.lowlow;
                break;
            case SpiritualProperty.devil:
                switch(NowPower)
                {
                    case ThePower.high:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium});
                        break;
                    case ThePower.medium:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.low, ThePower.medium,ThePower.lowlow});
                        break;
                    case ThePower.low:
                        NowPower = ThePower.lowlow;
                        break;
                    case ThePower.lowlow:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.lowlow});
                        break;
                }
                break;
        }
    }
    /// <summary>
    /// キャラクターのパワーが戦闘から逃げ出したときに変化する関数
    /// </summary>
    void TransitionPowerOnBattleRunOutByCharacterImpression()
    {
        switch(MyImpression)
        {
            case SpiritualProperty.pillar:
                NowPower =ThePower.medium;
                break;
            case SpiritualProperty.kindergarden:
                NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium,ThePower.low,ThePower.lowlow,
                ThePower.high, ThePower.medium,ThePower.low,});//←三つは、lowlowの確率を下げるため
                break;
            case SpiritualProperty.sacrifaith:
                NowPower = ThePower.high;
                break;
            case SpiritualProperty.godtier:
                switch(NowPower)
                {
                    case ThePower.medium:
                        NowPower = ThePower.low;
                        break;
                    case ThePower.low:
                        NowPower = ThePower.lowlow;
                        break;
                }
                break;
            case SpiritualProperty.devil:
                switch(NowPower)
                {
                    case ThePower.medium:
                        NowPower = ThePower.high;
                        break;
                    case ThePower.low:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium});
                        break;
                    case ThePower.lowlow:
                        NowPower = ThePower.medium;
                        break;
                }
                break;
        }
    }
    /// <summary>
    /// AllyClassのディープコピー（新しいインスタンスを返す）
    /// </summary>
    public AllyClass DeepCopy()
    {
        var clone = new AllyClass();
        DeepCopyTo(clone);
        Debug.Log("AllyClassディープコピー完了");
        return clone;
    }

    /// <summary>
    /// AllyClassのディープコピー（既存インスタンスにコピー）
    /// 初期化の処理もawake代わりにここでやろう
    /// </summary>
    /// <param name="dst"></param>
    public void DeepCopyTo(AllyClass dst)
    {
        // BaseStates のフィールドをコピー
        InitBaseStatesDeepCopy(dst);

        // AllyClass 独自フィールドをコピー
        dst._skillALLList = new List<AllySkill>();
        foreach(var skill in _skillALLList)
        {
            dst._skillALLList.Add(skill.InitAllyDeepCopy());
        }
        dst.ValidSkillIDList = new List<int>(ValidSkillIDList);
        dst.EmotionalAttachmentSkillID = EmotionalAttachmentSkillID;
        // BattleIconUIはCharacterUIRegistry経由でバインドされるため、
        // DeepCopyでは何もしない（_uicは旧システムで現在は使用されない）

        // バトルアイコンスプライト（アセット参照なので参照コピー）
        dst._battleIconSprite = _battleIconSprite;
    }
}
