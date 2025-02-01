using RandomExtensions;
using RandomExtensions.Linq;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
///     パーティー属性
/// </summary>
public enum PartyProperty
{
    TrashGroup,
    HolyGroup,
    MelaneGroup,
    Odradeks,

    Flowerees
    //馬鹿共、聖戦(必死、目的使命)、メレーンズ(王道的)、オドラデクス(秩序から離れてる、目を開いて我々から離れるようにコロコロと)、花樹(オサレ)
}

/// <summary>
///     戦いをするパーティーのクラス
/// </summary>
public class BattleGroup
{

    /// <summary>
    ///     パーティー属性
    /// </summary>
    public PartyProperty OurImpression;

    /// <summary>
    ///     コンストラクタ
    /// </summary>
    public BattleGroup(List<BaseStates> ours, PartyProperty ourImpression,WhichGroup _which)
    {
        Ours = ours;
        OurImpression = ourImpression;
        which = _which;
    }

    /// <summary>
    /// 前のめり消す処理　前のめりした人間が死亡したときなど
    /// </summary>
    private void RemoveVanguard()
    {
        InstantVanguard = null;
    }
    /// <summary>
    /// 前のめり者が死亡した際の処理
    /// </summary>
    public void VanGuardDeath()
    {
        if (InstantVanguard.Death())
        {
            RemoveVanguard();
        }
    }
    /// <summary>
    /// グループ全キャラの追加硬直値をリセットする
    /// </summary>
    public void ResetCharactersRecovelyStepTmpToAdd()
    {
        foreach (var chara in Ours)
        {
            chara.RemoveRecovelyTmpAddTurn();
        }

    }
    /// <summary>
    /// グループ全キャラの特別な補正を消去
    /// </summary>
    public void ResetCharactersUseThinges()
    {
        foreach (var chara in Ours)
        {
            chara.RemoveUseThings();
        }
    }

    /// <summary>
    /// グループ全員の全スキルをリセットする　BattleManager終了時
    /// </summary>
    public void OnBattleEndCharactersSkills()
    {
        foreach (var chara in Ours) 
        {
            chara.OnBattleEndSkills();
        }
    }
    /// <summary>
    /// グループ全員の次のターンへ進む際のコールバック
    /// </summary>
    public void OnPartyNextTurnNoArgument()
    {
        foreach (var chara in Ours)
        {
            chara.OnNextTurnNoArgument();
        }
    }

    /// <summary>
    /// グループ全員の行動を可能な状態にしておく
    /// </summary>
    public void PartyRecovelyTurnOK()
    {
        foreach (var chara in Ours)
        {
            chara.RecovelyOK();
        }

    }
    /// <summary>
    /// パーティー全員が死んでるかどうか
    /// </summary>
    /// <returns></returns>
    public bool PartyDeath()
    {
        foreach (var chara in Ours)
        {
            if (!chara.Death())//死んでなかったら
            {
                return false;//一人でも生きてるからfalse
            }
        }
        return true;//全員死んでるからtrue
    }
    /// <summary>
    /// oursがnormalEnemyの時だけ利用する。リカバリーステップのカウント準備の処理
    /// </summary>
    public void RecovelyStart(int nowProgress)
    {
        List<NormalEnemy> enes =  Ours.OfType<NormalEnemy>().ToList();
        if (enes.Count < 1) Debug.LogWarning("恐らくRecovelyStep用の関数を敵じゃないクラスで利用してる");
        else
        {
            enes.Where(enemy => enemy.Death() && enemy.Reborn).ToList();//死者であり、復活するタイプだけ

            foreach(var ene in enes)
            {
                ene.ReadyRecovelyStep(nowProgress);//敵キャラの復活歩数準備
            }
        }

        
    }

    /// <summary>
    ///     集団の人員リスト
    /// </summary>
    public List<BaseStates> Ours {  get; private set; }

    public void SetCharactersList(List<BaseStates> list)
    {
        Ours = list;
    }

    /// <summary>
    /// 現在のグループで前のめりしているcharacter
    /// </summary>
    public BaseStates InstantVanguard;

    /// <summary>
    /// 陣営
    /// </summary>
    public WhichGroup which;

    /// <summary>
    /// このグループには指定したどれかの精神印象を持った奴が"一人でも"いるかどうか　
    /// 複数の印象を一気に指定できます。
    /// </summary>
    public bool ContainAnyImpression(params SpiritualProperty[] impressions)
    {
        return Ours.Any(one => impressions.Contains(one.MyImpression));
    }

    /// <summary>
    /// 指定された印象を持ったキャラクター達を返す関数
    /// </summary>
    public List<BaseStates> GetCharactersFromImpression(params SpiritualProperty[] impressions)
    {
        return Ours.Where(one => impressions.Contains(one.MyImpression)).ToList();
    }
}