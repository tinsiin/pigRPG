using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEditor.Rendering;
using Unity.VisualScripting;
using static CommonCalc;


/// <summary>
/// 特定キャラクター（BaseStates）にのみ適用する補正値を保持するデータ構造
/// </summary>
public class CharacterSpecificModifier
{
    /// <summary>補正に反応するキャラ</summary>
    public BaseStates TargetCharacter;

    /// <summary>掛ける補正倍率、または加算する固定値</summary>
    public float Modifier;

    public CharacterSpecificModifier(BaseStates targetCharacter, float modifier)
    {
        TargetCharacter = targetCharacter;
        Modifier        = modifier;
    }
}
/// <summary>
/// パッシブの対象範囲
/// </summary>
public enum PassiveTargetScope { Allies, Enemies, Both }

/// <summary>
/// パッシブが付与/除去されるタイミングで追加HPをいつ追加するか
/// </summary>
public enum PassiveLinkedComponentTiming
{
    /// <summary> パッシブが追加(Apply)されたタイミング </summary>
    OnApply,

    /// <summary> パッシブが除去(Remove)されるタイミング </summary>
    OnRemove,

    /// <summary> 特に自動付与しない (コードで直接制御)</summary>
    Manual
}
/// <summary>
/// スキルのパッシブ付与性質の対象範囲とIDをまとめたもの。
/// </summary>
[Serializable]
public class ExtraPassiveBinding
{
    /// <summary> 対応するパッシブのID </summary>
    public int PassiveId;

    /// <summary> 対応するパッシブの対象範囲 </summary>
    public PassiveTargetScope TargetScope;
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
    public PassiveLinkedComponentTiming GrantTiming;

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
    /// パッシブ同士のリンク関係を表すクラス
    /// </summary>
    public class LinkedPassive
    {
        /// <summary>リンクされているパッシブ</summary>
        public BasePassive Passive { get; }

        /// <summary>親パッシブ(リスト実体のある方)が消えたときに一緒に消えるかどうか</summary>
        public bool RemoveWithMe { get; }

        /// <summary>リンクされたパッシブユーザー「に対してどれ程ダメージを食らわせるか」。</summary>
        public float DamageLinkRatio { get; }
        /// <summary>
        /// ダメージリンクのダメージが追加HPを通るかどうか。
        /// </summary>
        public bool LayerDamage { get; }

        public LinkedPassive(BasePassive passive, bool removeWithMe, float damageLinkRatio = 0, bool layerDamage = false)
        {
            Passive = passive;
            RemoveWithMe = removeWithMe;
            DamageLinkRatio = damageLinkRatio;
            LayerDamage = layerDamage;
        }
    }


/// <summary>
///     基礎状態の抽象クラス
/// </summary>
[Serializable]
public class BasePassive
{
    protected BattleManager manager => Walking.Instance.bm;
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
    /// このパッシブをキャンセルできるかどうか。
    /// </summary>
    public bool CanCancel;
    /// <summary>
    /// 永続パッシブかどうか
    /// </summary>
    public bool IsEternal;
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
    /// <summary>
    /// パッシブの名前の小さな版 (省略系UI表示用)
    /// </summary>
    public string SmallPassiveName;
    public int ID;

    /// <summary> このパッシブが有効な残りターン数（-1なら無効   設定値</summary>
    public int DurationTurn = -1;
    /// <summary>
    /// 実際にカウントに使用されるもの
    /// </summary>
    [HideInInspector]
    public int DurationTurnCounter;

    /// <summary> 
    // このパッシブが有効な残り歩数（-1なら戦闘終了時に消え、0なら戦闘終了後歩行した瞬間に歩行効果なしに消え、1以上なら効果発生）　 
    // 設定値 
    /// </summary>
    public int DurationWalk = -1;
    /// <summary>
    /// 実際にカウントに使用されるもの
    /// </summary>
    [HideInInspector]
    public int DurationWalkCounter;
    /// <summary>
    /// 死亡時に消えるパッシブかどうか
    /// </summary>
    [SerializeField]
    bool RemoveOnDeath = false;
    /// <summary>
    /// ダメージで消えるパッシブかどうか
    /// </summary>
    public bool RemoveOnDamage = false;

    /// <summary>
    /// 攻撃で消えるパッシブ
    /// </summary>
    [SerializeField]
    bool RemoveOnAttack = false;

