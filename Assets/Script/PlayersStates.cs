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
using Cysharp.Threading.Tasks;   // UniTask
[Serializable]
public class ButtonAndSkillIDHold
{
    public Button button;
    public int skillID;
    public void AddButtonFunc(UnityAction<int> call)
    {
        Debug.Log("AddButtonFunc" + skillID);
        button.onClick.AddListener(() => call(skillID));
    }
}
/// <summary>
/// スキルIDが必要なラジオボタン処理用のコントローラー
/// スキルIDなどが必要のない、例えば「キャラ自体の設定用」などは直接ToggleGroupControllerを使う。
/// </summary>
[Serializable]
public class RadioButtonsAndSkillIDHold
{
    public ToggleGroupController Controller;
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

    public GameObject EyeArea;
    public ActionMarkUI ActionMar;

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
        Debug.Log("ApplySkillButtons");
        //ボタンに「スキルを各キャラの使用スキル変数と結びつける関数」　を登録する
        foreach (var button in skillButtonList_geino)
        {
            Debug.Log("ApplySkillButtons geino");
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

        //ラジオボタンに「割り込みカウンターActiveのコールバック関数」を登録する
        SelectInterruptCounterRadioButtons_geino.AddListener(geino.OnSelectInterruptCounterActiveBtnCallBack);
        SelectInterruptCounterRadioButtons_noramlia.AddListener(noramlia.OnSelectInterruptCounterActiveBtnCallBack);
        SelectInterruptCounterRadioButtons_sites.AddListener(sites.OnSelectInterruptCounterActiveBtnCallBack);

        //何もしないボタンを結び付ける
        if(DoNothingButton_noramlia != null)
        {
            DoNothingButton_noramlia.onClick.AddListener(noramlia.OnSkillDoNothingBtnCallBack);
        }
        if(DoNothingButton_geino != null)
        {
            DoNothingButton_geino.onClick.AddListener(geino.OnSkillDoNothingBtnCallBack);
        }
        if(DoNothingButton_sites != null)
        {
            DoNothingButton_sites.onClick.AddListener(sites.OnSkillDoNothingBtnCallBack);
        }


        Debug.Log("ボタンとコールバックを結び付けました - ApplySkillButtons");
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
    [SerializeField] private List<RadioButtonsAndSkillIDHold> skillSelectAgressiveCommitRadioList_geino = new();
    [SerializeField] private List<RadioButtonsAndSkillIDHold> skillSelectAgressiveCommitRadioList_noramlia = new();
    [SerializeField] private List<RadioButtonsAndSkillIDHold> skillSelectAgressiveCommitRadioList_sites = new();

    //割り込みカウンターActiveのラジオボタン用リスト
    //スキル依存でないものは直接ToggleGroupControllerを使う。
    [SerializeField]
    private ToggleGroupController SelectInterruptCounterRadioButtons_geino;
    [SerializeField]
    private ToggleGroupController SelectInterruptCounterRadioButtons_noramlia;
    [SerializeField]
    private ToggleGroupController SelectInterruptCounterRadioButtons_sites;

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
            //前のめり = 0 そうでないなら　1 なので　IsAgressiveCommitの三項演算子
            radio.Controller.SetOnWithoutNotify(skill.IsAggressiveCommit ? 0 : 1);
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
            //前のめり = 0 そうでないなら　1 なので　IsAgressiveCommitの三項演算子
            radio.Controller.SetOnWithoutNotify(sites.SkillList[radio.skillID].IsAggressiveCommit ? 0 : 1);
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
            radio.Controller.SetOnWithoutNotify(noramlia.SkillList[radio.skillID].IsAggressiveCommit ? 0 : 1);
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
        if(CancelPassiveButtonField_geino == null) Debug.LogError("CancelPassiveButtonField_geinoがnullです");
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
    [SerializeField] Button DoNothingButton_geino;
    [SerializeField] Button DoNothingButton_sites;
    [SerializeField] Button DoNothingButton_noramlia;
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
    /// <summary>
    ///     味方のキャラクターのUIを表示するか非表示する
    /// </summary>
    public void AllyAlliesUISetActive(bool isActive)
    {
        if (geino?.UI != null) geino.UI.SetActive(isActive);
        if (sites?.UI != null) sites.UI.SetActive(isActive);
        if (noramlia?.UI != null) noramlia.UI.SetActive(isActive);
    }

    public BattleGroup GetParty()
    {
        //var playerGroup = new List<BaseStates> { geino, sites, noramlia }; //キャラ
        var playerGroup = new List<BaseStates> {geino}; //テストプレイはジーノのみ
        var nowOurImpression = GetPartyImpression(); //パーティー属性を彼らのHPから決定
        var CompatibilityData = new Dictionary<(BaseStates,BaseStates),int>();//相性値のデータ保存用

        // 複数キャラがいる場合のみ、各キャラペアの相性値を計算して格納する
        if (playerGroup.Count >= 2)
        {
            //ストーリー依存
        }
        
        //UI表示いるキャラだけ
        AllyAlliesUISetActive(false);
        foreach(var chara in playerGroup)
        {
            if (chara == null)
            {
                Debug.LogWarning("GetParty: playerGroup に null キャラが含まれています。");
                continue;
            }

            if (chara.UI != null)
            {
                chara.UI.SetActive(true);

                // HPバーを初期化（存在する場合のみ）
                var bar = chara.UI.HPBar;
                if (bar != null)
                {
                    bar.SetBothBarsImmediate(
                        chara.HP / chara.MaxHP,
                        chara.MentalHP / chara.MaxHP,
                        chara.GetMentalDivergenceThreshold());
                }
                else
                {
                    Debug.LogWarning($"GetParty: {chara.CharacterName} の UI.HPBar が未割り当てです。");
                }
            }
            else
            {
                Debug.LogWarning($"GetParty: {chara.CharacterName} の UI が未割り当てです。");
            }
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

    //モーダルエリア
    public GameObject ModalArea;

    void GoToModalArea()
    {
        ModalArea.gameObject.SetActive(true);
        EyeArea.gameObject.SetActive(false);//eyeAreaが上に映っちゃうから非表示
    }
    void ReturnModalArea()
    {
        EyeArea.gameObject.SetActive(true);//eyeAreaを元に戻す
        ModalArea.gameObject.SetActive(false);
    }

    //スキルパッシブ対象スキル選択ボタン管理エリア
    [SerializeField]SelectSkillPassiveTargetSkillButtons SelectSkillPassiveTargetHandle;
    /// <summary>
    /// スキルパッシブ対象スキル選択画面へ行く
    /// </summary>
    public async UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea
    (List<BaseSkill> skills, int selectCount)
    {
        GoToModalArea();//モーダルエリアの表示
        
        SelectSkillPassiveTargetHandle.gameObject.SetActive(true);//ボタンエリアの表示
        return await SelectSkillPassiveTargetHandle.ShowSkillsButtons(skills, selectCount);//スキル選択ボタンの生成
    }
    /// <summary>
    /// スキルパッシブ対象スキル選択画面から戻る
    /// </summary>
    public void ReturnSelectSkillPassiveTargetSkillButtonsArea()
    {
        SelectSkillPassiveTargetHandle.gameObject.SetActive(false);//ボタンエリアの非表示
        ReturnModalArea();//モーダルエリアの非表示
    }

    /// <summary>
    /// 思い入れスキル選択UI
    /// </summary>
    [SerializeField]SelectEmotionalAttachmentSkillButtons EmotionalAttachmentSkillSelectUIArea;
    ///思い入れスキル弱体化用スキルパッシブ
    public BaseSkillPassive EmotionalAttachmentSkillWeakeningPassive;
    /// <summary>
    /// ジーノの思い入れスキル選択UI表示ボタン
    /// </summary>
    public void Geino_OpenEmotionalAttachmentSkillSelectUIArea()
    {
        EmotionalAttachmentSkillSelectUIArea.OpenEmotionalAttachmentSkillSelectUIArea();
        EmotionalAttachmentSkillSelectUIArea.ShowSkillsButtons
        (geino.SkillList.Cast<AllySkill>().Where(skill => skill.IsTLOA).ToList(),//TLOAスキルのみ
         geino.EmotionalAttachmentSkillID, geino.OnEmotionalAttachmentSkillIDChange);
        //キャラのスキルリストと、現在の思い入れスキルIDを渡す
    }
    public void Sites_OpenEmotionalAttachmentSkillSelectUIArea()
    {
        EmotionalAttachmentSkillSelectUIArea.OpenEmotionalAttachmentSkillSelectUIArea();
        EmotionalAttachmentSkillSelectUIArea.ShowSkillsButtons
        (sites.SkillList.Cast<AllySkill>().Where(skill => skill.IsTLOA).ToList(),
         sites.EmotionalAttachmentSkillID, sites.OnEmotionalAttachmentSkillIDChange);
        //キャラのスキルリストと、現在の思い入れスキルIDを渡す
    }
    public void Noramlia_OpenEmotionalAttachmentSkillSelectUIArea()
    {
        EmotionalAttachmentSkillSelectUIArea.OpenEmotionalAttachmentSkillSelectUIArea();
        EmotionalAttachmentSkillSelectUIArea.ShowSkillsButtons
        (noramlia.SkillList.Cast<AllySkill>().Where(skill => skill.IsTLOA).ToList(),
         noramlia.EmotionalAttachmentSkillID, noramlia.OnEmotionalAttachmentSkillIDChange);
        //キャラのスキルリストと、現在の思い入れスキルIDを渡す
    }
    

    /// <summary>
    /// バトルスタート時のUI管理
    /// </summary>
    public void OnBattleStart()
    {
        EmotionalAttachmentSkillSelectUIArea.CloseEmotionalAttachmentSkillSelectUIArea();//思い入れスキル選択UIが開きっぱなしなら閉じる
    }

    /// <summary>
    /// 
    /// </summary>
    public void CheckGeinoTenDayValuesCallBack()
    {
        Debug.Log($"{geino.CharacterName}の十日能力値:");
        foreach(var tenDay in geino.BaseTenDayValues)
        {
            Debug.Log($"{tenDay.Key}: {tenDay.Value}");
        }
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
    /// 主人公キャラはUIコントローラーを直接参照
    /// </summary>
    [SerializeField] UIController _uic;


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
        oldSkill.ApplyEmotionalAttachmentSkillQuantityChangeSkillWeakeningPassive//弱体化スキルパッシブ専用の付与関数
        (PlayersStates.Instance.EmotionalAttachmentSkillWeakeningPassive);
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
        //var acter = Walking.bm.Acter;

        //範囲を選べるのなら　　 (自分だけのスキルなら範囲選択の性質があってもできない、本来できないもの)
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
    /// 味方キャラの歩く際に呼び出されるコールバック
    /// </summary>
    public void OnWalkCallBack(int walkCount)
    {
        AllPassiveWalkEffect();//全パッシブの歩行効果を呼ぶ
        UpdateWalkAllPassiveSurvival();
        UpdateAllSkillPassiveWalkSurvival();//スキルパッシブの歩行残存処理
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
        var ratio = Walking.bm.EnemyGroup.OurTenDayPowerSum(false) / Walking.bm.AllyGroup.OurTenDayPowerSum(false);
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
    /// AllyClassのディープコピー
    /// 初期化の処理もawake代わりにここでやろう
    /// </summary>
    /// <param name="dst"></param>
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
        dst.EmotionalAttachmentSkillID = EmotionalAttachmentSkillID;//思い入れスキルIDをコピー
        //dst._uic = _uic;//参照なのでそのまま渡す 主人公キャラの参照UIオブジェクト
        if(_uic != null)
        {
            dst.BindUIController(_uic);//一元化された基本クラスのフィールドに設定する
        }
        if(dst.UI == null)Debug.LogError("UIがnullです");
        if(dst.UI.arrowGrowAndVanish == null)Debug.LogError("arrowGrowAndVanishがnullです");
        dst.UI.arrowGrowAndVanish.InitializeArrowByIcon();//主人公はここで矢印画像のアイコンに合わせた初期化をする。

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

    /// <summary>
    /// allySkillにおける行使者はAllyClassなのでキャストして返す
    /// </summary>
    public new AllyClass Doer => (AllyClass)base.Doer;

    /// <summary>
    /// スキル熟練度　単純な数のプロパティで、標準のロジックはスキル内ではないからここでは数だけ
    /// </summary>
    public float Proficiency;

    /// <summary>
    /// スキル使用回数をカウントアップする
    /// allySkillでは熟練度が加算される
    /// </summary>
    public override void DoSkillCountUp()
    {
        base.DoSkillCountUp();
        AddProficiency();
        AddEmotionalAttachmentSkillQuantity();
    }

    /// <summary>
    /// 熟練度を加算する（思い入れ量に応じてスケーリング）
    /// </summary>
    void AddProficiency()
    {
        // このスキルが思い入れスキルかどうかチェック
        if (Doer.EmotionalAttachmentSkillID == ID)
        {
            // 思い入れスキルの場合、思い入れ量に応じてスケーリング
            float multiplier = Doer.GetProficiencyMultiplierFromQuantity();
            Proficiency += multiplier;
            Debug.Log($"思い入れスキル熟練度加算: +{multiplier:F2} (思い入れ量: {Doer.EmotionalAttachmentSkillQuantity})");
        }
        else
        {
            // 通常スキルは1.0倍
            Proficiency += 1.0f;
        }
    }
    /// <summary>
    /// allySkillで継承したスキルパワー関数
    /// </summary>
    protected override float _skillPower(bool IsCradle)
    {
        //スキルの思い入れ由来のhp固定加算値
        return base._skillPower(IsCradle) + CurrentHPFixedAdditionSkillPower();
    }
    /// <summary>
    /// 現在HP固定加算をスキルパワーに加算する用の関数
    /// スキルの思い入れ由来の固定加算値
    /// </summary>
    float CurrentHPFixedAdditionSkillPower()
    {
        if(Doer.EmotionalAttachmentSkillID == ID)//思い入れスキルならhp固定加算が入る
        {
            // 思い入れ量から倍率を取得
            float multiplier = Doer.GetCurrentHPFixedAdditionMultiplier();
            
            // 基礎値×現在HP = SkillPower加算値
            float currentHP = Doer.HP;
            float additionPower = multiplier * currentHP;
            
            Debug.Log($"思い入れスキルHP固定加算: +{additionPower:F2} (倍率: {multiplier:F3}, 現在HP: {currentHP}, 思い入れ量: {Doer.EmotionalAttachmentSkillQuantity})");
            return additionPower;
        }
        return 0;
    }
    /// <summary>
    /// 思い入れ量を加算する用の関数
    /// </summary>
    void AddEmotionalAttachmentSkillQuantity()
    {
        Doer.EmotionalAttachmentSkillQuantity++;
    }
    /// <summary>
    /// TLOAの思い入れスキル用弱体化スキルパッシブを付与する専用の関数
    /// 重複を防ぐため既にあった場合、同名のパッシブを削除する
    /// </summary>
    /// <param name="passive">ここに渡すのは弱体化スキルパッシブ(スキルの思い入れ由来)専用</param>
    public void ApplyEmotionalAttachmentSkillQuantityChangeSkillWeakeningPassive(BaseSkillPassive passive)
    {
        if(passive.Name == PlayersStates.Instance.EmotionalAttachmentSkillWeakeningPassive.Name)
        {
            //弱体化スキルパッシブに該当するものが一つでもあったら、それを消す
            for (int i = ReactiveSkillPassiveList.Count - 1; i >= 0; i--)//回してるリストから削除するのでfor文で逆順に回すと安心
            {
                if(ReactiveSkillPassiveList[i].Name == passive.Name)
                {
                    ReactiveSkillPassiveList.RemoveAt(i);
                }
            }
            //思い入れ弱体化スキルパッシブを付与する
            ApplySkillPassive(passive);
        }else
        {
            Debug.LogError("思い入れ弱体化スキルパッシブ付与関数に\n渡されたパッシブがスキルの思い入れ専用の弱体化スキルパッシブではありません");
        }
    }
    
    public AllySkill InitAllyDeepCopy()
    {
        var clone = new AllySkill();
        InitDeepCopy(clone);

        clone._iD = _iD;
        clone.Proficiency = Proficiency;//熟練度
        return clone;
    }
}