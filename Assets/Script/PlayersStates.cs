using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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

        CreateDecideValues();//中央決定値をゲーム開始時一回だけ生成

        NowProgress = 0;//ステージ関連のステータス初期化
        NowStageID = 0;
        NowAreaID = 0;

        geino.OnInitializeSkillsAndChara();//スキル初期化
        noramlia.OnInitializeSkillsAndChara();
        sites.OnInitializeSkillsAndChara();

        ApplySkillButtons();
        Debug.Log("ボタンは読み込まれたはず");

    }
    public StairStates geino;
    public BassJackStates noramlia;
    public SateliteProcessStates sites;

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
    /// <summary>主人公達の歩行時コールバック</summary>
    public void PlayersOnWalkNoArgument()
    {
        geino.OnWalkNoArgument();//歩行時コールバック
        noramlia.OnWalkNoArgument();
        sites.OnWalkNoArgument();   
    }

    public BattleGroup GetParty()
    {
        var playerGroup = new List<BaseStates> { geino, sites, noramlia }; //キャラ
        var nowOurImpression = GetPartyImpression(); //パーティー属性を彼らのHPから決定

        return new BattleGroup(playerGroup, nowOurImpression,WhichGroup.alliy); //パーティー属性を返す
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

}

[Serializable]
public class BassJackStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    
}
[Serializable]
public class SateliteProcessStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
}
[Serializable]
public class StairStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
}