    /// <summary>
    /// 割り込みカウンターで消えるパッシブかどうか
    /// </summary>
    [SerializeField]
    bool RemoveOnInterruptCounter = false;

    /// <summary>
    /// 攻撃後に消えるパッシブかどうか
    /// </summary>
    [SerializeField]
    bool RemoveOnAfterAttack = false;
    /// <summary>
    /// キャラ単位への攻撃後に消えるパッシブかどうか
    /// </summary>
    [SerializeField]
    bool RemoveOnAfterAttackToTarget = false;
    /// <summary>
    /// 完全単体選択系の攻撃の直前消えるパッシブかどうか
    /// </summary>
    [SerializeField]
    bool RemoveOnAfterPerfectSingleAttack = false;

    /// <summary>
    /// 前のめりでないなら消えるパッシブかどうか
    /// </summary>
    [SerializeField]
    bool RemoveOnNotVanguard = false;
    /// <summary>
    /// 味方や自分が攻撃を受けた後に消えるパッシブかどうか
    /// </summary>
    [SerializeField]
    bool RemoveOnAfterAlliesDamage = false;
    
    /// <summary>
    /// スキル実行時に付与するパッシブ
    /// </summary>
    public List<ExtraPassiveBinding> ExtraPassivesIdOnSkillACT = new();

    /// <summary>
    /// パッシブが持つ追加HP　IDで扱う。
    /// </summary>
    public List<PassiveVitalLayerBinding> VitalLayers = new();

    /// <summary>
    /// 自分が前のめりの時に、味方の前のめり交代を阻止するかどうか
    /// </summary>
    public bool BlockVanguardByAlly_IfImVanguard = false;


    /// <summary>
    ///     この値はパッシブの"重ね掛け" →&lt;思え鳥4&gt;
    /// </summary>
    public int PassivePower { get; private set; }

    /// <summary>
    /// オーナー
    /// </summary>
    public BaseStates _owner;

    /// <summary>
    /// 付与者
    /// </summary>
    public BaseStates _grantor;

    /// <summary>
    /// リンクするパッシブのリスト
    /// 基本的にはキャンセル連携など
    /// </summary>
    protected List<LinkedPassive> _passiveLinkList = new();
    public void SetPassiveLink(LinkedPassive link)
    {
        _passiveLinkList.Add(link);
    }

    /// <summary>
    /// PassivePowerを上書きする
    /// </summary>
    public void SetPassivePower(int value)
    {
        PassivePower = value;
    }
    /// <summary>
    ///     パッシブの残りターン数と歩数などの生存条件系を再補充
    /// </summary>
    protected void DurationRefill()
    {
        DurationTurnCounter = DurationTurn;
        DurationWalkCounter = DurationWalk;
    }

    /// <summary>
    ///     パッシブを重ね掛けした際呼び出す関数
    ///     PassivePowerを加算したり、それによる影響を操作(ここでの操作は主にターン数、歩数などの補充)
    /// </summary>
    /// <param name="addpoint"></param>
    public virtual void AddPassivePower(int addpoint)
    {
        PassivePower += addpoint;
        if (PassivePower > MaxPassivePower) PassivePower = MaxPassivePower; //設定値を超えたら設定値にする

        DurationRefill();//PassivePowerを増やしたため、念のため生存時間を補充
    }

    /// <summary>
    /// パッシブ付与時
    /// </summary>
    public virtual void OnApply(BaseStates user,BaseStates grantor)
    {
        DurationRefill();//生存時間を補充
        // ここで Timing == OnApply の VitalLayer を付与
        if (VitalLayers != null)
        {
            foreach (var bind in VitalLayers)
            {
                if (bind.GrantTiming == PassiveLinkedComponentTiming.OnApply)
                {
                    user.ApplyVitalLayer(bind.VitalLayerId);
                }
            }
        }

        if(BeVanguardOnApply)//前のめりになるパッシブなら
        {
            manager.BeVanguard(user);
        }

        //前のめりでないなら消えるパッシブなら、消す
        UpdateNotVanguardSurvival(user);

        _owner = user;
        _grantor = grantor;
}

