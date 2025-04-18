﻿using System.Collections.Generic;
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
    bool RemoveOnDeath = false;
    

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
                    user.RemoveVitalLayer(bind.VitalLayerId);
                }

                //矛盾してる場合を警告する
                if(bind.GrantTiming == PassiveVitalTiming.OnRemove && bind.IsSurvivalCondition)
                {
                    Debug.LogWarning
                    ($"{_owner}に付与された{PassiveName}の追加HP({bind.VitalLayerId})はOnRemoveというパッシブが消されるタイミングで付与されますが、/n同時にパッシブの生存条件であるというプロパティも存在します。 パッシブは正常に削除された為、エラーは起きません。\n");
                }

                if(bind.GrantTiming == PassiveVitalTiming.OnRemove && bind.RemoveOnPassiveRemove)
                {
                    Debug.LogWarning
                    ($"{_owner}に付与された{PassiveName}の追加HP({bind.VitalLayerId})はOnRemoveというパッシブが消されるタイミングで付与されますが、同時にパッシブ終了時にも消される設定です。この場合、削除処理が優先され、追加HPは付与されませんでした。\n");
                }
            }
        }

        _owner = null;
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
    /// ユーザーの残っている生存条件のVitalLayerを返す
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    List<PassiveVitalLayerBinding> RemainingSurvivalVitalLayerBinding(BaseStates user)
    {
        //すべてのSurvivalConditionなLayerが消滅していたらRemove
        var survivalBinds = VitalLayers
            .Where(v => v.IsSurvivalCondition )
            .ToList();

        if (survivalBinds.Count > 0)//生存条件のvitalLayerBindがあるならば、
        {
            //user上に残っているレイヤーのIDを取得（HP > 0のもの）
            var livingLayerIds = user.VitalLayers
                .Where(lay => lay.LayerHP > 0)
                .Select(lay => lay.id)
                .ToList();
        
            // 生存条件を満たすバインディングだけをフィルタリング
            var remainingBindings = survivalBinds
                .Where(bind => livingLayerIds.Contains(bind.VitalLayerId))
                .ToList();
        
            if(remainingBindings.Count > 0)
            {
                return remainingBindings;
            }
        }
        return null;
    }    
    /// <summary>
    /// 生存条件のvitalLayerを残っているかどうか
    /// </summary>
    public bool HasRemainingSurvivalVitalLayer(BaseStates user)
    {
        var remainingLayers = RemainingSurvivalVitalLayerBinding(user);
        return remainingLayers != null && remainingLayers.Count > 0; // 残っているレイヤーがあればtrueを返す
    }
    /// <summary>
    /// 残っている生存条件のVitalLayerがパッシブ削除されたときに消される性質を持っているかどうか
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    bool RemainigSurvivalVitalLayer_Has_RemoveOnPassiveRemove(BaseStates user)
    {
        var remainingLayers = RemainingSurvivalVitalLayerBinding(user);
        if(remainingLayers.Count < 1) return false;//そもそも残っている生存条件のvitalLayerがない
        return remainingLayers.Any(lay => lay.RemoveOnPassiveRemove);
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
                if(HasRemainingSurvivalVitalLayer(user))//もし生存条件のvitalLayerがあれば
                {
                    Debug.Log($"{user}の{PassiveName}-パッシブがターン経過により削除されました。 生存条件の追加HPがありますが、ターン経過が優先されます。\n");

                    //もし残っている生存条件のvitalLayerが「パッシブが消えるときに一緒に消える性質を持っていなかったら」警告する
                    if(!RemainigSurvivalVitalLayer_Has_RemoveOnPassiveRemove(user))
                    {
                        Debug.LogWarning($"{user}の{PassiveName}-パッシブの残っている生存条件の追加HPにRemoveOnPassiveRemove性質がありません。これではターン経過削除の際、パッシブと密接なはずの追加HPなのに消えずに残りますが、\nよろしいでしょうか？\n");
                    }
                }
                return; // ここで処理打ち切り
            }
        }

        // 2) VitalLayer の生存条件チェック
        //    IsSurvivalCondition == true の Layer が "ひとつも残っていない" 場合
        if (VitalLayers != null)
        {
            if (!HasRemainingSurvivalVitalLayer(user))
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