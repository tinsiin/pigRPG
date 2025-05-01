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
    /// <summary>
    /// イースターノジールの効果にて、
    /// どちらのパッシブの効果ターンをどれだけにするかを対象者と付与者の十日能力を比較して決定する
    /// 詳しくは　スレーム=>イースターノジール
    /// </summary>
    void DecideNoshiirTurnByAbilityComparison(ref int noshiirTurn,ref int backFacingTurn,BaseStates target,BaseStates attacker)
    {
        var targetSilent = target.TenDayValues().GetValueOrZero(TenDayAbility.SilentTraining);//対象者のサイレント練度
        var attackerPilmagreatifull = attacker.TenDayValues().GetValueOrZero(TenDayAbility.Pilmagreatifull);//付与者のピルマグレイトフル
        var targetStar = target.TenDayValues().GetValueOrZero(TenDayAbility.StarTersi);//対象者の星テルシ
        var attackerStar = attacker.TenDayValues().GetValueOrZero(TenDayAbility.StarTersi);//付与者の星テルシ
        var targetHeavenAndEndWar = target.TenDayValues().GetValueOrZero(TenDayAbility.HeavenAndEndWar);//対象者の天と終戦

        //星テルシが26以上で、尚且つ相手に対して多ければ、それぞれのピグマ、サイレント練度比較用の値を1.1倍する
        if(attackerStar>=26 && attackerStar >targetStar)
        {
            attackerPilmagreatifull *= 1.1f;
        }else if(targetStar>=26 && targetStar >attackerStar)
        {
            targetSilent *= 1.1f;
        }

        //付与者のピルマ - 対象者のサイレント練度　それが>0なら その余剰÷　対象者のサイレント練度
        var overhead = attackerPilmagreatifull - targetSilent;
        if(overhead > 0 &&  targetSilent > 0)//余剰が合って、対象者のサイレント練度も0よりあるなら
        {
            var Addd = Mathf.Min((int)(overhead / targetSilent),3);//余剰÷対象者のサイレント練度 最大三ターン増える
            noshiirTurn += Addd;
            backFacingTurn += Addd;//背面ターンとノジール両方増やす。
        }

        //天と終戦が 20以降10で割った数分、背面パッシブを減らすことが出来る。　二つまで減らせる
        backFacingTurn -=  (int)Mathf.Min(Mathf.Max(targetHeavenAndEndWar - 20, 0) /10,2);
        

        
    }
    /// <summary>
    /// パーティー全員のイースターノジールの効果を適用と判定の関数
    /// </summary>
    public void PartySlaimsEasterNoshiirEffectOnEnemyDisturbedAttack(BaseStates Attacker,ref StatesPowerBreakdown dmg,ref StatesPowerBreakdown ResonanceDmg)
    {
        const int geinoSlaimID = 17;
        const int slaimID = 12;
        int id = 0;//初期値
        BaseStates Doer = null;//スレーム実行者

        // 1) ジーノスレーム優先検索
        foreach (var chara in RemoveDeathCharacters(Ours))
        {
            var gePas = chara.GetPassiveByID(geinoSlaimID) as geino_Slaim;
            var noPas = chara.GetPassiveByID(slaimID) as Slaim;
            if ((gePas != null &&gePas.EasterNoshiirLockKey) ||(noPas != null &&noPas.EasterNoshiirLockKey)
                || chara.NowPower  < ThePower.low)   
                continue; //パワーが低い未満か(ノジールの否定条件)、またはどっちかのスレームが存在して、ロックがかかっていたら、スキップ

            id   = geinoSlaimID;
            Doer = chara;
            break;
        }

        // 2) ジーノがなければ普通スレームを検索
        if (id == 0)
        {
            foreach (var chara in RemoveDeathCharacters(Ours))
            {
                var gePas = chara.GetPassiveByID(geinoSlaimID) as geino_Slaim;
                var noPas = chara.GetPassiveByID(slaimID) as Slaim;
                if ((gePas != null &&gePas.EasterNoshiirLockKey) ||(noPas != null &&noPas.EasterNoshiirLockKey)
                || chara.NowPower  < ThePower.high)   
                    continue; //パワーが高い未満か(ノジール条件の否定)、またはどっちかのスレームが存在して、ロックがかかっていたら、スキップ

                id   = slaimID;
                Doer = chara;
                break;
            }
        }


        if(id == 0)//スレーム所持者がいないなら終わり
            return;

        //今回のスレームパッシブ
        var pas = Doer.GetPassiveByID(id) as Slaim;
        //イースターノジール効果の効果としてダメージを半減
        dmg /= 2;
        ResonanceDmg /= 2;
        //スレームロックを掛ける。
        pas.EasterNoshiirLock();
        
        int noshiirTurn=2;//ノジールパッシブの効果ターン　　基本ターンが代入しておく
        int backFacingTurn=2;//背面パッシブの効果ターン
        const int noshiirID = 19;
        const int backFacingID = 18;
        
        //ターン数を十日能力の比較で決める
        DecideNoshiirTurnByAbilityComparison(ref noshiirTurn,ref backFacingTurn,Attacker,Doer);

        if(noshiirTurn > 0)//ノジールパッシブの効果ターンがあるなら
        {
            //実行者にノジーラのパッシブを付与する。
            Doer.ApplyPassiveBufferInBattleByID(noshiirID);
            var handle = Doer.GetBufferPassiveByID(noshiirID);//パッシブに付与するハンドル
            handle.DurationTurn = noshiirTurn;//ターン数を代入
            handle.AddEvasionPercentageModifierByAttacker(Attacker, 1.2f);//攻撃者(対象者)に対する20%の回避率

        }
        if(backFacingTurn > 0)//背面パッシブの効果ターンがあるなら
        {
            //攻撃者に背面パッシブの効果付与する。
            Attacker.ApplyPassiveBufferInBattleByID(backFacingID);
            var handle = Attacker.GetBufferPassiveByID(backFacingID);
            handle.DurationTurn = backFacingTurn;
            handle.AddDefensePercentageModifierByAttacker(Doer, 0.7f);//付与者からの攻撃の際防御力が30%低下
            //付与者のピルマグレイトフルを分、攻撃者(対象者)の回避率を減らす。
            handle.SetFixedValue(whatModify.agi, -Doer.TenDayValues().GetValueOrZero(TenDayAbility.Pilmagreatifull));
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