    /// <summary>
    /// 割り込みカウンター発生時
    /// </summary>
    public virtual void OnInterruptCounter()
    {
        //メイン効果は派生クラスで実装して
        UpdateInterruptCounterSurvival();//パッシブ消えるかどうか
    }
    /// <summary>
    /// 攻撃後
    /// </summary>
    public virtual void OnAfterAttack()
    {
        //派生クラスで実装して
        UpdateAfterAttackSurvival();//パッシブ消えるかどうか

    }
    /// <summary>
    /// キャラ単位への攻撃後コールバック
    /// 命中段階を伴った処理
    /// </summary>
    /// <param name="UnderAttacker"></param>
    public virtual void OnAfterAttackToTargetWitheHitresult(BaseStates UnderAttacker,HitResult hitResult)
    {
        UpdateAfterAttackToTargetSurvival();
    }


    /// <summary>
    /// 完全単体選択系の攻撃の直後コールバック  キャラ単位で実行
    /// </summary>
    public virtual void OnAfterPerfectSingleAttack(BaseStates UnderAttacker)
    {
        UpdateAfterPerfectSingleAttackSurvival();
    }
    /// <summary>
    /// ダメージを受ける直前に
    /// </summary>
    public virtual void OnBeforeDamage(BaseStates Atker)
    {
        //派生クラスで実装して
        UpdateDamageSurvival();//パッシブが生存判定

    }
    /// <summary>
    /// ダメージ発動前
    /// ここでダメージの発動判定や、攻撃者、次カウンター予約などをする。
    /// 「被害者がパッシブ由来で防ぐってイメージね」
    /// </summary>
    public virtual bool OnBeforeDamageActivate(BaseStates attacker)
    {
        //基本パッシブは攻撃の発動を防がないから return true
        return true;
    }
    /// <summary>
    /// 味方や自分がダメージを食らった後に
    /// </summary>
    public virtual void OnAfterAlliesDamage(BaseStates Atker)
    {
        //派生クラスで実装して
        UpdateAfterAlliesDamageSurvival();//パッシブ消えるかどうか
    }
    /// <summary>
    /// 対全体攻撃限定で、攻撃が食らう前の発動是非、発動効果等
    /// </summary>
    public virtual void OnBeforeAllAlliesDamage(BaseStates Atker,ref UnderActersEntryList underActers)
    {
        //全体攻撃を食らったときに消えるって必要性がまだ考えられないから、
        //まだ何もなし。
    }
    /// <summary>
    /// ダメージ食らった後。
    /// </summary>
    public virtual void OnAfterDamage(BaseStates Atker, StatesPowerBreakdown damage)
    {
        //ダメージ時のパッシブ生存判定はOnBeforeDamageで行う

        //ダメージリンクの処理
        foreach(var link in _passiveLinkList)
        {
            if(link.DamageLinkRatio>0)//ダメージ率があるなら
            {
                //パッシブ所持者のレイザーダメージ
                link.Passive._owner.RatherDamage(damage,link.LayerDamage,link.DamageLinkRatio);
            }
        }
    }
    /// <summary>
    /// 次のターンに進むときのパッシブ効果
    /// </summary>
    public virtual void OnNextTurn()
    {
    }

    public virtual float OnDamageReductionEffect()
    {
        return DamageReductionRateOnDamage;
    }

    /// <summary>
    /// パッシブ側からオーナーのRemovePassiveを呼び出し、オーナーに消してもらう
    /// </summary>
    public void RemovePassiveByOwnerCall()
    {
        _owner.RemovePassive(this);
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
                if (bind.GrantTiming == PassiveLinkedComponentTiming.OnRemove)
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
                if(bind.GrantTiming == PassiveLinkedComponentTiming.OnRemove && bind.IsSurvivalCondition)
                {
                    Debug.LogWarning
                    ($"{_owner}に付与された{PassiveName}の追加HP({bind.VitalLayerId})はOnRemoveというパッシブが消されるタイミングで付与されますが、/n同時にパッシブの生存条件であるというプロパティも存在します。 パッシブは正常に削除された為、エラーは起きません。\n");
                }

                if(bind.GrantTiming == PassiveLinkedComponentTiming.OnRemove && bind.RemoveOnPassiveRemove)
                {
                    Debug.LogWarning
                    ($"{_owner}に付与された{PassiveName}の追加HP({bind.VitalLayerId})はOnRemoveというパッシブが消されるタイミングで付与されますが、同時にパッシブ終了時にも消される設定です。この場合、削除処理が優先され、追加HPは付与されませんでした。\n");
                }
            }
        }
        //リンクされたパッシブが消えるかどうか
        foreach(var passiveLink in _passiveLinkList)
        {
            if(passiveLink.RemoveWithMe)
            {//リンクされたパッシブがそのオーナーから消える。
                passiveLink.Passive.RemovePassiveByOwnerCall();
            }
        }

