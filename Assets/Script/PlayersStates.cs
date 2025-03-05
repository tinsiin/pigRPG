using RandomExtensions;
using RandomExtensions.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static CommonCalc;
public class ButtonAndSkillIDHold
{
    public Button button;
    public int skillID;
    public void AddButtonFunc(UnityAction<int> call)
    {
        button.onClick.AddListener(() => call(skillID));
    }
}

/// <summary>
///セーブでセーブされるような事柄とかメインループで操作するためのステータス太刀　シングルトン
/// </summary>
public class PlayersStates:MonoBehaviour 
{
    //staticなインスタンス
    public static PlayersStates Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)//シングルトン
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        Init();

    }
    public void Init()
    {
        CreateDecideValues();//中央決定値をゲーム開始時一回だけ生成

        NowProgress = 0;//ステージ関連のステータス初期化
        NowStageID = 0;
        NowAreaID = 0;

        //初期データをランタイム用にセット
        geino = Init_geino.DeepCopy();
        noramlia = Init_noramlia.DeepCopy();
        sites = Init_sites.DeepCopy();

        //スキル初期化
        geino.OnInitializeSkillsAndChara();
        noramlia.OnInitializeSkillsAndChara();
        sites.OnInitializeSkillsAndChara();

        ApplySkillButtons();//ボタンの結びつけ処理

        //初期の各キャラのスキルボタンのみの有効化処理
        //「主人公キャラ達はスキルを最初に全てbaseStatesに持っており、ボタンのみを有効化する」
 
       
    }
    public StairStates Init_geino;
    public BassJackStates Init_noramlia;
    public SateliteProcessStates Init_sites;
    public StairStates geino;
    public BassJackStates noramlia;
    public SateliteProcessStates sites;

    /// <summary>
    /// ボタンと全てのスキルを結びつける。
    /// </summary>
    void ApplySkillButtons()
    {
                //ボタンに「スキルを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillButtonList_geino)
        {
            button.AddButtonFunc(geino.OnSkillBtnCallBack);
        }

        //ストックボタンに「ストックを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillStockButtonList_geino)
        {
            button.AddButtonFunc(geino.OnSkillStockBtnCallBack);
        }

        //ボタンに「スキルを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillButtonList_noramlia)
        {
            button.AddButtonFunc(noramlia.OnSkillBtnCallBack);
        }

        //ストックボタンに「ストックを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillStockButtonList_noramlia)
        {
            button.AddButtonFunc(noramlia.OnSkillStockBtnCallBack);
        }

        //ボタンに「スキルを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillButtonList_sites)
        {
            button.AddButtonFunc(sites.OnSkillBtnCallBack);
        }

        //ストックボタンに「ストックを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillStockButtonList_sites)
        {
            button.AddButtonFunc(sites.OnSkillStockBtnCallBack);
        }


    }

    [SerializeField]
    private List<ButtonAndSkillIDHold> skillButtonList_geino;//ジーノ用スキルボタン用リスト
    [SerializeField]
    private List<ButtonAndSkillIDHold> skillButtonList_noramlia;//ノーマリア用スキルボタン用リスト
    [SerializeField]
    private List<ButtonAndSkillIDHold> skillButtonList_sites;//サテライト用スキルボタン用リスト

    [SerializeField]
    private List<ButtonAndSkillIDHold> skillStockButtonList;//該当のスキルの攻撃ストックボタン用リスト
    [SerializeField]
    private List<ButtonAndSkillIDHold> skillStockButtonList_geino;//ジーノの該当のスキルの攻撃ストックボタン用リスト
    [SerializeField]
    private List<ButtonAndSkillIDHold> skillStockButtonList_noramlia;//ノーマリアの該当のスキルの攻撃ストックボタン用リスト
    [SerializeField]
    private List<ButtonAndSkillIDHold> skillStockButtonList_sites;//サテライトの該当のスキルの攻撃ストックボタン用リスト

    /// <summary>
    /// 指定したZoneTraitとスキル性質を所持するスキルのみを、有効化しそれ以外を無効化するコールバック
    /// </summary>
    public void OnlyInteractHasZoneTraitSkills_geino(SkillZoneTrait trait,SkillType type)
    {
        foreach(var hold in skillButtonList_geino)
        {
            var skill = geino.SkillList[hold.skillID];
            hold.button.interactable = skill.HasZoneTraitAny(trait) && skill.HasType(type);//一つでも持ってればOK
        }
    }
    public void OnlyInteractHasZoneTraitSkills_normalia(SkillZoneTrait trait,SkillType type)
    {
        foreach(var hold in skillButtonList_noramlia)
        {
            var skill = noramlia.SkillList[hold.skillID];
            hold.button.interactable = skill.HasZoneTraitAny(trait) && skill.HasType(type);//一つでも持ってればOK
        }
    }
    public void OnlyInteractHasZoneTraitSkills_sites(SkillZoneTrait trait,SkillType type)
    {
        foreach(var hold in skillButtonList_sites)
        {
            var skill = sites.SkillList[hold.skillID];
            hold.button.interactable = skill.HasZoneTraitAny(trait) && skill.HasType(type);//一つでも持ってればOK
        }
    }




    //連続実行スキル(FreezeConsecutive)の停止予約のボタン
    [SerializeField]
    Button StopFreezeConsecutiveButton_geino;
    [SerializeField]
    Button StopFreezeConsecutiveButton_Bassjack;
    [SerializeField]
    Button StopFreezeConsecutiveButton_Sites;
    public void StarirStopFreezeConsecutiveButtonCallBack()
    {
        geino.TurnOnDeleteMyFreezeConsecutiveFlag();
        StopFreezeConsecutiveButton_geino.gameObject.SetActive(false);
    }
    public void BassJackStopFreezeConsecutiveButtonCallBack()
    {
        noramlia.TurnOnDeleteMyFreezeConsecutiveFlag();
        StopFreezeConsecutiveButton_Bassjack.gameObject.SetActive(false);

    }

    public void SateliteStopFreezeConsecutiveButtonCallBack()
    {
        sites.TurnOnDeleteMyFreezeConsecutiveFlag();
        StopFreezeConsecutiveButton_Sites.gameObject.SetActive(false);
    }
    /// <summary>
    /// FreezeConsecutiveを消去予約するボタンの表示設定
    /// </summary>
    public void VisiableSettingStopFreezeConsecutiveButtons()
    {
        if(!geino.IsDeleteMyFreezeConsecutive)//現在消去予約をしていなくて、
        StopFreezeConsecutiveButton_geino.gameObject.SetActive(geino.IsNeedDeleteMyFreezeConsecutive());//消去予約が可能ならボタンを表示する

        if(!noramlia.IsDeleteMyFreezeConsecutive)//現在消去予約をしていなくて、
        StopFreezeConsecutiveButton_Bassjack.gameObject.SetActive(noramlia.IsNeedDeleteMyFreezeConsecutive());//消去予約が可能ならボタンを表示する


        if(!sites.IsDeleteMyFreezeConsecutive)//現在消去予約をしていなくて、
        StopFreezeConsecutiveButton_Sites.gameObject.SetActive(sites.IsNeedDeleteMyFreezeConsecutive());//消去予約が可能ならボタンを表示する


    }



    /// <summary>
    ///     現在進行度
    /// </summary>
    public int NowProgress { get; private set; }

    /// <summary>
    ///     現在のステージ
    /// </summary>
    public int NowStageID { get; private set; }

    /// <summary>
    ///     現在のステージ内のエリア
    /// </summary>
    public int NowAreaID { get; private set; }

    public BattleGroup GetParty()
    {
        var playerGroup = new List<BaseStates> { geino, sites, noramlia }; //キャラ
        var nowOurImpression = GetPartyImpression(); //パーティー属性を彼らのHPから決定
        var CompatibilityData = new Dictionary<(BaseStates,BaseStates),int>();//相性値のデータ保存用

        // 複数キャラがいる場合のみ、各キャラペアの相性値を計算して格納する
        if (playerGroup.Count >= 2)
        {
            //ストーリー依存
        }

        return new BattleGroup(playerGroup, nowOurImpression,WhichGroup.alliy,CompatibilityData); //パーティー属性を返す
    }

    /// <summary>
    /// 味方のパーティー属性を取得する 現在のHPによって決まる。
    /// </summary>
    /// <returns></returns>
    private PartyProperty GetPartyImpression()
    {
        //それぞれの最大HP５％以内の差は許容し、それを前提に三人が同じHPだった場合
        float toleranceStair = geino.MAXHP * 0.05f;
        float toleranceSateliteProcess = sites.MAXHP * 0.05f;
        float toleranceBassJack = noramlia.MAXHP * 0.05f;
        if (Mathf.Abs(geino.HP - sites.HP) <= toleranceStair && 
            Mathf.Abs(sites.HP - noramlia.HP) <= toleranceSateliteProcess &&
            Mathf.Abs(noramlia.HP - geino.HP) <= toleranceBassJack)
        {
            return PartyProperty.MelaneGroup;//三人のHPはほぼ同じ
        }
        
        
        //HPの差による場合分け
        if (geino.HP >= sites.HP && sites.HP >= noramlia.HP)
        {
            // geino >= sites >= noramlia ステアが一番HPが多い　サテライトプロセスが二番目　バスジャックが三番目
            return PartyProperty.MelaneGroup;
        }
        else if (geino.HP >= noramlia.HP && noramlia.HP >= sites.HP)
        {
            // geino >= noramlia >= sites　ステアが一番HPが多い　ステアが二番目　バスジャックが三番目
            return PartyProperty.Odradeks;
        }
        else if (sites.HP >= geino.HP && geino.HP >= noramlia.HP)
        {
            // sites >= geino >= noramlia　サテライトプロセスが一番HPが多い　ステアが二番目　バスジャックが三番目
            return PartyProperty.MelaneGroup;
        }
        else if (sites.HP >= noramlia.HP && noramlia.HP >= geino.HP)
        {
            // sites >= noramlia >= geino　サテライトプロセスが一番HPが多い　バスジャックが二番目　ステアが三番目
            return PartyProperty.HolyGroup;
        }
        else if (noramlia.HP >= geino.HP && geino.HP >= sites.HP)
        {
            // noramlia >= geino >= sites　バスジャックが一番HPが多い　ステアが二番目　サテライトプロセスが三番目
            return PartyProperty.TrashGroup;
        }
        else if (noramlia.HP >= sites.HP && sites.HP >= geino.HP)
        {
            // noramlia >= sites >= geino　バスジャックが一番HPが多い　サテライトプロセスが二番目　ステアが三番目
            return PartyProperty.Flowerees;
        }

        return PartyProperty.MelaneGroup;//基本的にif文で全てのパターンを網羅しているので、ここには来ない
    }

    /// <summary>
    ///     進行度を増やす
    /// </summary>
    /// <param name="addPoint"></param>
    public void AddProgress(int addPoint)
    {
        NowProgress += addPoint;
    }

    /// <summary>
    ///     現在進行度をゼロにする
    /// </summary>
    public void ProgressReset()
    {
        NowProgress = 0;
    }

    /// <summary>
    ///     エリアをセットする。
    /// </summary>
    public void SetArea(int id)
    {
        NowAreaID = id;
        Debug.Log(id + "をPlayerStatesに記録");
    }
    /// <summary>
    /// 主人公達の勝利時のコールバック
    /// </summary>
    public void PlayersOnWin()
    {
        geino.OnAllyWinCallBack();
        sites.OnAllyWinCallBack();
        noramlia.OnAllyWinCallBack();

    }
    /// <summary>
    /// 主人公達の負けたときのコールバック
    /// </summary>
    public void PlayersOnLost()
    {
        geino.OnAllyLostCallBack();
        sites.OnAllyLostCallBack();
        noramlia.OnAllyLostCallBack();
    }
    /// <summary>
    /// 主人公達の逃げ出した時のコールバック
    /// </summary>
    public void PlayersOnRunOut()
    {
        geino.OnAllyRunOutCallBack();
        sites.OnAllyRunOutCallBack();
        noramlia.OnAllyRunOutCallBack();
    }

    public void PlayersOnWalks(int walkCount)
    {
        geino.OnWalkCallBack(walkCount);
        sites.OnWalkCallBack(walkCount);
        noramlia.OnWalkCallBack(walkCount);
    }
    /// <summary>
    /// 主人公陣の勝利時ブースト
    /// </summary>
    public void PlayersVictoryBoost(float multiplier)
    {
        geino.VictoryBoost(multiplier);
        sites.VictoryBoost(multiplier);
        noramlia.VictoryBoost(multiplier);
    }
    

    //中央決定値など---------------------------------------------------------中央決定値
    /// <summary>
    /// 中央決定値　空洞爆発の値　割り込みカウンター用
    /// </summary>
    public float ExplosionVoid;
    void CreateDecideValues()
    {
        ExplosionVoid = RandomEx.Shared.NextFloat(10,61);
    }
}

