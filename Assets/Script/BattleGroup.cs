using RandomExtensions;
using RandomExtensions.Linq;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static CommonCalc;

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
    public BattleGroup(List<BaseStates> ours, PartyProperty ourImpression,allyOrEnemy _which,Dictionary<(BaseStates,BaseStates),int> CompatibilityData = null)
    {
        Ours = ours;
        OurImpression = ourImpression;
        which = _which;
        CharaCompatibility = CompatibilityData;
    }
    /// <summary>
    /// グループの十日能力の総量の平均値を返す　強さの指標
    /// </summary>
    public float OurAvarageTenDayPower()
    {
        float sum = 0f;
        foreach(var chara in Ours)
        {
            sum += chara.TenDayValuesSum;
        }
        return sum / Ours.Count;
    }
    /// <summary>
    /// グループの十日能力の総量
    /// </summary>
    public float OurTenDayPowerSum
    {
        get
        {
            float sum = 0f;
            foreach(var chara in Ours)
            {
                sum += chara.TenDayValuesSum;
            }
            return sum;
        }
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
    /// 主にBattleManagerの終了判定に使う
    /// </summary>
    /// <returns></returns>
    public bool PartyDeathOnBattle()
    {
        if (Ours.Count == 0) return false;//全員死んでる訳ではない
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
    /// パーティー全員分のパッシブの自分たちの誰かがダメージを食らった際のコールバックを呼び出す
    /// </summary>
    /// <param name="Attacker"></param>
    public void PartyPassivesOnAfterAlliesDamage(BaseStates Attacker)
    {
        foreach(var chara in RemoveDeathCharacters(Ours))
        {
            chara.PassivesOnAfterAlliesDamage(Attacker);
        }
    }
    public void PartySlaimsEasterNoshiirEffectOnEnemyDisturbedAttack()
    {
        foreach(var chara in RemoveDeathCharacters(Ours))
        {
            //chara.GetPassiveByID(12)
        }
    }
    
    /// <summary>
    /// このターンで死んだキャラクターのリストに変換
    /// </summary>
    /// <returns></returns>
    List<BaseStates> ThisTurnToDieCharactersList(List<BaseStates> notYetCheckedCharacters)
    {
        var DeathCharacters = OnlyDeathCharacters(notYetCheckedCharacters);//まず死亡者のみに

        return DeathCharacters.Where(chara => chara._tempLive).ToList();//前回生きていたキャラクターのみを残す
    }

    /// <summary>
    /// パーティー全員分相性値の高い敵が死んだときの人間状況の変化処理を行う。
    /// BaseStatesの_tempLiveの記録が行われる前に実行する必要がある。
    /// </summary>
    public void PartyApplyConditionChangeOnCloseAllyDeath()
    {
        var thisTurnDeathCharacters = ThisTurnToDieCharactersList(Ours);
        var LiveCharacters = RemoveDeathCharacters(Ours);//生きてる味方のみ
        if(LiveCharacters.Count == 0)
        {
            Debug.LogWarning("生存者がいないのに、PartApplyConditionChangeOnCloseAllyDeathが呼び出された。");
            return;
        }

        foreach(var LiveAlly in LiveCharacters)
        {
            var DeathCount = 0;
            foreach(var DeathAlly in thisTurnDeathCharacters)//今回の死んだ味方で回す。
            {
                if(CharaCompatibility[(LiveAlly,DeathAlly)] >= 90)//生きてる味方から死んでる味方への相性が90以上なら
                {
                    DeathCount++;//相性値の高い死者数を増やす
                }
            }
            if(DeathCount > 0)//生きてる味方とと相性値が高い今回の死者がいたら
            {
                LiveAlly.ApplyConditionChangeOnCloseAllyDeath(DeathCount);//味方の人間状況を変更
            }
        }
    }
    /// <summary>
    /// パーティー全員分相性値の高い敵が復活したときの人間状況の変化処理を行う。
    /// Angelされた瞬間に行う。
    /// </summary>
    public void PartyApplyConditionChangeOnCloseAllyAngel(BaseStates angel)
    {
        var LiveCharacters = RemoveDeathCharacters(Ours).Where(x => x != angel).ToList();//生きてる味方のみ 今回復活したばかりのangelを除く
        if(LiveCharacters.Count == 0)
        {
            Debug.LogWarning("生存者がいないのに、PartApplyConditionChangeOnCloseAllyAngelが呼び出された。");
            return;
        }

        foreach(var LiveAlly in LiveCharacters)//angel以外の味方全員分ループ処理
        {
            if(CharaCompatibility[(LiveAlly,angel)] >= 77)//生きてる味方からangelへの相性判定
            {
                LiveAlly.ApplyConditionChangeOnCloseAllyAngel();//味方の人間状況を変更
            }
        }
    }
    /// <summary>
    ///     集団の人員リスト
    /// </summary>
    public List<BaseStates> Ours {  get; private set; }
    /// <summary>
    ///グループから逃走処理
    ///逃走時コールバックとキャラクターの消去
    /// </summary>
    public void EscapeAndRemove(NormalEnemy chara)
    {
        Ours.Remove(chara);
        chara.OnRunOut();//逃走コールバック
    }

    ///<summary>
    ///グループの人員同士の相性値
    ///</summary>
    public Dictionary<(BaseStates I,BaseStates You),int> CharaCompatibility = new();

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
    public allyOrEnemy which;

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

    //敵グループ専用関数----------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// 敵グループの勝利時のコールバック
    /// </summary>
    public void EnemyiesOnWin()
    {
        foreach(var enemy in Ours.OfType<NormalEnemy>())
        {
            enemy.OnWin();
        }
    }
    /// <summary>
    /// 敵グループの逃げ出した時のコールバック
    /// </summary>
    public void EnemiesOnRunOut()
    {
        foreach(var enemy in Ours.OfType<NormalEnemy>())
        {
            enemy.OnRunOut();
        }
    }
    /// <summary>
    /// 主人公達が逃げ出した時のコールバック
    /// </summary>
    public void EnemiesOnAllyRunOut()
    {
        foreach(var enemy in Ours.OfType<NormalEnemy>())
        {
            enemy.OnAllyRunOut();
        }
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
            enes.Where(enemy => enemy.Death() && enemy.Reborn && !enemy.broken).ToList();//死者であり、復活するタイプであり、壊れてないものだけ

            foreach(var ene in enes)
            {
                ene.ReadyRecovelyStep(nowProgress);//敵キャラの復活歩数準備
            }
        }
    }
}