        _owner = null;
    }

    /// <summary>
    ///     毎ターンこのパッシブが生存するかどうか(戦闘中)
    ///     -1はそもそもターンで消えず、0にセットすれば一気に終わらされる(パッシブ制作の操作で有用)
    /// </summary>
    public virtual void UpdateTurnSurvival(BaseStates user)
    {
        if(IsEternal)
        {
            return;
        }

        // 1) ターン経過による自動解除 (Duration >= 0)　　戦闘ターンによる経過削除は、歩行ターンを無視する。
        if (DurationTurnCounter > 0)
        {
            DurationTurnCounter--;
            if (DurationTurnCounter <= 0)
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
    /// 歩行時にパッシブが生存するかどうか
    /// </summary>
    public virtual void UpdateWalkSurvival(BaseStates user)
    {
        if(IsEternal)
        {
            return;
        }
        
        DurationWalkCounter --;
        if(DurationWalkCounter <= 0)
        {
            user.RemovePassive(this);
            return;
        }
    }
    /// <summary>
    /// 死亡時にパッシブ消すプロパティがあるなら消す関数
    /// </summary>
    public void UpdateDeathSurvival(BaseStates user)
    {
        if (RemoveOnDeath)//このパッシブが死亡時に消える物ならば
        {
            user.RemovePassive(this);
        }
    }
    /// <summary>
    /// 前のめりでないなら消えるパッシブなら消す関数
    /// </summary>
    public void UpdateNotVanguardSurvival(BaseStates user)
    {
        if (RemoveOnNotVanguard && !manager.IsVanguard(user))//前のめりでなく、前のめり出ないなら消える性質があるのなら
        {
            user.RemovePassive(this);
        }
    }
    /// <summary>
    /// ダメージ時にパッシブ消すプロパティがあるなら消す関数
    /// </summary>
    void UpdateDamageSurvival()
    {
        if (RemoveOnDamage)//このパッシブがダメージで消える物ならば
        {
            DurationTurnCounter = 0;//TurnSurvivalで自動で消える。
        }
    }
    /// <summary>
    /// 攻撃時にパッシブ消えるなら消す関数
    /// </summary>
    void UpdateAttackSurvival()
    {
        if (RemoveOnAttack)
        {
            DurationTurnCounter = 0;
        }
    }
    /// <summary>
    /// 割り込みカウンター時にパッシブが消えるなら消す関数
    /// </summary>
    void UpdateInterruptCounterSurvival()
    {
        if (RemoveOnInterruptCounter)
        {
            DurationTurnCounter = 0;
        }
    }
    /// <summary>
    /// 攻撃後にパッシブ消えるなら消す関数
    /// </summary>
    void UpdateAfterAttackSurvival()
    {
        if (RemoveOnAfterAttack)
        {
            DurationTurnCounter = 0;
        }
    }
    /// <summary>
    /// キャラ単位への攻撃後コールバック
    /// </summary>
    void UpdateAfterAttackToTargetSurvival()
    {
        if (RemoveOnAfterAttackToTarget)
        {
            DurationTurnCounter = 0;
        }
    }
    /// <summary>
    /// 完全単体選択系の攻撃の直後で消えるパッシブなら消す関数
    /// </summary>
    void UpdateAfterPerfectSingleAttackSurvival()
    {
        if (RemoveOnAfterPerfectSingleAttack)
        {
            DurationTurnCounter = 0;
        }
    }
    /// <summary>
    /// 味方や自分が攻撃を受けた後にパッシブ消えるなら消す関数
    /// </summary>
    void UpdateAfterAlliesDamageSurvival()
    {
        if (RemoveOnAfterAlliesDamage)
        {
            DurationTurnCounter = 0;
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
    /// 行動時効果
    /// </summary>
    public virtual void ACTEffect()
    {

    }
    /// <summary>
    /// 実行時に前のめりになるかどうか
    /// </summary>
    [SerializeField]
    bool BeVanguardOnApply;

    //十日能力などから補正するならここの単純値ではなく、Effectの関数などを派生クラスでoverrideして直接書きます。
    [SerializeField]
    float _atkFixedValue;
    [SerializeField]
    float _defFixedValue;
    [SerializeField]
    float _eyeFixedValue;
    [SerializeField]
    float _agiFixedValue;
    /// <summary>
    /// パッシブの四大ステの固定補正値をセットする。
    /// </summary>
    public void SetFixedValue(whatModify what, float value)
{
    switch (what)
    {
        case whatModify.atk:
            _atkFixedValue = value;
            break;
        case whatModify.def:
            _defFixedValue = value;
            break;
        case whatModify.eye:
            _eyeFixedValue = value;
            break;
        case whatModify.agi:
            _agiFixedValue = value;
            break;
    }
}
    /// <summary>
    /// 固定値でATKに作用する効果
    /// </summary>
    public virtual float ATKFixedValueEffect()
    {
        return _atkFixedValue;
    }
    /// <summary>
    /// 固定値でDEFに作用する効果
    /// </summary>
    public virtual float DEFFixedValueEffect()
    {
        return _defFixedValue;
    }
    /// <summary>
    /// 固定値でEYEに作用する効果
    /// </summary>
    public virtual float EYEFixedValueEffect()
    {
        return _eyeFixedValue;
    }
    /// <summary>
    /// 固定値でAGIに作用する効果
    /// </summary>
    public virtual float AGIFixedValueEffect()
    {
        return _agiFixedValue;
    }

    [SerializeField]
    float _atkPercentageModifier=1f;
    [SerializeField]
    float _defPercentageModifier=1f;
    [SerializeField]
    float _eyePercentageModifier=1f;
    [SerializeField]
    float _agiPercentageModifier=1f;
    /// <summary>
    /// パッシブの四大ステの倍率補正値をセットする。
    /// </summary>
    public void SetPercentageModifier(whatModify what, float value)
    {
        switch (what)
        {
            case whatModify.atk:
                _atkPercentageModifier = value;
                break;
            case whatModify.def:
                _defPercentageModifier = value;
                break;
            case whatModify.eye:
                _eyePercentageModifier = value;
                break;
            case whatModify.agi:
                _agiPercentageModifier = value;
                break;
        }
    }
    /// <summary>
    ///ATKに作用する補正値の倍率
    /// </summary>
    public virtual float ATKPercentageModifier()
    {
        return _atkPercentageModifier;
    }
    /// <summary>
    ///DEFに作用する補正値の倍率
    /// </summary>
    public virtual float DEFPercentageModifier()
    {
        return _defPercentageModifier;
    }
    /// <summary>
    ///EYEに作用する補正値の倍率
    /// </summary>
    public virtual float EYEPercentageModifier()
    {
        return _eyePercentageModifier;
    }
    /// <summary>
    ///AGIに作用する補正値の倍率
    /// </summary>
    public virtual float AGIPercentageModifier()
    {
        return _agiPercentageModifier;
    }
    /// <summary>
    /// 特定の攻撃者に反応する防御力補正　パーセント補正
    /// 特定の攻撃者なんてインスペクタの登録時は分かんないから、「付与時に変更する値だよ」
    /// </summary>
    List<CharacterSpecificModifier> defensePercentageModifiersByAttacker = new();
    /// <summary>
    /// 特定の攻撃者に反応する防御力パーセンテージ補正 を攻撃者をチェックして返す補正。
    /// </summary>
    public float CheckDefencePercentageModifierByAttacker(BaseStates Attacker)
    {
        if (defensePercentageModifiersByAttacker == null || defensePercentageModifiersByAttacker.Count == 0)
            return -1f;


        
        foreach (var modifier in defensePercentageModifiersByAttacker)
        {
            if (modifier.TargetCharacter == Attacker)
            {
                return modifier.Modifier;
            }
        }
        return -1f;//-1なら呼び出し元で候補に加えられない
    }
    /// <summary>
    /// 防御力対象者限定補正リストに新しい要素を追加します。
    /// </summary>
    /// <param name="targetCharacter">対象となるキャラクター</param>
    /// <param name="modifierValue">適用する補正値</param>
    public void AddDefensePercentageModifierByAttacker(BaseStates targetCharacter, float modifierValue)
    {
        if (defensePercentageModifiersByAttacker == null)
            defensePercentageModifiersByAttacker = new List<CharacterSpecificModifier>();
            
        CharacterSpecificModifier newModifier = new CharacterSpecificModifier(targetCharacter, modifierValue);
        
        defensePercentageModifiersByAttacker.Add(newModifier);
    }

    /// <summary>
    /// 特定の攻撃者に反応する回避補正　パーセント補正
    /// 特定の攻撃者なんてインスペクタの登録時は分かんないから、「付与時に変更する値だよ」
    /// AGIには影響を与えない。  小数指定
    /// </summary>
    List<CharacterSpecificModifier> EvasionPercentageModifiersByAttacker = new();
    /// <summary>
    /// 特定の攻撃者に反応する回避率補正を成長させる
    /// Attackerを指定しないと、全てを成長させる。
    /// </summary>
    /// <param name="growValue">成長させる値</param>
    /// <param name="attacker">対象の攻撃者（nullの場合は全ての攻撃者に適用）</param>
    protected void GrowEvasionPercentageModifierByAttacker(float growValue, BaseStates attacker = null)
    {
        // リストがなかったり空の場合は終了
        if (EvasionPercentageModifiersByAttacker == null || EvasionPercentageModifiersByAttacker.Count == 0)
            return;

        foreach (var modifier in EvasionPercentageModifiersByAttacker)
        {
            // 攻撃者が指定されていないか、一致する攻撃者の場合に成長値を適用
            if (attacker == null || modifier.TargetCharacter == attacker)
            {
                modifier.Modifier += growValue;
            }
        }
    }
    /// <summary>
    /// 特定の攻撃者に反応する回避補正 を攻撃者をチェックして返す補正。
    /// </summary>
    public float CheckEvasionPercentageModifierByAttacker(BaseStates Attacker)
    {
        if (EvasionPercentageModifiersByAttacker == null || EvasionPercentageModifiersByAttacker.Count == 0)
        return -1f;

        foreach (var modifier in EvasionPercentageModifiersByAttacker)
        {
            if (modifier.TargetCharacter == Attacker)
            {
                return modifier.Modifier;
            }
        }
        return -1f;//-1なら呼び出し元で候補に加えられない
    }
    /// <summary>
    /// 特定の攻撃者に反応する回避率対象者限定補正リストに新しい要素を追加します。
    /// </summary>
    /// <param name="targetCharacter">対象となるキャラクター</param>
    /// <param name="modifierValue">適用する補正値</param>
    public void AddEvasionPercentageModifierByAttacker(BaseStates targetCharacter, float modifierValue)
    {
        if (EvasionPercentageModifiersByAttacker == null)
            EvasionPercentageModifiersByAttacker = new List<CharacterSpecificModifier>();
            
        CharacterSpecificModifier newModifier = new CharacterSpecificModifier(targetCharacter, modifierValue);
        
        EvasionPercentageModifiersByAttacker.Add(newModifier);
    }
    /// <summary>
    /// 0.0~1.0の範囲で、最大HPのその割合よりもダメージを食らうことは絶対にない。　禁忌のプロパティなので全然使うな！
    /// キャラクタ限定でない通常の値
    /// </summary>
    public float NormalDontDamageHpMinRatio = -1;// -1なら呼び出し元で候補に加えられない


    /// <summary>
    /// 特定の攻撃者に反応する最大HP割合ダメージ制限
    /// 特定の攻撃者からのダメージを最大HPの指定割合以上に受けないようにする
    /// </summary>
    List<CharacterSpecificModifier> dontDamageHpMinRatioByAttackers = new();

    /// <summary>
    /// 特定の攻撃者に反応する最大HP割合ダメージ制限を成長させる
    /// Attackerを指定しないと、全てを成長させる。
    /// </summary>
    protected void GrowDontDamageHpMinRatioByAttacker(float growValue,BaseStates Attacker = null)
    {
        //そもそもリストがなかったり名にも登録されてなかったら終わり
        if (dontDamageHpMinRatioByAttackers == null || dontDamageHpMinRatioByAttackers.Count == 0)
            return;


        
        foreach (var modifier in dontDamageHpMinRatioByAttackers)
        {
            if (Attacker == null || modifier.TargetCharacter == Attacker)//攻撃者が指定されてない　OR 一致するなら　渡された成長値を成長させる。
            {
                modifier.Modifier += growValue;
            }
        }
    }

    /// <summary>
    /// 最大HP割合ダメージ制限をチェックします。
    /// 特定の攻撃者に反応するものか、標準の値
    /// キャラ限定の方が優先されます。
    /// </summary>
    public float CheckDontDamageHpMinRatioNormalOrByAttacker(BaseStates attacker)
    {
        //リストがなかったら
        if (dontDamageHpMinRatioByAttackers == null || dontDamageHpMinRatioByAttackers.Count == 0)
            return NormalDontDamageHpMinRatio;//通常の値

        //リストがあったら
        foreach (var modifier in dontDamageHpMinRatioByAttackers)
        {
            if (modifier.TargetCharacter == attacker)//あてはまるものが在れば
            {
                return modifier.Modifier;//キャラ限定の値が返る
            }
        }
        return NormalDontDamageHpMinRatio; //なければ標準の値
    }

    /// <summary>
    /// 特定の攻撃者に反応する最大HP割合ダメージ制限を追加します。
    /// </summary>
    public void AddDontDamageHpMinRatioByAttacker(BaseStates attacker, float minHpRatio)
    {
        if (minHpRatio < 0f || minHpRatio > 1f)
        {
            Debug.LogWarning("minHpRatioは0.0から1.0の範囲で指定してください。");
            return;
        }

        if (dontDamageHpMinRatioByAttackers == null)
            dontDamageHpMinRatioByAttackers = new List<CharacterSpecificModifier>();
                
        CharacterSpecificModifier newModifier = new CharacterSpecificModifier(attacker, minHpRatio);
        dontDamageHpMinRatioByAttackers.Add(newModifier);
    }
    /// <summary>
    /// ダメージに掛ける形で減衰する  0~1.0
    /// -1だと実行時に使われる平均計算に使用されない。
    /// </summary>
    public float DamageReductionRateOnDamage = -1;
    /// <summary>
    /// ターゲットされる確率　詳細はobsidianメモを
    /// 100~-100　の範囲　0なら計算にあまり使われない
    /// </summary>
    public float TargetProbability = 0f;

    /// <summary>
    /// スキル発動率のバッキングフィールド
    /// 本来計算されない概念なので、初期値は100　範囲は0~100
    /// </summary>
    [SerializeField]
    float _skillActivationRate = 100f;
    /// <summary>
    /// スキル発動率セット関数
    /// </summary>
    public void SetSkillActivationRate(float value) { _skillActivationRate = value; }

    /// <summary>
    /// スキル発動率　0~100
    /// </summary>
    public virtual float SkillActivationRate()
    {
        return _skillActivationRate;
    }
    /// <summary>
    /// パッシブによるリカバリーターン補正値　　行動を遅らせるパッシブなら正の数、行動を早めるパッシブなら負の数と設定する。
    /// </summary>
    [SerializeField]
    protected int _maxRecoveryTurnModifier;
    /// <summary>
    /// パッシブによるリカバリターン/再行動クールタイムの設定値。
    /// </summary>
    public int MaxRecoveryTurnModifier()
    {
        return _maxRecoveryTurnModifier;
    }
    /// <summary>
    /// 回復率のバッキングフィールド
    /// </summary>
    [SerializeField]
    float _healEffectRate = 100;
    /// <summary>
    /// Healによる回復率(パッシブによる補正)
    /// 基本100% 0~100で設定する。
    /// </summary>
    public virtual float HealEffectRate()
    {
        return _healEffectRate;
    }
    /// <summary>
    /// 攻撃時ケレン行動パーセントのバッキングフィールド
    /// </summary>
    [SerializeField]
    float _attackACTKerenACTRate = KerenACTRateDefault;
    /// <summary>
    /// 攻撃時ケレン行動パーセント　0~100で設定する。
    /// </summary>
    public virtual float AttackACTKerenACTRate()
    {
        return _attackACTKerenACTRate;
    }
    /// <summary>
    /// 防御時ケレン行動パーセントのバッキングフィールド
    /// </summary>
    [SerializeField]
    float _defenceACTKerenACTRate = KerenACTRateDefault;
    /// <summary>
    /// 防御時ケレン行動パーセント　0~100で設定する。
    /// </summary>
    public virtual float DefenceACTKerenACTRate()
    {
        return _defenceACTKerenACTRate;
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