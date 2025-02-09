using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// パッシブが付与/除去されるタイミングで追加HPをいつ追加するか
/// </summary>
public enum PassiveVitalTiming
{
    /// <summary> パッシブが追加(Apply)されたタイミング </summary>
    OnApply,

    /// <summary> パッシブが除去(Remove)されるタイミング </summary>
    OnRemove,

    /// <summary> 特に自動付与しない (コードで直接制御)</summary>
    Manual
}
/// <summary>
/// パッシブが持つVitalLayerについてをまとめた情報
/// </summary>
[Serializable]
public class PassiveVitalLayerBinding
{
    /// <summary> 対応するVitalLayerのID </summary>
    public int VitalLayerId;

    /// <summary> 付与のタイミング </summary>
    public PassiveVitalTiming GrantTiming;

    /// <summary>
    /// このレイヤーが生存条件となるかどうか
    /// </summary>
    public bool IsSurvivalCondition;

    /// <summary> パッシブがRemoveされた際、このVitalLayerを消すならtrue </summary>
    public bool RemoveOnPassiveRemove;
}


/// <summary>
///     基礎状態の抽象クラス
/// </summary>
[Serializable]
public class BasePassive
{
    /// <summary>
    /// 適合するキャラ属性(精神属性)　
    /// </summary>
    public SpiritualProperty OkImpression;

    /// <summary>
    ///     適合する種別　
    /// </summary>
    public CharacterType OkType;

    /// <summary>
    /// 存在してる間行動できないかどうか。
    /// </summary>
    public bool IsCantACT;
    /// <summary>
    /// trueなら悪いパッシブで、SkillType.RemovePassiveでSkillHitCalcだけで解除される。
    /// falseならIsReactHitの判定(良いパッシブを無理やり外すっていう攻撃だからね)
    /// </summary>
    public bool IsBad;

    /// <summary>
    ///     PassivePowerの設定値
    /// </summary>
    public int MaxPassivePower = 0;
    /// <summary>
    /// パッシブの名前
    /// </summary>
    public string PassiveName;
    public int ID;

    /// <summary> このパッシブが有効な残りターン数（-1なら無効 </summary>
    public int DurationTurn = -1;

    /// <summary> このパッシブが有効な残り歩数（-1なら無効） </summary>
    public int DurationWalk = -1;

    /// <summary>
    /// パッシブが持つ追加HP　IDで扱う。
    /// </summary>
    public List<PassiveVitalLayerBinding> VitalLayers;


    /// <summary>
    ///     この値はパッシブの"重ね掛け" →&lt;思え鳥4&gt;
    /// </summary>
    public int PassivePower { get; private set; }

    /// <summary>
    /// PassivePowerを上書きする
    /// </summary>
    public void SetPassivePower(int value)
    {
        PassivePower = value;
    }

    /// <summary>
    ///     パッシブを重ね掛けする。
    /// </summary>
    /// <param name="addpoint"></param>
    public void AddPassivePower(int addpoint)
    {
        PassivePower += addpoint;
        if (PassivePower > MaxPassivePower) PassivePower = MaxPassivePower; //設定値を超えたら設定値にする
    }

    /// <summary>
    /// パッシブ付与時
    /// </summary>
    public void OnApply(BaseStates user)
    {
        // ここで Timing == OnApply の VitalLayer を付与
        if (VitalLayers != null)
        {
            foreach (var bind in VitalLayers)
            {
                if (bind.GrantTiming == PassiveVitalTiming.OnApply)
                {
                    user.ApplyVitalLayer(bind.VitalLayerId);
                }
            }
        }
    }

    /// <summary>
    /// パッシブがキャラからRemoveされる時 (OnRemove)
    /// </summary>
    public virtual void OnRemove(BaseStates user)
    {
        // ここで Timing == OnRemove の VitalLayer を付与
        if (VitalLayers != null)
        {
            foreach (var bind in VitalLayers)
            {
                if (bind.GrantTiming == PassiveVitalTiming.OnRemove)
                {
                    user.ApplyVitalLayer(bind.VitalLayerId);
                }

                //パッシブ削除時にこのレイヤーも消す場合 vitalLayerのRemoveOnPassiveRemoveで判断
                if (bind.RemoveOnPassiveRemove)
                {
                    // user から IDが一致するVitalLayerを検索して削除
                    // (BaseStates側にRemoveVitalLayer(id)など作っておく)
                    RemoveVitalLayerById(user, bind.VitalLayerId);
                }
            }
        }
    }
    /// <summary>
    /// 特定のキャラからidを通じて追加HPを消す。
    /// </summary>
    private void RemoveVitalLayerById(BaseStates user, int targetId)
    {
        // user.VitalLayers は IReadOnlyList かもしれないので、castや実体取得が必要
        var list = user.VitalLayers as List<BaseVitalLayer>;
        if (list == null) return;

        var index = list.FindIndex(l => l.id == targetId);
        if (index >= 0)
        {
            list.RemoveAt(index);
        }
    }
    /// <summary>
    /// 歩行時にパッシブが生存するかどうか
    /// </summary>
    public virtual void UpdateWalkSurvival(BaseStates user)
    {
        if (DurationWalk > 0)//歩行回数による自動解除
        {
            DurationWalk--;
            if(DurationWalk <= 0)
            {
                user.RemovePassive(this);
                return;
            }
        }
    }



    /// <summary>
    ///     毎ターンこのパッシブが生存するかどうか(戦闘中)
    /// </summary>
    public virtual void UpdateTurnSurvival(BaseStates user)
    {

        // 1) ターン経過による自動解除 (Duration >= 0)
        if (DurationTurn > 0)
        {
            DurationTurn--;
            if (DurationTurn <= 0)
            {
                // userのpassivelist からこのパッシブをRemove
                user.RemovePassive(this);
                return; // ここで処理打ち切り
            }
        }

        // 2) VitalLayer の生存条件チェック
        //    IsSurvivalCondition == true の Layer が "ひとつも残っていない" 場合
        if (VitalLayers != null)
        {
            bool needRemove = false;

            //すべてのSurvivalConditionなLayerが消滅していたらRemove
            var survivalIds = VitalLayers
                .Where(v => v.IsSurvivalCondition)
                .Select(v => v.VitalLayerId)
                .ToList();

            if (survivalIds.Count > 0)//生存条件のvitalLayerがあるならば、
            {
                // user上に「HP>0の対応レイヤー」があるか探す
                var existAny = user.VitalLayers
                    .Any(lay => survivalIds.Contains(lay.id) && lay.LayerHP > 0);

                if (!existAny) // 1つも残ってない
                {
                    needRemove = true;
                }
            }

            if (needRemove)
            {
                user.RemovePassive(this);
                return;
            }
        }

    }

    /// <summary>
    ///     歩行時効果　basestatesでapplypassiveで購読する
    /// </summary>
    public virtual void WalkEffect()
    {

    }

    /// <summary>
    ///     戦闘時効果　basestatesでapplypassiveで購読する
    /// </summary>
    public virtual void BattleEffect()
    {

    }
    /// <summary>
    ///行動時効果
    /// </summary>
    public virtual void ACTEffect()
    {

    }

    //↑三つで操り切れない部分は、直接baseStatesでのforeachでpassiveListから探す関数でゴリ押しすればいい。
}