public class AllyClass : BaseStates
{
    
    /// <summary>
    /// キャラクターのデフォルト精神属性を決定する関数　十日能力が変動するたびに決まる。
    /// </summary>
    void DecideDefaultMyImpression()
    {
        //1~4の範囲で合致しきい値が決まる。
        var Threshold = RandomEx.Shared.NextInt(1,5);

        //キャラクターの持つ十日能力を多い順に重み付き抽選リストに入れ、処理をする。
        var AbilityList = new WeightedList<TenDayAbility>();
        //linqで値の多い順にゲット
        foreach(var ability in TenDayValues.OrderByDescending(x => x.Value))
        {
            AbilityList.Add(ability.Key,ability.Value);//キーが十日能力の列挙体　重みの部分に能力の値が入る。
        }

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
        while(true)
        {
            if(AbilityList.Count <= 0)//十日能力の重み付き抽選リストが空になったら
            {
                DefaultImpression = SpiritualProperty.none;
                break;
            }
            AbilityList.RemoveRandom(out selectedAbility);//比較用能力値変数に重み付き抽選リストから消しながら抜き出し

            


            foreach(var map in SpritualTenDayAbilitysMap)
            {
                //現在回してる互換表の、「十日能力値の必要合致リスト」の添え字に一時保存している各精神属性の合致数を渡し、必要な十日能力を抜き出す。
                //合致リストの能力と今回の多い順から数えた能力値が合ってるかを比較。
                if(selectedAbility == map.Value[SpiritualMatchCounts[map.Key]])
                {
                    SpiritualMatchCounts[map.Key]++; //合致したら合致数を1増やす
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
                break;
            }
        }
        

    }
    /// <summary>
    /// スキルボタンからそのスキルの範囲や対象者の画面に移る
    /// </summary>
    /// <param name="skillListIndex"></param>
    public void OnSkillBtnCallBack(int skillListIndex)
    {
        NowUseSkill = SkillList[skillListIndex];//使用スキルに代入する
        Debug.Log(SkillList[skillListIndex].SkillName + "を" + CharacterName +" のNowUseSkillにボタンを押して登録しました。");

        //もし先約リストによる単体指定ならば、範囲や対象者選択画面にはいかず、直接actbranchiへ移行
        if(manager.Acts.GetAtSingleTarget(0)!= null)
        {
            Walking.USERUI_state.Value = TabState.NextWait;
        }else
        {
                    //スキルの性質によるボタンの行く先の分岐
            Walking.USERUI_state.Value = DetermineNextUIState(NowUseSkill);
        }

        
    }
        /// <summary>
    /// スキル攻撃回数ストックボタンからはそのまま次のターンへ移行する(対象者選択や範囲選択などはない。)
    /// </summary>
    /// <param name="skillListIndex"></param>
    public void OnSkillStockBtnCallBack(int skillListIndex)
    {
        var skill = SkillList[skillListIndex];
        if(skill.IsFullStock())
        {
            Debug.Log(skill.SkillName + "をストックが満杯。");
            return;//ストックが満杯なら何もしない
        } 
        skill.ATKCountStock();;//該当のスキルをストックする。
        Debug.Log(skill.SkillName + "をストックしました。");

        var list = SkillList.Where((skill,index) => index != skillListIndex && skill.HasConsecutiveType(SkillConsecutiveType.Stockpile)).ToList();
        
        foreach(var stockSkill in list)
        {
            stockSkill.ForgetStock();//今回選んだストックスキル以外のストックが減る。
        }

        Walking.bm.DoNothing = true;//ACTBranchingで何もしないようにするboolをtrueに。

        Walking.USERUI_state.Value = TabState.NextWait;//CharacterACTBranchingへ
        
    }

        /// <summary>
    /// スキルの性質に基づいて、次に遷移すべき画面状態を判定する
    /// </summary>
    /// <param name="skill">判定対象のスキル</param>
    /// <returns>遷移先のTabState</returns>
    public static TabState DetermineNextUIState(BaseSkill skill)
    {
        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectRange))//範囲を選べるのなら
        {
            return TabState.SelectRange;//範囲選択画面へ飛ぶ
        }
        else if (skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget) || 
                skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget) || 
                skill.HasZoneTrait(SkillZoneTrait.CanSelectMultiTarget))//選択できる系なら
        {
            return TabState.SelectTarget;//選択画面へ飛ぶ
        }
        else if (skill.HasZoneTrait(SkillZoneTrait.ControlByThisSituation))
        {
            return TabState.NextWait;//何もないなら事象ボタンへ
        }
        
        return TabState.NextWait; // デフォルトの遷移先
    }
    /// <summary>
    /// ターンをまたぐ連続実行スキル(FreezeConsecutiveの性質持ち)が実行中なのを次回のターンで消す予約をする
    /// </summary>
    public void TurnOnDeleteMyFreezeConsecutiveFlag()
    {
        Debug.Log("TurnOnDeleteMyFreezeConsecutiveFlag を呼び出しました。");
        IsDeleteMyFreezeConsecutive = IsNeedDeleteMyFreezeConsecutive();
    }

    /// <summary>
    /// 2歩ごとに回復するポイントカウンター
    /// </summary>
    private int _walkPointRecoveryCounter = 0;

    /// <summary>
    /// 歩行時にポイントを回復する処理
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
            MentalHP += TenDayValues.GetValueOrZero(TenDayAbility.Rain) + MentalMaxHP * 0.16f;
        }
    }
    
    public override void OnBattleEndNoArgument()
    {
        base.OnBattleEndNoArgument();
        _walkPointRecoveryCounter = 0;//歩行のポイント回復用カウンターをゼロに
    }
    public override void OnBattleStartNoArgument()
    {
        base.OnBattleStartNoArgument();
        
    }

    /// <summary>
    /// 味方キャラの歩く際に呼び出されるコールバック
    /// </summary>
    public void OnWalkCallBack(int walkCount)
    {
        AllPassiveWalkEffect();//全パッシブの歩行効果を呼ぶ
        UpdateWalkAllPassiveSurvival();
        TransitionPowerOnWalkByCharacterImpression();

        RecoverMentalHPOnWalk();//歩行時精神HP回復
        RecoverPointOnWalk();//歩行時ポイント回復
        FadeConfidenceBoostByWalking(walkCount);//歩行によって自信ブーストがフェードアウトする
    }
    public void OnAllyWinCallBack()
    {
        TransitionPowerOnBattleWinByCharacterImpression();
        HP += MAXHP * 0.3f;//HPの自然回復
    }
    public void OnAllyLostCallBack()
    {
        TransitionPowerOnBattleLostByCharacterImpression();
    }
    public void OnAllyRunOutCallBack()
    {
        TransitionPowerOnBattleRunOutByCharacterImpression();
    }
    
    /// <summary>
    /// キャラクターのパワーが歩行によって変化する関数
    /// </summary>
    void TransitionPowerOnWalkByCharacterImpression()
    {
        switch(MyImpression)
        {
            case SpiritualProperty.doremis:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(35))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(25))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(6))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(6))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        if(rollper(2.7f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(7.55f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                }
                break;
            case SpiritualProperty.pillar:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(2.23f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(5))
                        {
                            NowPower = ThePower.high;
                        }
                        if(rollper(20))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(6.09f))
                        {
                            NowPower = ThePower.medium;
                        }
                        if(rollper(15))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(8))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }

                break;
            case SpiritualProperty.kindergarden:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(25))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(31))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(28))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(25))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(20))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(30))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.liminalwhitetile:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(17))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(3))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(3.1f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(13))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(2))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(40))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.sacrifaith:
                switch(NowPower)
                {
                    case ThePower.high:
                        //不変
                    case ThePower.medium:
                        if(rollper(14))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(20))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(26))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.cquiest:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(14))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(3))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(3.1f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(13))
                        {
                            NowPower = ThePower.medium;
                        }

                        break;
                    case ThePower.lowlow:
                        if(rollper(4.3f))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.pysco:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(77.77f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(6.7f))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(3))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(90))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(10))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(80))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.godtier:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(4.26f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(3))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(30))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(28))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(8))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(100))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.baledrival:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(9))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(25))
                        {
                            NowPower = ThePower.high;
                        }
                        if(rollper(11))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(26.5f))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(8))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(50))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.devil:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(5))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(6))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(4.1f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(15))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(7))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(22))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
        }
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

    public AllyClass DeepCopy(AllyClass dst)
    {

        // 2. BaseStates のフィールドをコピー
        InitBaseStatesDeepCopy(dst);

        // 3. AllyClass 独自フィールドをコピー
        //今はない
        
        // 4. 戻り値
        return dst;
    }

}

[Serializable]
public class BassJackStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public BassJackStates DeepCopy()
    {
        var clone = new BassJackStates();
        clone.DeepCopy(clone);
        return clone;
    }
}
[Serializable]
public class SateliteProcessStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public SateliteProcessStates DeepCopy()
    {
        var clone = new SateliteProcessStates();
        clone.DeepCopy(clone);
        return clone;
    }
}
[Serializable]
public class StairStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public StairStates DeepCopy()
    {
        var clone = new StairStates();
        clone.DeepCopy(clone);
        return clone;
    }
}