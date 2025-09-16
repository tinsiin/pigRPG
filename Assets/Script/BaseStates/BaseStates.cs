using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RandomExtensions;
using RandomExtensions.Linq;
using System;
using static UnityEngine.Rendering.DebugUI;
using static CommonCalc;
using UnityEditor;
using static TenDayAbilityPosition;

//BaseStatesのシンプルなコア部分

/// <summary>
///     基礎ステータスのクラス　　クラスそのものは使用しないので抽象クラス
/// </summary>
[Serializable]
public abstract partial class BaseStates
{
    //  ==============================================================================================================================
    //                                              参照用
    //  ==============================================================================================================================


    WatchUIUpdate wui => WatchUIUpdate.Instance;
    protected BattleManager manager => Walking.Instance.bm;
    protected SchizoLog schizoLog => SchizoLog.Instance;

    //  ==============================================================================================================================
    //                                              UI
    //  ==============================================================================================================================

    /// <summary>
    /// 一元化したキャラ用のUIコントローラー
    /// </summary>
    public UIController UI { get; private set; }
    /// <summary>
    /// それぞれの陣営でUIの生成方法をは違うので、基本クラスの一元化したフィールドに設定する。
    /// </summary>
    /// <param name="ui"></param>
    public void BindUIController(UIController ui)
    {
        UI = ui;
        UI.BindUser(this);

        // 属性ポイントリングUIを探し、無ければ追加して初期化
        if (UI != null && UI.Icon != null)
        {
            var ring = UI.GetComponentInChildren<AttrPointRingUIController>(true);
            if (ring == null)
            {
                ring = UI.gameObject.AddComponent<AttrPointRingUIController>();
            }
            ring.Initialize(this, UI.Icon.rectTransform);
        }
    }
    //  ==============================================================================================================================
    //                                              フレーバー要素
    //  ==============================================================================================================================

    [Header("フレーバー要素")]
    /// <summary>
    ///     このキャラクターの名前
    /// </summary>
    public string CharacterName;
    /// <summary>
    ///     このキャラクターの説明
    /// </summary>
    public string Description = "入力されていません";

    /// <summary>
    /// 裏に出す種別も考慮した彼のことの名前
    /// </summary>
    public string ImpressionStringName;



        

    /// <summary>
    ///派生クラスのディープコピーでBaseStatesのフィールドをコピーする関数 
    ///ゲーム開始時セーブデータがない時前提のディープコピー(戦闘中に扱われる値などのコピーの必要がないものは省略)
    /// </summary>
    protected void InitBaseStatesDeepCopy(BaseStates dst)
    {
        //_passiveListは戦闘されないと入らない
        //初期所持パッシブがあるのなら、_passiveListに入れて渡す
        if(InitpassiveIDList.Count > 0)
        {
            foreach (var passiveID in InitpassiveIDList)
            {
                Debug.Log($"{CharacterName}の初期パッシブ:{passiveID}");
                dst.ApplyPassiveByID(passiveID);//applyする
            }
        }
        //VitalLayerのコピー　追加HP
        if(InitVitalLaerIDList.Count > 0){
            foreach (var vitalLayerID in InitVitalLaerIDList)
            {
                dst.ApplyVitalLayer(vitalLayerID);
            }
        }

        //スキルは敵や主人公達によって違うシステム管理なので、各クラスでスキルの実体リストを持つ。
        /*
        //スキルのコピー
        foreach (var skill in _skillList)
        {
            dst._skillList.Add(skill.InitDeepCopy());
        }*/

        //NowPowerは戦闘開始時や歩行で切り替わるから、コピーしない

        dst.b_b_atk = b_b_atk;
        dst.b_b_def = b_b_def;
        dst.b_b_eye = b_b_eye;
        dst.b_b_agi = b_b_agi;

        //十日能力のディープコピー
        dst._baseTenDayValues = new TenDayAbilityDictionary();
        foreach(var tenDay in _tenDayTemplate)
        {
            //Debug.Log($"({CharacterName})ディープコピーで十日能力をコピー。-{tenDay.Key} : {tenDay.Value}");
            dst._baseTenDayValues.Add(tenDay.Key,tenDay.Value);
        }
        //Debug.Log($"{CharacterName}のコピーした十日能力のリストの数:{dst._baseTenDayValues.Count}");
        dst.CharacterName = CharacterName;
        dst.ImpressionStringName = ImpressionStringName;
        dst.ApplyWeapon(InitWeaponID);//ここで初期武器と戦闘規格を設定
        dst.maxRecoveryTurn = maxRecoveryTurn;
        //dst.UI = UI;//各キャラで扱い方が違うから
        dst._hp = _hp;
        dst._maxhp = _maxhp;
        // maxHPがコピーされた後に、プロパティ経由でPを設定（下限0、上限MAXP）
        dst.P = Mathf.Clamp(this.P, 0, dst.MAXP);
        dst._mentalHP = _mentalHP;
        dst.MyType = MyType;
        dst.MyImpression = DefaultImpression;//デフォルト精神属性を最初の精神属性にする　-> エンカウント時に持ってるスキルの中でランダムに決まるけどまぁ一応ね
        dst.DefaultImpression = DefaultImpression;
        dst.PersistentAdaptSkillImpressionMemories = PersistentAdaptSkillImpressionMemories;//恒常的な慣れ補正のリストはインスペクタで敵とかが初期所持ので記録するかもしれないのでコピー

        //思えの値ユニーク値の思慮係数をディープコピー
        dst._thinkingFactor = _thinkingFactor;
        //思えの値現在値をランダム化
        dst.InitializeNowResonanceValue();

        // 属性ポイント（混合上限 + DropNew）のディープコピー（AttrPointModule 経由）
        var _attrState = this.AttrP.ExportState();
        dst.AttrP.ImportState(_attrState, suppressNotify: true);

        if(dst.DefaultImpression == 0)
        {
            //Debug.LogError("DefaultImpressionが0です、敵はディープコピー時デフォルトの精神属性が入ります。");
        }
        //Debug.Log($"{CharacterName}のDefaultImpression:{dst.DefaultImpression}");

        //Debug.Log(CharacterName + "のBaseStatesディープコピー完了");
        //パワーは初期値　medium allyは歩行で変化　enemyは再遭遇時コールバックで一回だけ歩行変化で判別
        
    }

    
}