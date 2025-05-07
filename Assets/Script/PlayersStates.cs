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
[Serializable]
public class ButtonAndSkillIDHold
{
    public Button button;
    public int skillID;
    public void AddButtonFunc(UnityAction<int> call)
    {
        button.onClick.AddListener(() => call(skillID));
    }
}
[Serializable]
public class RadioButtonsAndSkillIDHold
{
    public ToggleGroupController_SelectAggressiveCommit Controller;
    public int skillID;
    
    // UnityAction<int, int>に変更 - 第1引数：どのトグルが選ばれたか、第2引数：skillID
    public void AddRadioFunc(UnityAction<int, int> call)
    {
         // nullチェック
        if (Controller == null)
        {
            Debug.LogError("toggleGroupがnullです！ skillID: " + skillID);
        }
        
        if (call == null)
        {
            Debug.LogError("callがnullです！ skillID: " + skillID);
        }
        // 両方の情報を渡す
        Controller.AddListener((int toggleIndex) => call(toggleIndex, skillID));
    }

    public void Interactable(bool interactable)
    {
        Controller.interactable = interactable;
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
    /// <summary>
    /// ゲームの値や、主人公達のステータスの初期化
    /// </summary>
    public void Init()
    {
        Debug.Log("Init");

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

        //デフォルト精神属性の初回設定
        geino.DecideDefaultMyImpression();
        noramlia.DecideDefaultMyImpression();
        sites.DecideDefaultMyImpression();

        

        ApplySkillButtons();//ボタンの結びつけ処理

        //現在の有効化リストIDの分だけスキルボタンを見えるようにする。
        UpdateSkillButtonVisibility();
 
       
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

        //ラジオボタンに「スキルを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillSelectAgressiveCommitRadioList_geino)
        {
            button.AddRadioFunc(geino.OnSkillSelectAgressiveCommitBtnCallBack);
        }

        //ラジオボタンに「スキルを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillSelectAgressiveCommitRadioList_noramlia)
        {
            button.AddRadioFunc(noramlia.OnSkillSelectAgressiveCommitBtnCallBack);
        }

        //ラジオボタンに「スキルを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillSelectAgressiveCommitRadioList_sites)
        {
            button.AddRadioFunc(sites.OnSkillSelectAgressiveCommitBtnCallBack);
        }


    }

    [SerializeField]
    private List<ButtonAndSkillIDHold> skillButtonList_geino =new();//ジーノ用スキルボタン用リスト
    [SerializeField]
    private List<ButtonAndSkillIDHold> skillButtonList_noramlia=new();//ノーマリア用スキルボタン用リスト
    [SerializeField]
    private List<ButtonAndSkillIDHold> skillButtonList_sites=new();//サテライト用スキルボタン用リスト

    [SerializeField]
    private List<ButtonAndSkillIDHold> skillStockButtonList_geino=new();//ジーノの該当のスキルの攻撃ストックボタン用リスト
    [SerializeField]
    private List<ButtonAndSkillIDHold> skillStockButtonList_noramlia=new();//ノーマリアの該当のスキルの攻撃ストックボタン用リスト
    [SerializeField]
    private List<ButtonAndSkillIDHold> skillStockButtonList_sites=new();//サテライトの該当のスキルの攻撃ストックボタン用リスト

    //前のめり選択が可能なスキル用に選択できるラジオボタン用リスト
    [SerializeField]
    private List<RadioButtonsAndSkillIDHold> skillSelectAgressiveCommitRadioList_geino = new();
    [SerializeField]
    private List<RadioButtonsAndSkillIDHold> skillSelectAgressiveCommitRadioList_noramlia = new();
    [SerializeField]
    private List<RadioButtonsAndSkillIDHold> skillSelectAgressiveCommitRadioList_sites = new();

    /// <summary>
    /// スキル選択画面へ遷移する際のコールバック
    /// </summary>
    public void OnSkillSelectionScreenTransition_geino() 
    {
        //OnlyInteractHasZoneTraitSkills_geino(OnlyRemainButtonByZoneTrait,OnlyRemainButtonByType);//ボタンのオンオフをするコールバック
        //これは引数必要だから呼び出し元で
        OnlyInteractHasHasBladeWeaponShowBladeSkill_geino();

        //「有効化されてるスキル達のみ」の前のめり選択状態を　ラジオボタンに反映する処理
        foreach(var radio in skillSelectAgressiveCommitRadioList_geino.Where(radio => geino.ValidSkillIDList.Contains(radio.skillID)))
        {
            BaseSkill skill = geino.SkillList[radio.skillID];
            if(skill == null) Debug.LogError("スキルがありません");
            radio.Controller.UpdateToggleState(skill.IsAggressiveCommit);
        }
    }
    /// <summary>
    /// スキル選択画面へ遷移する際のコールバック（sites用）
    /// </summary>
    public void OnSkillSelectionScreenTransition_sites() 
    {
        //OnlyInteractHasZoneTraitSkills_sites(OnlyRemainButtonByZoneTrait,OnlyRemainButtonByType);//ボタンのオンオフをするコールバック
        //これは引数必要だから呼び出し元で
        OnlyInteractHasHasBladeWeaponShowBladeSkill_sites();

        //「有効化されてるスキル達のみ」の前のめり選択状態を　ラジオボタンに反映する処理
        foreach(var radio in skillSelectAgressiveCommitRadioList_sites.Where(radio => sites.ValidSkillIDList.Contains(radio.skillID)))
        {
            radio.Controller.UpdateToggleState(sites.SkillList[radio.skillID].IsAggressiveCommit);
        }
    }

    /// <summary>
    /// スキル選択画面へ遷移する際のコールバック（bassjack/noramlia用）
    /// </summary>
    public void OnSkillSelectionScreenTransition_noramlia() 
    {
        //OnlyInteractHasZoneTraitSkills_noramlia(OnlyRemainButtonByZoneTrait,OnlyRemainButtonByType);//ボタンのオンオフをするコールバック
        //これは引数必要だから呼び出し元で
        OnlyInteractHasHasBladeWeaponShowBladeSkill_noramlia();

        //「有効化されてるスキル達のみ」の前のめり選択状態を　ラジオボタンに反映する処理
        foreach(var radio in skillSelectAgressiveCommitRadioList_noramlia.Where(radio => noramlia.ValidSkillIDList.Contains(radio.skillID)))
        {
            radio.Controller.UpdateToggleState(noramlia.SkillList[radio.skillID].IsAggressiveCommit);
        }
    }

    
    /// <summary>
    /// 指定したZoneTraitとスキル性質を所持するスキルのみを、有効化しそれ以外を無効化するコールバック
    /// </summary>
    public void OnlySelectActs_geino(SkillZoneTrait trait,SkillType type,bool OnlyCantACTPassiveCancel)
    {
        foreach(var skill in geino.SkillList.Cast<AllySkill>())
        {
            //有効なスキルのidとボタンのスキルidが一致したらそれがそのスキルのボタン
            var hold = skillButtonList_geino.Find(hold => hold.skillID == skill.ID);
            if(OnlyCantACTPassiveCancel)//キャンセル可能な行動可能パッシブを消せるなら
            {
                //一つでも持ってればOK
                if (hold != null)
                {
                    hold.button.interactable = false;//有効なスキルは全て無効
                }
            }else//それ以外は
            {
                
                //一つでも持ってればOK
                if (hold != null)
                {
                    hold.button.interactable = skill.HasZoneTraitAny(trait) && skill.HasType(type);
                }
            }
        }
        //キャンセル可能な行動可能パッシブを作成する。
        CancelPassiveButtonField_geino.ShowPassiveButtons(geino,OnlyCantACTPassiveCancel);
    }
    public void OnlySelectActs_normalia(SkillZoneTrait trait, SkillType type, bool OnlyCantACTPassiveCancel)
    {
        foreach(var skill in noramlia.SkillList.Cast<AllySkill>())
        {
            //有効なスキルのidとボタンのスキルidが一致したらそれがそのスキルのボタン
            var hold = skillButtonList_noramlia.Find(hold => hold.skillID == skill.ID);
            if(OnlyCantACTPassiveCancel)//キャンセル可能な行動可能パッシブを消せるなら
            {
                //一つでも持ってればOK
                if (hold != null)
                {
                    hold.button.interactable = false;//有効なスキルは全て無効
                }
            }
            else//それ以外は
            {
                //一つでも持ってればOK
                if (hold != null)
                {
                    hold.button.interactable = skill.HasZoneTraitAny(trait) && skill.HasType(type);
                }
            }
        }
        //キャンセル可能な行動可能パッシブを作成する。
        CancelPassiveButtonField_normalia.ShowPassiveButtons(noramlia, OnlyCantACTPassiveCancel);
    }
    public void OnlySelectActs_sites(SkillZoneTrait trait, SkillType type, bool OnlyCantACTPassiveCancel)
    {
        foreach(var skill in sites.SkillList.Cast<AllySkill>())
        {
            //有効なスキルのidとボタンのスキルidが一致したらそれがそのスキルのボタン
            var hold = skillButtonList_sites.Find(hold => hold.skillID == skill.ID);
            if(OnlyCantACTPassiveCancel)//キャンセル可能な行動可能パッシブを消せるなら
            {
                //一つでも持ってればOK
                if (hold != null)
                {
                    hold.button.interactable = false;//有効なスキルは全て無効
                }
            }
            else//それ以外は
            {
                //一つでも持ってればOK
                if (hold != null)
                {
                    hold.button.interactable = skill.HasZoneTraitAny(trait) && skill.HasType(type);
                }
            }
        }
        //キャンセル可能な行動可能パッシブを作成する。
        CancelPassiveButtonField_sites.ShowPassiveButtons(sites, OnlyCantACTPassiveCancel);
    }
    /// <summary>
    /// 刃物武器でないと刃物スキルが表示されない処理。
    /// 既に全てのスキルが表示されてる前提の関数なので、
    /// 非表示にしていくって形で。
    /// </summary>
    public void OnlyInteractHasHasBladeWeaponShowBladeSkill_geino()
    {
        if(geino.NowUseWeapon.IsBlade) return;//刃物武器だから非表示の必要なし、終了。

        //刃物武器でないので、刃物スキルの非表示処理
        foreach(var skill in geino.SkillList.Cast<AllySkill>())
        {
            //有効なスキルのidとボタンのスキルidが一致したらそれがそのスキルのボタン
            var hold = skillButtonList_geino.Find(hold => hold.skillID == skill.ID);
            //刃物スキルのボタンを非表示にする
            if (hold != null)
            {
                hold.button.interactable = !skill.IsBlade;
            }
        }
    }
    public void OnlyInteractHasHasBladeWeaponShowBladeSkill_noramlia()
    {
        if(noramlia.NowUseWeapon.IsBlade) return;//刃物武器だから非表示の必要なし、終了。

        //刃物武器でないので、刃物スキルの非表示処理
        foreach(var skill in noramlia.SkillList.Cast<AllySkill>())
        {
            //有効なスキルのidとボタンのスキルidが一致したらそれがそのスキルのボタン
            var hold = skillButtonList_noramlia.Find(hold => hold.skillID == skill.ID);
            //刃物スキルのボタンを非表示にする
            if (hold != null)
            {
                hold.button.interactable = !skill.IsBlade;
            }
        }
    }
    public void OnlyInteractHasHasBladeWeaponShowBladeSkill_sites()
    {
        if(sites.NowUseWeapon.IsBlade) return;//刃物武器だから非表示の必要なし、終了。

        //刃物武器でないので、刃物スキルの非表示処理
        foreach(var skill in sites.SkillList.Cast<AllySkill>())
        {
            //有効なスキルのidとボタンのスキルidが一致したらそれがそのスキルのボタン
            var hold = skillButtonList_sites.Find(hold => hold.skillID == skill.ID);
            //刃物スキルのボタンを非表示にする
            if (hold != null)
            {
                hold.button.interactable = !skill.IsBlade;
            }
        }
    }

    /// <summary>
    /// スキルボタンの使いを有効化する処理　可視化
    /// </summary>
    void UpdateSkillButtonVisibility()
    {
        // 有効なスキルのIDを抽出（キャストが必要ならキャストも実施）
        var activeSkillIds_geino = new HashSet<int>(geino.SkillList.Cast<AllySkill>().Select(skill => skill.ID));
        var activeSkillIds_normalia = new HashSet<int>(noramlia.SkillList.Cast<AllySkill>().Select(skill => skill.ID));
        var activeSkillIds_sites = new HashSet<int>(sites.SkillList.Cast<AllySkill>().Select(skill => skill.ID));

        // 各ボタンについて、対応スキルが有効かどうか判定
        foreach (var hold in skillButtonList_geino)
        {
            hold.button.interactable = activeSkillIds_geino.Contains(hold.skillID);
        }
        foreach (var hold in skillStockButtonList_geino)//ストックボタン
        {
            hold.button.interactable = activeSkillIds_geino.Contains(hold.skillID);
        }
        foreach(var hold in skillSelectAgressiveCommitRadioList_geino)
        {
            hold.Interactable(activeSkillIds_geino.Contains(hold.skillID));//前のめり選択ラジオボタンの設定
        }

        foreach (var hold in skillButtonList_noramlia)
        {
            hold.button.interactable = activeSkillIds_normalia.Contains(hold.skillID);
        }
        foreach (var hold in skillStockButtonList_noramlia)//ストックボタン
        {
            hold.button.interactable = activeSkillIds_normalia.Contains(hold.skillID);
        }
        foreach(var hold in skillSelectAgressiveCommitRadioList_noramlia)
        {
            hold.Interactable(activeSkillIds_normalia.Contains(hold.skillID));//前のめり選択ラジオボタンの設定
        }

        foreach (var hold in skillButtonList_sites)
        {
            hold.button.interactable = activeSkillIds_sites.Contains(hold.skillID);
        }
        foreach (var hold in skillStockButtonList_sites)//ストックボタン
        {
            hold.button.interactable = activeSkillIds_sites.Contains(hold.skillID);
        }
        foreach(var hold in skillSelectAgressiveCommitRadioList_sites)
        {
            hold.Interactable(activeSkillIds_sites.Contains(hold.skillID));//前のめり選択ラジオボタンの設定
        }
    }




    //連続実行スキル(FreezeConsecutive)の停止予約のボタン
    //主にCharaConfigタブの方で扱う。
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
    /// スキル選択画面の一番デフォルトのエリア
    /// </summary>
    [SerializeField] GameObject DefaultButtonArea_geino;
    [SerializeField] GameObject DefaultButtonArea_sites;
    [SerializeField] GameObject DefaultButtonArea_normalia;
    /// <summary>
    /// パッシブをキャンセルするボタンのエリア
    /// </summary>
    [SerializeField] SelectCancelPassiveButtons CancelPassiveButtonField_geino;
    [SerializeField] SelectCancelPassiveButtons CancelPassiveButtonField_sites;
    [SerializeField] SelectCancelPassiveButtons CancelPassiveButtonField_normalia;
    /// <summary>
    /// スキル選択デフォルト画面からパッシブキャンセルエリアへ進むボタン
    /// </summary>
    [SerializeField] Button GoToCancelPassiveFieldButton_geino;
    [SerializeField] Button GoToCancelPassiveFieldButton_sites;
    [SerializeField] Button GoToCancelPassiveFieldButton_normalia;
    /// <summary>
    /// パッシブをキャンセルするエリアからデフォルトのスキル選択のエリアまで戻るボタン
    /// </summary>
    [SerializeField] Button ReturnCancelPassiveToDefaultAreaButton_geino;
    [SerializeField] Button ReturnCancelPassiveToDefaultAreaButton_sites;
    [SerializeField] Button ReturnCancelPassiveToDefaultAreaButton_normalia;
    /// <summary>
    /// デフォルトのスキル選択のエリアからキャンセルパッシブのエリアまで進むボタン処理
    /// </summary>
    public void GoToCancelPassiveField_geino()
    {
        DefaultButtonArea_geino.gameObject.SetActive(false);
        CancelPassiveButtonField_geino.gameObject.SetActive(true);
    }
    public void GoToCancelPassiveField_sites()
    {
        DefaultButtonArea_sites.gameObject.SetActive(false);
        CancelPassiveButtonField_sites.gameObject.SetActive(true);
    }
    public void GoToCancelPassiveField_normalia()
    {
        DefaultButtonArea_normalia.gameObject.SetActive(false);
        CancelPassiveButtonField_normalia.gameObject.SetActive(true);
    }
    /// <summary>
    /// キャンセルパッシブのエリアからデフォルトのスキル選択のエリアまで戻る
    /// </summary>
    public void ReturnCancelPassiveToDefaultArea_geino()
    {
        CancelPassiveButtonField_geino.gameObject.SetActive(false);
        DefaultButtonArea_geino.gameObject.SetActive(true);
    }
    public void ReturnCancelPassiveToDefaultArea_sites()
    {
        CancelPassiveButtonField_sites.gameObject.SetActive(false);
        DefaultButtonArea_sites.gameObject.SetActive(true);
    }
    public void ReturnCancelPassiveToDefaultArea_normalia()
    {
        CancelPassiveButtonField_normalia.gameObject.SetActive(false);
        DefaultButtonArea_normalia.gameObject.SetActive(true);
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

        return new BattleGroup(playerGroup, nowOurImpression,allyOrEnemy.alliy,CompatibilityData); //パーティー属性を返す
    }

    /// <summary>
    /// 味方のパーティー属性を取得する 現在のHPによって決まる。
    /// </summary>
    /// <returns></returns>
    private PartyProperty GetPartyImpression()
    {
        //それぞれの最大HP５％以内の差は許容し、それを前提に三人が同じHPだった場合
        float toleranceStair = geino.MaxHP * 0.05f;
        float toleranceSateliteProcess = sites.MaxHP * 0.05f;
        float toleranceBassJack = noramlia.MaxHP * 0.05f;
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
    /// 主人公達の全所持スキルリスト
    /// </summary>
    [SerializeReference,SelectableSerializeReference]
    List<AllySkill> _skillALLList = new();
    /// <summary>
    /// 有効なスキルリスト
    /// </summary>
    public List<int> ValidSkillIDList = new();
    public override IReadOnlyList<BaseSkill> SkillList => _skillALLList.Where(skill => ValidSkillIDList.Contains(skill.ID)).ToList();

    public override void OnInitializeSkillsAndChara()
    {
        foreach (var skill in _skillALLList)
        {
            skill.OnInitialize(this);
        }
    }

    
    /// <summary>
    /// キャラクターのデフォルト精神属性を決定する関数　十日能力が変動するたびに決まる。
    /// </summary>
    public void DecideDefaultMyImpression()
    {
        //1~4の範囲で合致しきい値が決まる。
        var Threshold = RandomEx.Shared.NextInt(1,5);

        //キャラクターの持つ十日能力を多い順に重み付き抽選リストに入れ、処理をする。
        var AbilityList = new WeightedList<TenDayAbility>();
        //linqで値の多い順にゲット
        foreach(var ability in TenDayValues().OrderByDescending(x => x.Value))
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
            if(AbilityList.Count <= 0)//十日能力の重み付き抽選リストが空　つまり十日能力がないなら
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
        NowUseSkill = SkillList[skillListIndex];//使用スキルに代入する
        Debug.Log(SkillList[skillListIndex].SkillName + "を" + CharacterName +" のNowUseSkillにボタンを押して登録しました。");

        //ムーブセットをキャッシュする。
        NowUseSkill.CashMoveSet();

        //今回選んだスキル以外のストック可能なスキル全てのストックを減らす。
        var list = SkillList.Where((skill,index) => index != skillListIndex && skill.HasConsecutiveType(SkillConsecutiveType.Stockpile)).ToList();
        foreach(var stockSkill in list)
        {
            stockSkill.ForgetStock();
        }

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

        
        
        //今回選んだストックスキル以外のストックが減る。
        var list = SkillList.Where((skill,index) => index != skillListIndex && skill.HasConsecutiveType(SkillConsecutiveType.Stockpile)).ToList();
        foreach(var stockSkill in list)
        {
            stockSkill.ForgetStock();
        }

        Walking.bm.DoNothing = true;//ACTBranchingで何もしないようにするboolをtrueに。

        Walking.USERUI_state.Value = TabState.NextWait;//CharacterACTBranchingへ
        
    }

    /// <summary>
    /// 前のめりを選択できるスキルで選択したときのコールバック関数
    /// </summary>
    public void OnSkillSelectAgressiveCommitBtnCallBack(int toggleIndex, int skillID)
    {
        bool isAgrresiveCommit;
        var skill = SkillList[skillID];
        
        if(toggleIndex == 0) 
        {
            isAgrresiveCommit = true;
            Debug.Log("前のめりして攻撃する" );
        }
        else
        {
            isAgrresiveCommit = false;
            Debug.Log("そのままの位置から攻撃" );
        }
        skill.IsAggressiveCommit = isAgrresiveCommit;//スキルの前のめり性に代入すべ
    }

    /// <summary>
    /// スキルの性質に基づいて、次に遷移すべき画面状態を判定する
    /// </summary>
    /// <param name="skill">判定対象のスキル</param>
    /// <returns>遷移先のTabState</returns>
    public static TabState DetermineNextUIState(BaseSkill skill)
    {
        //var acter = Walking.bm.Acter;

        //範囲を選べるのなら　　(自分だけのスキルなら範囲選択の性質があってもできない、本来できないもの)
        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectRange) && !skill.HasZoneTrait(SkillZoneTrait.SelfSkill))
        {
            return TabState.SelectRange;//範囲選択画面へ飛ぶ
        }
        else if ((skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget) || 
                skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget) || 
                skill.HasZoneTrait(SkillZoneTrait.CanSelectMultiTarget))&& !skill.HasZoneTrait(SkillZoneTrait.SelfSkill))
        {//選択できる系なら (自分だけのスキルなら範囲選択の性質があってもできない、本来なら範囲性質に含めてないはず)
            return TabState.SelectTarget;//選択画面へ飛ぶ
        }
        else if (skill.HasZoneTrait(SkillZoneTrait.ControlByThisSituation))
        {
            //~~実行意志ではないので、RangeWillに入れない。~~
            //普通にSelectTargetWillの直前で範囲意志に入ります。
            return TabState.NextWait;//何もないなら事象ボタンへ
        }

        Debug.Log("範囲選択も対象者選択も起こらないControlByThisSituation以外のスキル性質: " + skill.ZoneTrait);
        //acter.RangeWill = skill.ZoneTrait;//実行者の範囲意志にそのままスキルの範囲性質を入れる。
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

    
    public override void OnBattleEndNoArgument()
    {
        base.OnBattleEndNoArgument();
        _walkPointRecoveryCounter = 0;//歩行のポイント回復用カウンターをゼロに
        _walkCountForTransitionToDefaultImpression = 0;//歩行の精神属性変化用カウンターをゼロに
    }
    public override void OnBattleStartNoArgument()
    {
        base.OnBattleStartNoArgument();
        
    }
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
            MentalHP += TenDayValues().GetValueOrZero(TenDayAbility.Rain) + MentalMaxHP * 0.16f;
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
    /// 味方キャラの歩く際に呼び出されるコールバック
    /// </summary>
    public void OnWalkCallBack(int walkCount)
    {
        AllPassiveWalkEffect();//全パッシブの歩行効果を呼ぶ
        UpdateWalkAllPassiveSurvival();
        TransitionPowerOnWalkByCharacterImpression();

        RecoverMentalHPOnWalk();//歩行時精神HP回復
        RecoverPointOnWalk();//歩行時ポイント回復　味方のみ
        ResonanceHealingOnWalking();//歩行時思えの値回復
        FadeConfidenceBoostByWalking(walkCount);//歩行によって自信ブーストがフェードアウトする
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
        TransitionPowerOnBattleLostByCharacterImpression();
    }
    public void OnAllyRunOutCallBack()
    {
        TransitionPowerOnBattleRunOutByCharacterImpression();
    }
    /// <summary>
    /// 勝利時の十日能力ブースト倍化処理
    /// </summary>
    public void AllyVictoryBoost()
    {
        //まず主人公グループと敵グループの強さの倍率
        var ratio = Walking.bm.EnemyGroup.OurTenDayPowerSum / Walking.bm.AllyGroup.OurTenDayPowerSum;
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

    public void DeepCopy(AllyClass dst)
    {

        // 2. BaseStates のフィールドをコピー
        InitBaseStatesDeepCopy(dst);

        // 3. AllyClass 独自フィールドをコピー
        dst._skillALLList = new List<AllySkill>();
        foreach(var skill in _skillALLList)
        {
            dst._skillALLList.Add(skill.InitAllyDeepCopy());
        }
        dst.ValidSkillIDList = new List<int>(ValidSkillIDList);  //主人公達の初期有効化スキルIDをランタイム用リストにセット
        Debug.Log("AllyClassディープコピー完了");
    }

}

[Serializable]
public class BassJackStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public BassJackStates DeepCopy()
    {
        var clone = new BassJackStates();
        DeepCopy(clone);
        Debug.Log("BassJackStatesディープコピー完了");
        return clone;
    }
}
[Serializable]
public class SateliteProcessStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public SateliteProcessStates DeepCopy()
    {
        var clone = new SateliteProcessStates();
        DeepCopy(clone);
        Debug.Log("SateliteProcessStatesディープコピー完了");
        return clone;
    }
}
[Serializable]
public class StairStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public StairStates DeepCopy()
    {
        var clone = new StairStates();
        DeepCopy(clone);
        Debug.Log("StairStatesディープコピー完了");
        return clone;
    }
}
[Serializable]
public class AllySkill : BaseSkill
{
    [SerializeField]
    int _iD;
    /// <summary>
    /// id　主に有効化されてるかどうか
    /// </summary>
    public int ID => _iD;

    public AllySkill InitAllyDeepCopy()
    {
        var clone = new AllySkill();
        InitDeepCopy(clone);

        clone._iD = _iD;
        return clone;
    }
}