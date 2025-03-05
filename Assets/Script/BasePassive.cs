using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEditor.Rendering;
using Unity.VisualScripting;

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
    public PassiveVitalLayerBinding DeepCopy()
    {
        return new PassiveVitalLayerBinding()
        {
            VitalLayerId = VitalLayerId,
            GrantTiming = GrantTiming,
            IsSurvivalCondition = IsSurvivalCondition,
            RemoveOnPassiveRemove = RemoveOnPassiveRemove
        };
    }
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

    /// <summary> このパッシブが有効な残り歩数（-1なら戦闘終了時に消え、0なら戦闘終了後歩行した瞬間に歩行効果なしに消え、1以上なら効果発生） </summary>
    public int DurationWalk = -1;
    /// <summary>
    /// 死亡時に消えるパッシブかどうか
    /// </summary>
    [SerializeField]
    bool RemoveOnDeath = true;
    

    /// <summary>
    /// パッシブが持つ追加HP　IDで扱う。
    /// </summary>
    public List<PassiveVitalLayerBinding> VitalLayers = new();


    /// <summary>
    ///     この値はパッシブの"重ね掛け" →&lt;思え鳥4&gt;
    /// </summary>
    public int PassivePower { get; private set; }

    protected BaseStates _owner;//オーナー

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

        _owner = user;
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

        _owner = null;
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
            DurationWalk --;
            if(DurationWalk <= 0)
            {
                user.RemovePassive(this);
                return;
            }
        }
    }
    public void UpdateDeathSurvival(BaseStates user)
    {
        if (RemoveOnDeath)//このパッシブが死亡時に消える物ならば
        {
            user.RemovePassive(this);
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

    /// <summary>
    /// 固定値でATKに作用する効果
    /// </summary>
    /// <returns></returns>
    public virtual float ATKFixedValueEffect()
    {
        return 0f;
    }
    /// <summary>
    /// 固定値でDEFに作用する効果
    /// </summary>
    /// <returns></returns>
    public virtual float DEFFixedValueEffect()
    {
        return 0f;
    }
    /// <summary>
    /// 固定値でEYEに作用する効果
    /// </summary>
    /// <returns></returns>
    public virtual float EYEFixedValueEffect()
    {
        return 0f;
    }
    /// <summary>
    /// 固定値でAGIに作用する効果
    /// </summary>
    /// <returns></returns>
    public virtual float AGIFixedValueEffect()
    {
        return 0f;
    }
    /// <summary>
    ///ATKに作用する補正値の倍率
    /// </summary>
    public virtual float ATKPercentageModifier()
    {
        return 1f;
    }
    /// <summary>
    ///DEFに作用する補正値の倍率
    /// </summary>
    public virtual float DEFPercentageModifier()
    {
        return 1f;
    }
    /// <summary>
    ///EYEに作用する補正値の倍率
    /// </summary>
    public virtual float EYEPercentageModifier()
    {
        return 1f;
    }
    /// <summary>
    ///AGIに作用する補正値の倍率
    /// </summary>
    public virtual float AGIPercentageModifier()
    {
        return 1f;
    }

    //これらで操り切れない部分は、直接baseStatesでのforeachでpassiveListから探す関数でゴリ押しすればいい。

    /// <summary>
    /// インスタンス共有するとターン処理が共有されてヤバいし、
    /// これやると自由に敵のパッシブを壊すこととか実装できるようになる
    /// </summary>
    public BasePassive DeepCopy()
    {
        var copy = new BasePassive();
        copy.OkImpression = OkImpression;
        copy.OkType = OkType;
        copy.IsCantACT = IsCantACT;
        copy.IsBad = IsBad;
        copy.MaxPassivePower = MaxPassivePower;
        copy.PassiveName = PassiveName;
        copy.ID = ID;
        copy.DurationTurn = DurationTurn;
        copy.DurationWalk = DurationWalk;
        
        foreach (var vital in VitalLayers)
        {
            copy.VitalLayers.Add(vital.DeepCopy());
        }
        copy.PassivePower = PassivePower;
        
        return copy;
    }
}