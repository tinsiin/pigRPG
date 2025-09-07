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
/// <summary>
///     基礎ステータスのクラス　　クラスそのものは使用しないので抽象クラス
/// </summary>
[Serializable]
public abstract class BaseStates
{
    //UI
    WatchUIUpdate wui => WatchUIUpdate.Instance;
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


    /// <summary>
    /// このキャラの種別と一致してるかどうか
    /// </summary>
    public bool HasCharacterType(CharacterType type)
    {
        // Inspector の "Everything" は -1 でシリアライズされるため、全許可として扱う
        if ((int)type == -1) return true;
        return (MyType & type) == type;
    }/// <summary>
     /// このキャラの印象/キャラクタ属性と一致してるかどうか
     /// </summary>
    public bool HasCharacterImpression(SpiritualProperty imp)
    {
        // Inspector の "Everything" は -1 でシリアライズされるため、全許可として扱う
        if ((int)imp == -1) return true;
        return (MyImpression & imp) == imp;
    }

    protected BattleManager manager => Walking.Instance.bm;
    protected SchizoLog schizoLog => SchizoLog.Instance;
    /// <summary>
    /// キャラクターの被害記録
    /// </summary>
    public List<DamageData> damageDatas;
    /// <summary>
    /// キャラクターの対ひとりごとの行動記録
    /// </summary>
    public List<ACTSkillDataForOneTarget> ActDoOneSkillDatas;
    /// <summary>
    /// 直近の行動記録
    /// </summary>
    public ACTSkillDataForOneTarget RecentACTSkillData => ActDoOneSkillDatas[ActDoOneSkillDatas.Count - 1];
    /// <summary>
    /// アクション事のスキルデータ AttackChara単位で記録　= スキル一回に対して
    /// </summary>
    public List<ActionSkillData> DidActionSkillDatas = new();
    /// <summary>
    /// bm内にスキル実行した回数。
    /// </summary>
    protected int AllSkillDoCountInBattle => DidActionSkillDatas.Count;

    /// <summary>
    /// 乖離したスキルを利用したことによる苦悩での十日能力値下降処理
    /// </summary>
    protected void ResolveDivergentSkillOutcome()
    {
        //まずバトル内でのスキル実行回数が一定以上で発生
        if(AllSkillDoCountInBattle < 7) return;

        //終了時の精神属性による発生確率の分岐
        switch(MyImpression)
        {
            case SpiritualProperty.liminalwhitetile:
                // リーミナルホワイトタイル 0%
                return; // 常に発生しない
                
            case SpiritualProperty.kindergarden:
                // キンダーガーデン 95%
                if(!rollper(95f)) return;
                break;
                
            case SpiritualProperty.sacrifaith:
            case SpiritualProperty.cquiest:
            case SpiritualProperty.devil:
            case SpiritualProperty.doremis:
            case SpiritualProperty.pillar:
                // 自己犠牲・シークイエスト・デビル・ドレミス・支柱は全て100%
                // 100%発生するので何もしない（returnしない）
                break;
                
            case SpiritualProperty.godtier:
                // ゴッドティア 50%
                if(!rollper(50f)) return;
                break;
                
            case SpiritualProperty.baledrival:
                // ベールドライヴァル 70%
                if(!rollper(70f)) return;
                break;
                
            case SpiritualProperty.pysco:
                // サイコパス 20%
                if(!rollper(20f)) return;
                break;
                
            default:
                // その他の精神属性の場合はデフォルト処理
                break;
        }
    
        //乖離したスキルが一定％以上全体実行に対して使用されていたら
        var DivergenceCount = 0;
        foreach(var skill in DidActionSkillDatas)
        {
            if(skill.IsDivergence)
            {
                DivergenceCount++;
            }
        }
        //特定％以上乖離スキルがあったら発生する。
        if(AllSkillDoCountInBattle * 0.71 > DivergenceCount) return;

        //減少する十日能力値の計算☆☆☆

        //最後に使った乖離スキル
        BaseSkill lastDivergenceSkill = null;
        for(var i = DidActionSkillDatas.Count - 1; i >= 0; i--)//最後に使った乖離スキルに辿り着くまでループ
        {
            if(DidActionSkillDatas[i].IsDivergence)
            {
                lastDivergenceSkill = DidActionSkillDatas[i].Skill;
                break;
            }
        }

        //乖離スキルの全種類の印象構造の平均
        
        //まず全乖離スキルを取得する　同じのは重複しないようにhashset
        var DivergenceSkills = new HashSet<BaseSkill>();
        foreach(var skill in DidActionSkillDatas)
        {
            if(skill.IsDivergence)
            {
                DivergenceSkills.Add(skill.Skill);
            }
        }
        //全乖離スキルの印象構造の平均値
        var averageImpression = TenDayAbilityDictionary.CalculateAverageTenDayValues(DivergenceSkills);

        //「最後に使った乖離スキル」と「乖離スキル全体の平均値」の平均値を求める
        var DecreaseTenDayValue = TenDayAbilityDictionary.CalculateAverageTenDayDictionary(new[] { lastDivergenceSkill.TenDayValues(), averageImpression });
        DecreaseTenDayValue *= 1.2f;//定数で微増

        //自分の十日能力から減らす
        _baseTenDayValues -= DecreaseTenDayValue;
        Debug.Log($"乖離スキルの影響で、{CharacterName}の十日能力が減少しました。- {DecreaseTenDayValue}:現在値は{_baseTenDayValues}");
    }

    /// <summary>
    /// 現在持ってる対象者のボーナスデータ
    /// </summary>
    public TargetBonusDatas TargetBonusDatas = new();

    /// <summary>
    /// 直近の行動記録
    /// </summary>
    public ACTSkillDataForOneTarget RecentSkillData => ActDoOneSkillDatas[ActDoOneSkillDatas.Count - 1];
    /// <summary>
    /// 直近の被害記録
    /// </summary>
    public DamageData RecentDamageData => damageDatas[damageDatas.Count - 1];


    /// <summary>
    /// インスペクタからいじれないように、パッシブのmanagerから来たものがbaseStatesに保存されるpassive保存用
    /// </summary>
    List<BasePassive> _passiveList = new();
    public List<BasePassive> Passives => _passiveList;
    /// <summary>
    /// 初期所持のパッシブのIDリスト
    /// </summary>
    [SerializeField]
    List<int> InitpassiveIDList = new();

    /// <summary>
    /// パッシブの設計上、即座に適用するのではなく、NextTurnにてUpdateTurnSurvivalの後に適用するためのバッファリスト
    /// 詳しくは豚のパッシブを参照
    /// </summary>
    List<(BasePassive passive,BaseStates grantor)> BufferApplyingPassiveList = new();
    /// <summary>
    /// バッファのパッシブを追加する。
    /// </summary>
    void ApplyBufferApplyingPassive()
    {
        foreach(var passive in BufferApplyingPassiveList)
        {
            ApplyPassive(passive.passive,passive.grantor);
        }
        BufferApplyingPassiveList.Clear();//追加したからバッファ消す

    }
    /// <summary>
    /// すべてのスキルのバッファのスキルパッシブをスキルに適用する。
    /// </summary>
    void ApplySkillsBufferApplyingSkillPassive()
    {
        foreach(var skill in SkillList)
        {
            skill.ApplyBufferApplyingSkillPassive();
        }
    }

    /// <summary>
    /// 戦闘中のパッシブ追加は基本バッファに入れよう
    /// 付与者が自分自身ではないのなら、grantorに自分以外の付与者を渡す
    /// </summary>
    public void ApplyPassiveBufferInBattleByID(int id,BaseStates grantor = null)
    {
        if(grantor == null) grantor = this;//指定してないのなら、付与者は自分自身
        var status = PassiveManager.Instance.GetAtID(id).DeepCopy();//idを元にpassiveManagerから取得 ディープコピーでないとインスタンス共有される
        BufferApplyingPassiveList.Add((status,grantor));
    }
    



    List<BaseVitalLayer> _vitalLayerList = new();
    /// <summary>
    /// 初期所持のVitalLayerのIDリスト
    /// </summary>
    [SerializeField] List<int> InitVitalLaerIDList = new();


    /// <summary>
    /// 状態異常のリスト
    /// </summary>
    public IReadOnlyList<BasePassive> PassiveList => _passiveList;
    /// <summary>
    /// unityのインスペクタ上で設定したPassiveのIDからキャラが持ってるか調べる。
    /// </summary>
    public bool HasPassive(int id)
    {
        return _passiveList.Any(pas => pas.ID == id);
    }
    /// <summary>
    /// 自分が前のめりの時に味方を交代阻止するパッシブを一個でも持っているかどうか
    /// </summary>
    public bool HasBlockVanguardByAlly_IfImVanguard()
    {
        return _passiveList.Any(pas => pas.BlockVanguardByAlly_IfImVanguard);
    }

    /// <summary>
    /// 所持してるリストの中から指定したIDのパッシブを取得する。存在しない場合はnullを返す
    /// </summary>
    /// <param name="passiveId">取得したいパッシブのID</param>
    /// <returns>パッシブのインスタンス。存在しない場合はnull</returns>
    public BasePassive GetPassiveByID(int passiveId)
    {
        return _passiveList.FirstOrDefault(p => p.ID == passiveId);
    }
    /// <summary>
    /// バッファのパッシブリストから指定したIDのパッシブを取得する。存在しない場合はnullを返す
    /// もし、一回のターンで複数個の重複された"ID"のパッシブがあった場合、それの複数取得(そしてそれらへの何らかの適用)に対応出来てないよ...
    /// バッファシステムとパッシブの初期値変更に完璧に対応できていない。
    /// </summary>
    public BasePassive GetBufferPassiveByID(int passiveId)
    {
        return BufferApplyingPassiveList.FirstOrDefault(p => p.passive.ID == passiveId).passive;
    }

    /// <summary>
    ///     パッシブを適用
    /// </summary>
    public void ApplyPassiveByID(int id,BaseStates grantor = null)
    {
        if(grantor == null) grantor = this;//指定してないのなら、付与者は自分自身

        // マネージャ未初期化やID不正の安全ガード
        var pm = PassiveManager.Instance;
        if (pm == null)
        {
            Debug.LogError($"[ApplyPassiveByID] PassiveManager.Instance が null です。id={id}, character={CharacterName}. 初期化順序の前に呼ばれています。処理をスキップします。");
            return;
        }

        var template = pm.GetAtID(id);
        if (template == null)
        {
            Debug.LogError($"[ApplyPassiveByID] Passive ID が見つかりません。id={id}, character={CharacterName}. PassiveManager に未登録の可能性。処理をスキップします。");
            return;
        }

        var status = template.DeepCopy();// ディープコピーでないとインスタンス共有される

        // 条件(OkType,OkImpression) は既にチェック済みならスキップ
        if (!CanApplyPassive(status)){
            Debug.LogWarning($"{status.ID}のパッシブを付与しようとしましたが、条件が満たされていません。付与者は{grantor?.CharacterName ?? "自分自身"}です。OKType: {status.OkType}, OKImpression: {status.OkImpression}");
            return;
        }

        ApplyPassive(status,grantor);

    }
    /// <summary>
    /// パッシブが適合するか
    /// </summary>
    private bool CanApplyPassive(BasePassive passive)
    {
        if (!HasCharacterType(passive.OkType))       return false;
        if (!HasCharacterImpression(passive.OkImpression)) return false;
        return true;
    }
    public void ApplyPassive(BasePassive passive,BaseStates grantor = null)
    {
        Debug.Log($"ApplyPassive");
        if(grantor == null) grantor = this;//指定してないのなら、付与者は自分自身

        // 条件(OkType,OkImpression) は既にチェック済みならスキップ
        if (!CanApplyPassive(passive)) return;

        // すでに持ってるかどうか
        var existing = _passiveList.FirstOrDefault(p => p.ID == passive.ID);
        if (existing != null)
        {
            // 重ね掛け

            var pasPower = existing.PassivePower;//今のpassivepower
            RemovePassive(existing);
            ApplyPassive(passive,grantor);//新しいパッシブ側の変更された想定のプロパティを優先するため、入れ替える。
            //これは再帰的な処理だが、ループはしない。詳しくはAIに聞けよ
            
            passive.SetPassivePower(pasPower);//保存しといた前の時のPassivePowerを代入
            passive.AddPassivePower(1);//その上で挿げ替えた方のpassivepowerを増やす
        }
        else
        {
            // 新規追加
            _passiveList.Add(passive);
            // パッシブ側のOnApplyを呼ぶ
            passive.OnApply(this,grantor);
            Debug.Log($"{passive.ID}のパッシブを付与しました。付与者は{grantor?.CharacterName ?? "自分自身"}です。OKType: {passive.OkType}, OKImpression: {passive.OkImpression}");
        }

    }
    /// <summary>
    /// 割り込みカウンター発生時のパッシブ効果
    /// </summary>
    public bool PassivesOnInterruptCounter()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnInterruptCounter();
        }
        return true;
    }
    /// <summary>
    ///ダメージ直前のパッシブ効果
    /// </summary>
    public void PassivesOnBeforeDamage(BaseStates Atker)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnBeforeDamage(Atker);
        }
    }
    /// <summary>
    ///被害者がダメージを食らうその発動前
    /// 持ってるパッシブ中で一つでも　false(スキルは発動しない)があるなら、falseを返す
    /// </summary>
    public bool PassivesOnBeforeDamageActivate(BaseStates attacker)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            //一つでもfalseがあればfalseを返す
            if(!pas.OnBeforeDamageActivate(attacker)) return false;
        }
        return true;
    }
    /// <summary>
    ///ダメージ食らった後のパッシブ効果
    /// </summary>
    public void PassivesOnAfterDamage(BaseStates Atker, StatesPowerBreakdown damage)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterDamage(Atker, damage);
        }
    }

    /// <summary>
    /// 攻撃後のパッシブ効果
    /// </summary>
    public void PassivesOnAfterAttack()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterAttack();
        }
    }
    /// <summary>
    /// キャラ単位への攻撃後のパッシブ効果　　命中段階を伴った処理
    /// </summary>
    public void PassivesOnAfterAttackToTargetWithHitresult(BaseStates UnderAttacker,HitResult hitResult)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterAttackToTargetWitheHitresult(UnderAttacker,hitResult);
        }
    }

    /// <summary>
    /// 完全単体選択系の攻撃の直後の全パッシブ効果　一人に対する攻撃ごとに使用
    /// </summary>
    public void PassivesOnAfterPerfectSingleAttack(BaseStates UnderAttacker)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterPerfectSingleAttack(UnderAttacker);
        }
    }

    /// <summary>
    /// 味方や自分がダメージを食らった後のパッシブ効果
    /// </summary>
    public void PassivesOnAfterAlliesDamage(BaseStates Atker)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterAlliesDamage(Atker);
        }
    }
    /// <summary>
    /// 対全体攻撃限定で、攻撃が食らう前のパッシブ効果
    /// </summary>
    public void PassivesOnBeforeAllAlliesDamage(BaseStates Atker,ref UnderActersEntryList underActers)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnBeforeAllAlliesDamage(Atker,ref underActers);
        }
    }
    /// <summary>
    /// 次のターンに進むときのパッシブ効果
    /// </summary>
    public void PassivesOnNextTurn()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnNextTurn();
        }
    }
    
    /// <summary>
    /// パッシブをIDで除去
    /// </summary>
    void RemovePassiveByID(int id)
    {
        var passive = _passiveList.FirstOrDefault(p => p.ID == id);
        // パッシブがあるか確認
        if (_passiveList.Remove(passive))
        {
            // パッシブ側のOnRemoveを呼ぶ
            passive.OnRemove(this);
        }
    }

    /// <summary>
    /// パッシブを指定して除去
    /// </summary>
    public void RemovePassive(BasePassive passive)
    {
        // パッシブがあるか確認
        if (_passiveList.Remove(passive))
        {
            // パッシブ側のOnRemoveを呼ぶ
            passive.OnRemove(this);
            Debug.Log($"RemovePassive: {passive.ID} Character: {CharacterName}");
        }
    }
    /// <summary>
    /// パッシブをidで指定し、存在するかチェックしてから、除去する。
    /// </summary>
    public void TryRemovePassiveByID(int passiveId)
    {
    if (HasPassive(passiveId))
    {
        RemovePassiveByID(passiveId);
    }
    }

    /// <summary>
    /// 全スキルの、パッシブリストを全てが消えるかどうか
    /// </summary>
    void UpdateAllSkillPassiveSurvival()
    {
        //スキルのリスト
        foreach (var skill in SkillList)
        {
            //スキルが持つパッシブリストで回す
            foreach (var pas in skill.ReactiveSkillPassiveList)
            {
                pas.Update();
            }
        }
    }
    /// <summary>
    /// 全スキルの、パッシブリストを全てが歩行で消えるかどうか
    /// </summary>
    protected void UpdateAllSkillPassiveWalkSurvival()
    {
        //スキルのリスト
        foreach (var skill in SkillList)
        {
            //スキルが持つパッシブリストで回す
            foreach (var pas in skill.ReactiveSkillPassiveList)
            {
                pas.UpdateWalk();
            }
        }
    }

    /// <summary>
    /// 全パッシブのUpdateTurnSurvivalを呼ぶ ターン経過時パッシブが生存するかどうか
    /// NextTurnで呼び出す
    /// </summary>
    void UpdateTurnAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateTurnSurvival(this);
        }
    }
    /// <summary>
    /// 全パッシブが前のめり出ないときに消えるかどうかの判定をする
    /// RemoveOnNotVanguard = true 前のめり出ないなら消える
    /// </summary>
    void UpdateNotVanguardAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateNotVanguardSurvival(this);
        }
    }
    
    /// <summary>
    /// 全パッシブのUpdateWalkSurvivalを呼ぶ 歩行時パッシブが生存するかどうか
    /// </summary>
    protected void UpdateWalkAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateWalkSurvival(this);
        }
    }
    void UpdateDeathAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateDeathSurvival(this);
        }
    }
    /// <summary>
    /// 全パッシブの歩行時効果を呼ぶ
    /// </summary>
    protected void AllPassiveWalkEffect()
    {
        foreach (var pas in _passiveList)
        {
            if(pas.DurationWalkCounter > 0)
            {
                pas.WalkEffect();//歩行残存ターンが1以上でないと動作しない。
            }
        }
    }

    /// <summary>
    /// 持ってるパッシブの中で一番大きいDontDamageRatioを探し、その割合を用いてHPをクランプする
    /// レイザーダメージは貫通する。(この補正がない。)
    /// </summary>
    void DontDamagePassiveEffect(BaseStates attacker = null)
{
    float maxDontDamageHpMinRatio = 0;//使われない値は-1としてデフォ値が入ってるので、0ならば、比較時に0以上の値が入らない。
    foreach (var pas in _passiveList)
    {
        float ratio = attacker != null 
            ? pas.CheckDontDamageHpMinRatioNormalOrByAttacker(attacker)
            : pas.NormalDontDamageHpMinRatio;
            
        if (ratio > maxDontDamageHpMinRatio)
        {
            maxDontDamageHpMinRatio = ratio;
        }
    }
    
    // 「HPが下回らない最大HPの割合の最大値」より下回ってたらクランプ処理
    if (maxDontDamageHpMinRatio > 0)
    {
        int minHp = (int)(MaxHP * maxDontDamageHpMinRatio);
        HP = Math.Clamp(_hp, minHp, MaxHP);
    }
}

    /// <summary>
    /// ダメージに対して実行されるパッシブの減衰率
    /// 平均計減衰率が使われ、-1ならそもそも平均計算に使われない...
    /// </summary>
    void PassivesDamageReductionEffect(ref StatesPowerBreakdown damage)
    {
        // -1 を除外して有効な減衰率を収集
        var rates = _passiveList
            .Select(p => p.DamageReductionRateOnDamage)
            .Where(r => r >= 0f)
            .ToList();
        if (rates.Count == 0) return;

        // 平均減衰率を計算
        var avgRate = rates.Average();

        // ダメージに乗算
        damage *= avgRate;
    }
    
    /// <summary>
    /// 持ってるパッシブによるターゲットされる確率
    /// 平均化と実効化が行われる　1~100
    /// </summary>
    public float PassivesTargetProbability()
    {
        // -1 を除外して有効な確率を収集
        var rates = _passiveList
            .Select(p => p.TargetProbability)
            .Where(r => r >= 0f)
            .ToList();
        if (rates.Count == 0) return 0f;

        // 平均確率
        var avgRate = rates.Average();// -100～100 想定
        var sign = Mathf.Sign(avgRate);         // +1 or -1
        var absNorm = Mathf.Abs(avgRate) / 100f; // 0～1 正規化

        // FE式を適用（0.76閾値は 1.0 基準 → 0.76）
        float eff = absNorm <= 0.76f
            ? 2f * absNorm * absNorm
            : 1f - 2f * (1f - absNorm) * (1f - absNorm);

        // 符号を戻し、0～±100 にスケール
        return sign * eff * 100f;
    }
    /// <summary>
    /// 持ってるパッシブ全てのスキル発動率を平均化して返す
    /// このキャラクターのパッシブ由来のスキル発動率
    /// 全ての値が100% の場合　平均化しても100% その場合100として呼び出す元に返った時計算が省かれる
    /// </summary>
    /// <returns></returns>
    public float PassivesSkillActivationRate()
    {
        // -1 を除外して有効な確率を収集
        var rates = _passiveList
            .Select(p => p.SkillActivationRate())
            .Where(r => r >= 0f)//念のため0以上のみが入るようにする
            .ToList();
        if (rates.Count == 0) return 100f;//もし要素がなければ100を返す

        // 平均確率
        var avgRate = rates.Average();
        return avgRate;
    }
    /// <summary>
    /// パッシブによる回復補正率、下の方に補正される。
    /// </summary>
    public float PassivesHealEffectRate()
    {
        // -1 を除外して有効な確率を収集
        var rates = _passiveList
            .Select(p => p.HealEffectRate())
            .Where(r => r >= 0f) //念のため0以上のみが入るようにする
            .ToList();

        if (rates.Count == 0) return 100f; //もし要素がなければ100を返す

        // 要素を昇順に並べ替え
        var sortedRates = rates.OrderBy(r => r).ToList();
        
        // 考慮する要素数を計算 
        // 例:(小さい方から75%の個数、小数点以下切り上げ)
        // rates.Count = 1 => countToConsider = Ceiling(0.75) = 1
        // rates.Count = 2 => countToConsider = Ceiling(1.5)  = 2
        // rates.Count = 3 => countToConsider = Ceiling(2.25) = 3
        // rates.Count = 4 => countToConsider = Ceiling(3.0)  = 3
        int countToConsider = (int)Math.Ceiling(sortedRates.Count * 0.65f);

        // 上記の計算により、countToConsider は rates.Count > 0 なら必ず1以上になります。
        
        // 計算対象の要素を取得
        var takenRates = sortedRates.Take(countToConsider).ToList();
        
        // takenRatesが空になることは上記のロジックでは通常ありませんが、
        // リストが空でないことを保証してからAverageを呼び出すのがより安全です。
        if (!takenRates.Any()) 
        {
            // この状況は rates.Count > 0 の場合は発生しない想定です。
            // 万が一のためのフォールバック処理（例：元のリスト全体の平均や100fなど）
            // ここでは元のリスト全体の平均を返します。
            return rates.Average(); 
        }

        return takenRates.Average();
    }
    /// <summary>
    /// 攻撃行動時のパッシブ由来のケレン行動パーセント
    /// </summary>
    public float PassivesAttackACTKerenACTRate()
    {
        // デフォルト値以外の値を収集する。
        var rates = _passiveList
            .Select(p => p.AttackACTKerenACTRate())
            .Where(r => r != KerenACTRateDefault)
            .ToList();
        if (rates.Count == 0) return KerenACTRateDefault;//もし要素がなければデフォルト値を返す

        // 集めたデフォ値以外の値の中から、ランダムで一つ返す
        return RandomEx.Shared.GetItem(rates.ToArray());
    }
    public float PassivesDefenceACTKerenACTRate()
    {
        // デフォルト値以外の値を収集する。
        var rates = _passiveList
            .Select(p => p.DefenceACTKerenACTRate())
            .Where(r => r != KerenACTRateDefault)
            .ToList();
        if (rates.Count == 0) return KerenACTRateDefault;//もし要素がなければデフォルト値を返す

        // 集めたデフォ値以外の値の中から、ランダムで一つ返す
        return RandomEx.Shared.GetItem(rates.ToArray());
    }
    
    /// <summary>
    /// 指定された対象範囲のパッシブIDリストを返す
    /// スキル実行時に追加適用される。
    /// </summary>
    public List<int> ExtraPassivesIdOnSkillACT(allyOrEnemy whichAllyOrEnemy)
    {
        var result = new List<int>();
        foreach (var pas in _passiveList)
        {
            foreach(var bind in pas.ExtraPassivesIdOnSkillACT)
            {
                //敵味方どっちにも追加適用されるパッシブなら
                if(bind.TargetScope == PassiveTargetScope.Both)
                {
                    result.Add(bind.PassiveId);
                    continue;
                }

                //敵味方の区別があるなら
                switch(whichAllyOrEnemy)
                {
                    case allyOrEnemy.alliy:
                        if(bind.TargetScope == PassiveTargetScope.Allies)
                        {
                            result.Add(bind.PassiveId);
                        }
                        break;
                    case allyOrEnemy.Enemyiy:
                        if(bind.TargetScope == PassiveTargetScope.Enemies)
                        {
                            result.Add(bind.PassiveId);
                        }
                        break;
                }
            }
        }
        return result;
    }
    /// <summary>
    /// 与えられた補正倍率リストを“積と平均のブレンド”でひとつの倍率にまとめて返す
    /// 空なら1倍が返るので、補正対象の値に何の影響も与えない。
    /// </summary>
    private static float CalculateBlendedPercentageModifier(IEnumerable<float> factors)
    {
        const float alpha = 0.26f;  // 1:積寄り／0:平均寄り
        if (!factors.Any()) return 1f;//補正要素がなければパーセンテージ補正なしとして1倍を返す

        // 積と算術平均を計算
        float product = 1f;
        foreach (var f in factors) product *= f;
        float average = factors.Sum() / factors.Count();

        // α でブレンド
        return Mathf.Pow(product, alpha) * Mathf.Pow(average, 1f - alpha);
    }
    /// <summary>
    /// パッシブのパーセンテージ補正を返す  特別補正と違い一個一個掛ける
    ///  特別補正と違い、積と平均の中間を取る（ブレンド方式）　CalculateBlendedModifierのconstで操作
    /// </summary>
    public void PassivesPercentageModifier(whatModify mod,ref StatesPowerBreakdown value)
    {
        // 1) モディファイアを収集
        var factors = new List<float>();
        switch (mod)
        {
            case whatModify.atk:
                foreach (var pas in _passiveList) factors.Add(pas.ATKPercentageModifier());
                break;
            case whatModify.def:
                foreach (var pas in _passiveList) factors.Add(pas.DEFPercentageModifier());
                break;
            case whatModify.eye:
                foreach (var pas in _passiveList) factors.Add(pas.EYEPercentageModifier());
                break;
            case whatModify.agi:
                foreach (var pas in _passiveList) factors.Add(pas.AGIPercentageModifier());
                break;
            default:
                return;
        }
        if (factors.Count == 0) return;

        // 3) ブレンド乗算
        float blend = CalculateBlendedPercentageModifier(factors);
        value *= blend;
    }
    /// <summary>
    /// 持ってる全てのパッシブによる防御力パーセンテージ補正
    /// </summary>
    public float PassivesDefencePercentageModifierByAttacker()
    {
        // 各パッシブから「この攻撃者に対する防御倍率」を取得
        var factors = _passiveList
            .Select(p => p.CheckDefencePercentageModifierByAttacker(manager.Acter))
            .Where(mod => mod >= 0f);

        // ブレンドして返却　　
        return CalculateBlendedPercentageModifier(factors);
    }
    /// <summary>
    /// 持ってる全てのパッシブによる回避パーセンテージ補正
    /// </summary>
    public float PassivesEvasionPercentageModifierByAttacker()
    {
        // 各パッシブから「この攻撃者に対する回避倍率」を取得
        var factors = _passiveList
            .Select(p => p.CheckEvasionPercentageModifierByAttacker(manager.Acter))
            .Where(mod => mod >= 0f);

        // ブレンドして返却　　
        return CalculateBlendedPercentageModifier(factors);
    }
    
    public IReadOnlyList<BaseVitalLayer> VitalLayers => _vitalLayerList;
    /// <summary>
    /// インスペクタ上で設定されたIDを通じて特定の追加HPを持ってるか調べる
    /// </summary>
    public bool HasVitalLayer(int id)
    {
        return _vitalLayerList.Any(vit => vit.id == id);
    }
    public void RemoveVitalLayer(int id)
    {
        int index = _vitalLayerList.FindIndex(l => l.id == id);
        if (index >= 0)
        {
            _vitalLayerList.RemoveAt(index);
        }
    }

    public ThePower NowPower = ThePower.medium;//初期値は中

    /// <summary>
    /// NowPowerが一段階上がる。
    /// </summary>
    void Power1Up()
    {
        NowPower = NowPower switch
            {
                ThePower.lowlow => ThePower.low,
                ThePower.low => ThePower.medium,
                ThePower.medium => ThePower.high,
                ThePower.high => ThePower.high, // 既に最高値の場合は変更なし
                _ => NowPower//ここはdefault句らしい
            };

    }

    /// <summary>
    /// NowPowerが一段階下がる。
    /// </summary>
    void Power1Down()
    {
        NowPower = NowPower switch
            {
                ThePower.high => ThePower.medium,
                ThePower.medium => ThePower.low,
                ThePower.low => ThePower.lowlow,
                ThePower.lowlow => ThePower.lowlow, // 既に最低値の場合は変更なし
                _ => NowPower//ここはdefault句らしい
            };
    }
        /// <summary>
    /// キャラクターのパワーが歩行によって変化する関数
    /// </summary>
    protected void TransitionPowerOnWalkByCharacterImpression()
    {
        switch(MyImpression)
        {
            case SpiritualProperty.doremis:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(35))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(25))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(6))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(6))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        if(rollper(2.7f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(7.55f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                }
                break;
            case SpiritualProperty.pillar:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(2.23f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(5))
                        {
                            NowPower = ThePower.high;
                        }
                        if(rollper(20))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(6.09f))
                        {
                            NowPower = ThePower.medium;
                        }
                        if(rollper(15))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(8))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }

                break;
            case SpiritualProperty.kindergarden:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(25))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(31))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(28))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(25))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(20))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(30))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.liminalwhitetile:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(17))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(3))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(3.1f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(13))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(2))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(40))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.sacrifaith:
                switch(NowPower)
                {
                    case ThePower.high:
                        //不変
                    case ThePower.medium:
                        if(rollper(14))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(20))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(26))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.cquiest:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(14))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(3))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(3.1f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(13))
                        {
                            NowPower = ThePower.medium;
                        }

                        break;
                    case ThePower.lowlow:
                        if(rollper(4.3f))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.pysco:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(77.77f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(6.7f))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(3))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(90))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(10))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(80))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.godtier:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(4.26f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(3))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(30))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(28))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(8))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(100))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.baledrival:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(9))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(25))
                        {
                            NowPower = ThePower.high;
                        }
                        if(rollper(11))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(26.5f))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(8))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(50))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.devil:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(5))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(6))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(4.1f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(15))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(7))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(22))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
        }
    }
    /// <summary>
    /// ライバハル値
    /// </summary>
    public float Rivahal;
    /// <summary>
    /// TLOAスキルからのダメージ時、ライバハルの増える処理
    /// </summary>
    public void RivahalDream(BaseStates Atker,BaseSkill skill)
    {
        //スキルの印象構造で回す
        var baseValue = 0f;
        foreach(var tenDay in skill.TenDayValues())
        {
            var attackerValue = 0f;
            if(tenDay.Value > 0)//ゼロ除算対策
            {
                attackerValue = Atker.TenDayValues(true).GetValueOrZero(tenDay.Key) / tenDay.Value;
            }
            baseValue += attackerValue;
        }
        //精神補正100%を適用
        var AtkerTLOAValue = baseValue * GetSkillVsCharaSpiritualModifier(skill.SkillSpiritual,Atker).GetValue();
        Rivahal += AtkerTLOAValue;
    }
    
    [Header("4大ステの基礎基礎値")]
    public float b_b_atk = 4f;
    public float b_b_def = 4f;
    public float b_b_eye = 4f;
    public float b_b_agi = 4f;

    [Header("基本十日能力値テンプレ\n"+"初期設定用のテンプレ値です。実行時はこのテンプレからランタイム用辞書にコピーされます。(設定用クラスからランタイム用クラスにコピーされますする際に)\n"
    +"ランタイム用の辞書は非シリアライズのため、プレイ中のインスペクタには表示・反映されません、\nつまりい、「ランタイム用クラスにはこのtenDayTempleteは何も表示されないのが正常です。」")]
    /// <summary>
    /// 基本の十日能力値、インスペクタで設定する。
    /// </summary>
    [SerializeField]
    TenDayAbilityDictionary _tenDayTemplate = new();
    /// <summary>
    /// ランタイムで使用する基本十日能力値（インスペクタ非対象）。
    /// </summary>
    [NonSerialized]
    TenDayAbilityDictionary _baseTenDayValues = new();
    /// <summary>
    /// 基本十日能力値データ構造への参照を返すメソッド
    /// 要は十日能力値の値をいじる用途
    /// </summary>
    public TenDayAbilityDictionary BaseTenDayValues
    {
        get { return _baseTenDayValues; }
    }
    /// <summary>
    /// 読み取り専用の十日能力値、直接代入しないで
    /// スキル専属十日値を参照するかは引数で指定する
    /// </summary>
    public ReadOnlyIndexTenDayAbilityDictionary TenDayValues(bool IsSkillEffect)
    {
        //武器ボーナスを参照する。
        var IsBladeSkill = false;
        var IsMagicSkill = false;
        var IsTLOASkill = false;
        if(NowUseSkill != null && IsSkillEffect)
        {
            IsBladeSkill = NowUseSkill.IsBlade;
            IsMagicSkill = NowUseSkill.IsMagic;
            IsTLOASkill = NowUseSkill.IsTLOA;
        }
        var weaponBonus = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(IsBladeSkill, IsMagicSkill, IsTLOASkill)
            : new TenDayAbilityDictionary();

        if(NowUseWeapon == null)
        {
            Debug.LogError($"NowUseWeapon is null 武器にデフォルトで設定されるはずのIDが設定されてない。{CharacterName}");
        }
        // 素の十日能力（武器ボーナスなし）の合計値を出力
        //Debug.Log($"{CharacterName}の素の十日能力の合計値:{_baseTenDayValues.Values.Sum()}");
        var result = _baseTenDayValues + weaponBonus;
        //Debug.Log($"{CharacterName}の武器ボーナスを加えた十日能力の合計値:{result.Values.Sum()}");
        return new ReadOnlyIndexTenDayAbilityDictionary(result);
    }

    /// <summary>
    /// UI表示用: 基本値と各スキル特判の「追加分のみ」を行データとして返す。
    /// - 基本値: TenDayValues(false) の値 = 素の値 + Normal武器補正
    /// - Normal武器補正: GetTenDayAbilityDictionary(false,false,false) から該当キーの値
    /// - 各スキル特判補正: (Normal+該当特判) - Normal の差分のみ
    /// 返却順は TenDayValues(false) の列挙順を維持する。
    /// </summary>
    public struct TenDayDisplayRow
    {
        public string Name;          // 能力名（ToString）
        public float BaseValue;      // 基本表示値（= 素 + Normal）
        public float NormalBonus;    // Normal のみの武器補正
        public float TloaBonus;      // TLOA の追加分のみ
        public float BladeBonus;     // 刃物 の追加分のみ
        public float MagicBonus;     // 魔法 の追加分のみ
    }

    public List<TenDayDisplayRow> GetTenDayDisplayRows()
    {
        var rows = new List<TenDayDisplayRow>();

        // 表示順は TenDayValues(false) に従う
        var baseWithNormal = TenDayValues(false);

        // Normal と各特判のフル(=Normal+特判)を用意
        var normalDict = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(false, false, false)
            : new TenDayAbilityDictionary();
        var tloaFull = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(false, false, true)
            : new TenDayAbilityDictionary();
        var bladeFull = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(true, false, false)
            : new TenDayAbilityDictionary();
        var magicFull = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(false, true, false)
            : new TenDayAbilityDictionary();

        // 差分 = (Normal+特判) - Normal
        // Dictionary的アクセスのため TryGetValue で個別キーを参照
        foreach (var kv in baseWithNormal)
        {
            var key = kv.Key;
            var name = key.ToDisplayText();

            float baseValue = kv.Value;
            float normalB = 0f;
            float tloaB = 0f;
            float bladeB = 0f;
            float magicB = 0f;

            if (normalDict != null && normalDict.TryGetValue(key, out var n)) normalB = n;
            if (tloaFull != null && tloaFull.TryGetValue(key, out var tf)) tloaB = tf - normalB;
            if (bladeFull != null && bladeFull.TryGetValue(key, out var bf)) bladeB = bf - normalB;
            if (magicFull != null && magicFull.TryGetValue(key, out var mf)) magicB = mf - normalB;

            rows.Add(new TenDayDisplayRow
            {
                Name = name,
                BaseValue = baseValue,
                NormalBonus = normalB,
                TloaBonus = tloaB,
                BladeBonus = bladeB,
                MagicBonus = magicB,
            });
        }

        return rows;
    }

    /// <summary>
    /// ある程度の自信ブーストを記録する辞書
    /// </summary>
    protected Dictionary<TenDayAbility, int> ConfidenceBoosts = new Dictionary<TenDayAbility, int>();
    /// <summary>
    /// そのキャラクターを殺すまでに与えたダメージ
    /// </summary>
    Dictionary<BaseStates, float> DamageDealtToEnemyUntilKill = new Dictionary<BaseStates, float>();
    /// <summary>
    /// キャラクターを殺すまでに与えるダメージを記録する辞書に記録する
    /// </summary>
    /// <param name="dmg"></param>
    /// <param name="target"></param>
    void RecordDamageDealtToEnemyUntilKill(float dmg,BaseStates target)//戦闘開始時にそのキャラクターを殺すまでに与えたダメージを記録する辞書に記録する
    {
        if (DamageDealtToEnemyUntilKill.ContainsKey(target))
        {
            DamageDealtToEnemyUntilKill[target] += dmg;
        }
        else
        {
            DamageDealtToEnemyUntilKill[target] = dmg;
        }
    }

    /// <summary>
    /// 十日能力の総量
    /// </summary>
    public float TenDayValuesSum(bool IsSkillEffect) => TenDayValues(IsSkillEffect).Values.Sum();
    /// <summary>
    /// 所持してる十日能力の中から、ランダムに一つ選ぶ
    /// </summary>
    public TenDayAbility GetRandomTenDayAbility()
    {
        return RandomEx.Shared.GetItem(TenDayValues(true).Keys.ToArray());
    }
    

    /// <summary>
    /// 十日能力成長値を勝利ブースト用に記録する
    /// </summary>
    protected TenDayAbilityDictionary battleGain = new();
    /// <summary>
    /// 勝利時の強さの比率から成長倍率を計算する
    /// </summary>
    protected float CalculateVictoryBoostMultiplier(float ratio)
    {
        // (最大比率, 対応する倍) を小さい順に並べる
        (float maxRatio, float multiplier)[] boostTable = new[]
        {
            // 6割以下
            (0.6f, 2.6f),
            // 6割 < 7割
            (0.7f, 5f),
            // 7割 < 8割
            (0.8f, 6.6f),
            // 8割 < 9割
            (0.9f, 7f),
            // 9割 < 12割
            (1.2f, 10f),
            // 12割 < 15割
            (1.5f, 12f),
            // 15割 < 17割
            (1.7f, 13f),
            // 17割 < 20割
            (2.0f, 16f),
            // 20割 < 24割
            (2.4f, 18f),
            // 24割 < 26割 (特別ゾーン)
            (2.6f, 20f),
            // 26割 < 29割 (少し下がる設定)
            (2.9f, 19f),
            // 29割 < 34割
            (3.4f, 24f),
            // 34割 < 38割
            (3.8f, 30f),
            // 38割 < 40割
            (4.0f, 31f),
            // 40割 < 42割
            (4.2f, 34f),
            // 42割 < 48割
            (4.8f, 36f),
        };

        // テーブルを順に判定して返す
        foreach (var (maxVal, multi) in boostTable)
        {
            if (ratio <= maxVal)
            {
                return multi;
            }
        }

        // 最後: 48割を超える場合は (ratio - 7) 倍
        // ただし (ratio - 7) が 1 未満になる場合の扱いは要相談。
        // ここでは最低1倍を保証する例を示す:
        float result = ratio - 7f;
        if (result < 1f) result = 1f;
        return result;
    }

    /// <summary>
    /// 勝利時の十日能力ブースト倍化処理
    /// </summary>
    public void VictoryBoost(float ratio)
    {
        // 勝利時の強さの比率から成長倍率を計算する
        var multiplier = CalculateVictoryBoostMultiplier(ratio);

        // 一時的なリストにキーをコピーしてから処理
        var abilities = battleGain.Keys.ToList();

        foreach (var ability in abilities)
        {
            float totalGained = battleGain[ability]; // 戦闘中に合計で上がった量
            float extra = totalGained * (multiplier - 1f);//リアルタイムで加算済みなので倍率から1減らす
            
            // 追加で足す
            BaseTenDayValues[ability] += extra;

            // battleGainに今回のバトルで上がった分をすべて代入する。
            battleGain[ability] = totalGained * multiplier;
        }

    }
    
    /// <summary>
    /// 十日能力の成長する、能力値を加算する関数
    /// </summary>
    void TenDayGrow(TenDayAbility ability, float growthAmount)
    {
        if (BaseTenDayValues.ContainsKey(ability))
        {
            BaseTenDayValues[ability] += growthAmount;
        }
        else
        {
            BaseTenDayValues[ability] = growthAmount;
        }

        if (battleGain.ContainsKey(ability))
        {
            battleGain[ability] += growthAmount;//勝利用ブーストのために記録する。
        }
        else
        {
            // 存在しない場合は新しく追加
            battleGain[ability] = growthAmount;
        }
    }
    /// <summary>
    /// 十日能力が割合で成長する関数。既存の値に対して指定された割合分を加算する。
    /// BaseTenDayValues と battleGain の両方に適用される。
    /// </summary>
    /// <param name="ability">成長させる能力の種類</param>
    /// <param name="percent">成長させる割合（例: 0.1f で10%増加）</param>
    public void TenDayGrowByPercentOfCurrent(TenDayAbility ability, float percent)
    {
        if (BaseTenDayValues.ContainsKey(ability))
        {
            // 成長量を計算（現在の値 * 割合）
            float growthAmount = BaseTenDayValues[ability] * percent;

            if(growthAmount <= 0)//成長量が0以下なら何もしない
            {
                return;
            }

            // BaseTenDayValues に成長量を加算
            BaseTenDayValues[ability] += growthAmount;

            // battleGain にも成長量を加算または新規追加
            if (battleGain.ContainsKey(ability))
            {
                battleGain[ability] += growthAmount;
            }
            else
            {
                battleGain[ability] = growthAmount;
            }
        }
        // BaseTenDayValuesにキーが存在しない場合は何もしない（成長の基となる値がないため）
    }    
    /// <summary>
    /// 十日能力の下降する、能力値を減算する関数（0未満にはならない）
    /// </summary>
    void TenDayDecrease(TenDayAbility ability, float decreaseAmount)
    {
        if (BaseTenDayValues.ContainsKey(ability))
        {
            BaseTenDayValues[ability] = Mathf.Max(0, BaseTenDayValues[ability] - decreaseAmount);
        }
        // 存在しない場合は何もしない

        // 勝利時ブーストは成長値を増やす奴なので、ここでは使わない。
    }    
    /// <summary>
    /// 十日能力の下降する、能力値を減算する関数（0未満にはならない）
    /// 割合を指定する際、ちゃんと0~1のRatioで指定する。
    /// </summary>
    public void TenDayDecreaseByPercent(TenDayAbility ability, float percent)
    {
        TenDayDecrease(ability, BaseTenDayValues[ability] * percent);
    }    
    
    /// <summary>
    /// ヒット分で伸びる十日能力の倍率と使用する印象構造を記録する。
    /// </summary>
    List<(float Factor, TenDayAbilityDictionary growTenDay)> TenDayGrowthListByHIT=new();
    /// <summary>
    /// スキル成長 引数で渡す倍率と十日能力の辞書から直接増加値を調整する。
    /// スキルを直接渡すのではなく、柔軟性のため成長する十日能力の辞書を渡す形式にした。
    /// </summary>
    void GrowTenDayAbilityBySkill(float Factor,TenDayAbilityDictionary growTenDay)
    {
        const float topValueThresholdRate = 0.6f;//トップ能力のしきい値　どのくらいの大きいのと同じような能力値をスキルの十日能力として比較するかの指標
        //精神属性を実際に構成している十日能力が実際にスキルの十日能力値と比較されるイメージ

        const float distanceAttenuationLimit = 15f;//距離をグラデーション係数に変える際の、一定以上の距離から0にカットオフし成長しないようにする値

        //現在の精神属性を構成する十日能力の中で最も大きいものを算出
        float topTenDayValue = 0f;
        Debug.Log($"(スキル成長)精神属性のチェック : {MyImpression},キャラ:{CharacterName}");
        if(MyImpression == SpiritualProperty.none)
        {
            Debug.Log($"キャラクター{CharacterName}の精神属性がnoneなので成長できません。、");
            return;
        }
        foreach(var ten in SpritualTenDayAbilitysMap[MyImpression])
        {
            topTenDayValue = TenDayValues(true).GetValueOrZero(ten) > topTenDayValue ? TenDayValues(true).GetValueOrZero(ten) : topTenDayValue;
        }

        //トップ能力の60%以内の「十日能力の列挙体と値」を該当スキルの該当能力値との距離比較用にピックアップ
        List<(TenDayAbility,float)> pickupSpiritualTenDays = new List<(TenDayAbility,float)>();
        foreach(var ten in SpritualTenDayAbilitysMap[MyImpression])
        {
            var value = TenDayValues(true).GetValueOrZero(ten);
            if(value > topTenDayValue * topValueThresholdRate)
            {
                pickupSpiritualTenDays.Add((ten,value));
            }
        }

        
        
        foreach(var GrowSkillTenDayValue in growTenDay)//渡された十日能力の辞書に含まれてる全ての印象構造の十日能力分処理する。
        {
            // 加重平均用の変数
            float totalWeight = 0f;
            float weightedDistanceSum = 0f;
            
            // ピックアップした十日能力値全ての距離を加重平均する。
            foreach(var (myImpTen, value) in pickupSpiritualTenDays)
            {
                // 十日能力間の距離を計算
                float dist = TenDayAbilityPosition.GetDistance(myImpTen, GrowSkillTenDayValue.Key);
                
                // 能力値を重みとして使用
                totalWeight += value;
                weightedDistanceSum += dist * value;
            }
            
            // 加重平均距離を計算（totalWeightが0の場合は0とする）
            float averageDistance = totalWeight > 0 ? weightedDistanceSum / totalWeight : 0f;//全ての能力値がゼロだった場合の対策
            
            // 距離からグラデーション係数を計算（距離が遠いほど成長しにくくなる）
            float growthFactor = TenDayAbilityPosition.GetLinearAttenuation(averageDistance, distanceAttenuationLimit); // 15は最大距離の目安

            //グラデーション係数のデフォルト精神属性による救済処理
            if(growthFactor < 0.3f)
            {
                var isHelp = true;
                foreach(var ten in SpritualTenDayAbilitysMap[DefaultImpression])//デフォルト精神属性の構成する十日能力で回す.
                {
                    //スキルの回してる十日能力とデフォルト精神属性の回してる十日能力間の距離が10より多いなら
                    if(TenDayAbilityPosition.GetDistance(ten, GrowSkillTenDayValue.Key) > 10)
                    {
                        isHelp = false;//foreachで一回でも当てはまってしまうと、falseとなり、救済は発生しません、
                        break;
                    }
                }
                if(isHelp) growthFactor = 0.35f;
            }

            //ある程度の自信ブーストを適用する
            var confidenceBoost = 1.0f;
            if(ConfidenceBoosts.ContainsKey(GrowSkillTenDayValue.Key))//自信ブーストの辞書に今回の能力値が含まれていたら
            {
                confidenceBoost = 1.3f + TenDayValues(true).GetValueOrZero(TenDayAbility.Baka) * 0.01f;
            }
            
            // 成長量を計算（スキルの該当能力値と減衰係数から）
            float growthAmount = growthFactor * GrowSkillTenDayValue.Value * Factor * confidenceBoost; 
            // グラデーション係数 × スキルの該当能力値 × 引数から渡された倍率　× 自信ブースト
            
            // 十日能力値を更新
            TenDayGrow(GrowSkillTenDayValue.Key, growthAmount);
        }
    }

    public StatesPowerBreakdown b_AGI
    {
        get
        {
            // StatesPowerBreakdownのインスタンスを作成
            var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_agi);
            // 共通係数の適用（AgiPowerConfig）
            foreach (var kv in global::AgiPowerConfig.CommonAGI)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    breakdown.TenDayAdd(kv.Key, td * kv.Value);
                }
            }

            return breakdown;
        }
    }
    /// <summary>
    /// 攻撃力を十日能力とb_b_atkから計算した値
    /// 分解用にStatesPowerBreakdownとして返す
    /// </summary>
    public StatesPowerBreakdown b_ATK
    {
        get 
        {
            // StatesPowerBreakdownのインスタンスを作成
            var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_atk);
            
            // 共通係数の適用（AttackPowerConfig）
            foreach (var kv in AttackPowerConfig.CommonATK)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    breakdown.TenDayAdd(kv.Key, td * kv.Value);
                }
            }

            // プロトコル排他係数の適用
            var excl = AttackPowerConfig.GetExclusiveATK(NowBattleProtocol);
            foreach (var kv in excl)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    breakdown.TenDayAdd(kv.Key, td * kv.Value);
                }
            }
            
            return breakdown;
        }   
    }
    public StatesPowerBreakdown b_ATKSimulate(BattleProtocol simulateProtocol)
    {
        // StatesPowerBreakdownのインスタンスを作成
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_atk);

        // 共通係数の適用（AttackPowerConfig）
        foreach (var kv in AttackPowerConfig.CommonATK)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }

        // 指定プロトコルの排他係数の適用
        var excl = AttackPowerConfig.GetExclusiveATK(simulateProtocol);
        foreach (var kv in excl)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }

        return breakdown;
    }
    /// <summary>
    /// 戦闘規格ごとの「排他（プロトコル固有）加算」だけを返す攻撃内訳
    /// 基礎値や共通TenDay加算は含めない
    /// </summary>
    public StatesPowerBreakdown b_ATKProtocolExclusive(BattleProtocol protocol)
    {
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        var excl = AttackPowerConfig.GetExclusiveATK(protocol);
        foreach (var kv in excl)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }
        return breakdown;
    }

    /// <summary>
    /// 攻撃の排他（プロトコル固有）加算の合計値
    /// </summary>
    public float ATKProtocolExclusiveTotal(BattleProtocol protocol)
    {
        return b_ATKProtocolExclusive(protocol).Total;
    }
    /// <summary>
    /// 指定したAimStyleでの基礎防御力を計算する
    /// </summary>
    private StatesPowerBreakdown CalcBaseDefenseForAimStyle(AimStyle style)
    {
        // StatesPowerBreakdownのインスタンスを作成
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);

        // 共通係数の適用（DefensePowerConfig）
        foreach (var kv in DefensePowerConfig.CommonDEF)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }

        // AimStyle排他係数の適用
        var excl = DefensePowerConfig.GetExclusiveDEF(style);
        foreach (var kv in excl)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }
    
        return breakdown;
    }

    /// <summary>
    /// 防御の排他（AimStyle固有）加算のみを返す防御内訳
    /// 基礎値や共通TenDay加算は含めない
    /// </summary>
    public StatesPowerBreakdown b_DEFProtocolExclusive(AimStyle style)
    {
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        var excl = global::DefensePowerConfig.GetExclusiveDEF(style);
        foreach (var kv in excl)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }
        return breakdown;
    }
    /// <summary>
    /// 防御の排他（AimStyle固有）加算の合計値
    /// </summary>
    public float DEFProtocolExclusiveTotal(AimStyle style)
    {
        return b_DEFProtocolExclusive(style).Total;
    }

    /// <summary>
    /// 基礎攻撃防御　(大事なのは、基本的にこの辺りは超スキル依存なの)
    /// オプションのAimStyleに値を入れるとそのAimStyleでシミュレート
    /// </summary>
    /// <param name="SimulateAimStyle"></param>
    /// <returns></returns>
    public StatesPowerBreakdown b_DEF(AimStyle? SimulateAimStyle = null)
    {
       // StatesPowerBreakdownのインスタンスを作成
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_def);
        
        StatesPowerBreakdown styleBreakdown;
        
        if(SimulateAimStyle == null)
        {
            styleBreakdown = CalcBaseDefenseForAimStyle(NowDeffenceStyle);
        }
        else
        {
            styleBreakdown = CalcBaseDefenseForAimStyle(SimulateAimStyle.Value);
        }
    
        // スタイルによる防御力内訳を追加
        return breakdown + styleBreakdown;
    }
    public StatesPowerBreakdown b_EYE
    {
        get
        {
            // StatesPowerBreakdownのインスタンスを作成
            var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_eye);
            
            // 共通係数の適用（EyePowerConfig）
            foreach (var kv in global::EyePowerConfig.CommonEYE)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    breakdown.TenDayAdd(kv.Key, td * kv.Value);
                }
            }
            
            return breakdown;
        }
    }
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
    /// 装備中の武器
    /// </summary>
    [NonSerialized]
    public BaseWeapon NowUseWeapon;
    /// <summary>
    /// 初期所持してる武器のID
    /// </summary>
    public int InitWeaponID;
    /// <summary>
    /// 武器装備、武器から移る戦闘規格の変化
    /// </summary>
    public void ApplyWeapon(int ID)
    {
        if (WeaponManager.Instance == null)
        {
            Debug.LogError("WeaponManager.Instance is null");
            return;
        }
        NowUseWeapon = WeaponManager.Instance.GetAtID(ID);//武器を変更'
        NowBattleProtocol = NowUseWeapon.protocol;//戦闘規格の変更
    }
    /// <summary>
    /// 今のキャラの戦闘規格
    /// </summary>
    public BattleProtocol NowBattleProtocol;
    /// <summary>
    /// 狙い流れに対する防ぎ方プロパティ
    /// </summary>
    public AimStyle NowDeffenceStyle;

    /// <summary>
    /// 狙い流れ(AimStyle)に対する短期記憶
    /// </summary>
    private AimStyleMemory _aimStyleMemory;

    /// <summary>
    ///現在のの攻撃ターンで使われる
    /// </summary>
    [NonSerialized]
    public BaseSkill NowUseSkill;
    /// <summary>
    /// 逃げる選択肢を押したかどうか
    /// </summary>
    public bool SelectedEscape;
    /// <summary>
    /// 実行するスキルがRandomRangeの計算が用いられたかどうか。
    /// </summary>
    public bool SkillCalculatedRandomRange;
    /// <summary>
    /// 強制続行中のスキル　nullならその状態でないということ
    /// </summary>
    [NonSerialized]
    public BaseSkill FreezeUseSkill;
    /// <summary>
    /// 前回使ったスキルの保持
    /// </summary>
    private BaseSkill _tempUseSkill;
    /// <summary>
    /// スキル使用時の処理をまとめたコールバック
    /// </summary>
    public void SKillUseCall(BaseSkill useSkill)
    {
        //スキルのポインントの消費
        if(!TryConsumeForSkillAtomic(useSkill))
        {
            Debug.LogError(CharacterName + "のスキルのポインントの消費に失敗しました。" + CharacterName +"の" + (useSkill != null ? useSkill.SkillName : "<null>") + "を実行できません。"+
            "事前にポインント可否判定されてるはずなのにポインントが足りない。-SkillResourceFlowクラスとPs.OnlySelectActsやBattleAIBrainを確認して。" );
            return;
        }

        NowUseSkill = useSkill;//使用スキルに代入する
        Debug.Log(useSkill.SkillName + "を" + CharacterName +" のNowUseSkillにボタンを押して登録しました。");
        
        //ムーブセットをキャッシュする。連続攻撃でもそうでなくてもキャッシュ
        NowUseSkill.CashMoveSet();

        //今回選んだスキル以外のストック可能なスキル全てのストックを減らす。
        var list = SkillList.Where(skill =>  !ReferenceEquals(skill, useSkill) && skill.HasConsecutiveType(SkillConsecutiveType.Stockpile)).ToList();
        foreach(var stockSkill in list)
        {
            stockSkill.ForgetStock();
        }

    }
    /// <summary>
    /// スキルを連続実行した回数などをスキルのクラスに記録する関数
    /// </summary>
    /// <param name="useSkill"></param>
    public void SkillUseConsecutiveCountUp(BaseSkill useSkill)
    {
        useSkill.SkillHitCount();//スキルのヒット回数の計算

        if (useSkill == _tempUseSkill)//前回使ったスキルと同じなら
        {
            useSkill.DoConsecutiveCount++;//連続実行回数を増やす
            useSkill.HitConsecutiveCount++;//連続ヒット回数を増やす
        }
        else//違ったら
        {
            if (_tempUseSkill != null)//nullじゃなかったら
            {
                _tempUseSkill.DoConsecutiveCount = 0;//リセット
                _tempUseSkill.HitConsecutiveCount++;//連続ヒット回数をリセット　
            }
            useSkill.DoConsecutiveCount++;//最初の一回目として
            useSkill.HitConsecutiveCount++;//連続ヒット回数を増やす
        }
    }

    
    /// <summary>
    /// 最大ポイントは実HPの最大値を定数で割ったもの。　この定数はHPのスケールの変更などに応じて、適宜調整する
    /// </summary>
    public int MAXP => (int)_maxhp / PlayersStates.Instance.HP_TO_MaxP_CONVERSION_FACTOR;

    [SerializeField]
    int _p;//バッキングフィールド
    /// <summary>
    /// ポイント
    /// </summary>
    public int P
    {
        get
        {
            return Mathf.Clamp(_p, 0, MAXP);
        }
        set
        {
            if(value < 0)
            {
                Debug.LogWarning("[BaseStates.P] negative assign detected value=" + value + " name=" + CharacterName);
            }
            _p = Mathf.Clamp(value, 0, MAXP);
        }
    }
    
    /// <summary>
    /// パワーに応じて戦闘開始時にポイントを初期化する関数
    /// </summary>
    void InitPByNowPower()
    {
        var lowlowMinus = 3;
        if (rollper(37))lowlowMinus = 0;
        P = NowPower switch
        {
            ThePower.lowlow => 3 - lowlowMinus,
            ThePower.low => (int)(MAXP * 0.15),
            ThePower.medium => (int)(MAXP * 0.5),
            ThePower.high => (int)(MAXP * 0.7),
            _ => 0
        };
    }
    
    /// <summary>
    /// ノーマルポイント(P)の消費処理。
    /// 残量不足なら消費せず false を返す。cost <= 0 は何もしないで true。
    /// </summary>
    public bool TrySpendP(int cost)
    {
        if (cost <= 0) return true;
        if (P < cost) return false;
        P -= cost;
        return true;
    }
    
    // ================================
    // 属性ポイント（混合上限 + DropNew）
    // ================================
    
    /// <summary>
    /// 属性ポイント総上限（混合最大値）。MAXP にパワー倍率を乗じて決定。
    /// </summary>
    public int CombinedAttrPMax
    {
        get
        {
            return Mathf.RoundToInt(MAXP * GetAttrCombinedCapMultiplier(NowPower));
        }
    }
    
    /// <summary>
    /// 現在の属性ポイント総量（混合合計）。0..CombinedAttrPMax。
    /// </summary>
    public int CombinedAttrPTotal => _attrPTotal;
    public event Action<SpiritualProperty, int> OnAttrPChanged;
    
    // バッチ通知用フィールド
    private int _attrPBatchDepth;
    private readonly HashSet<SpiritualProperty> _attrPDirty = new HashSet<SpiritualProperty>();

    // 属性ポイント変更の通知（バッチ考慮）
    private void NotifyAttrPChanged(SpiritualProperty a)
    {
        if (_attrPBatchDepth > 0) _attrPDirty.Add(a);
        else OnAttrPChanged?.Invoke(a, GetAttrP(a));
    }

    // バッチ期間の開始/終了 (入れ子可)
    public IDisposable BeginAttrPBatch()
    {
        _attrPBatchDepth++;
        return new AttrPBatchToken(this);
    }
    private struct AttrPBatchToken : IDisposable
    {
        private readonly BaseStates _owner;
        public AttrPBatchToken(BaseStates owner) { _owner = owner; }
        public void Dispose()
        {
            if (_owner == null) return;
            _owner._attrPBatchDepth--;
            if (_owner._attrPBatchDepth <= 0)
            {
                _owner._attrPBatchDepth = 0;
                if (_owner._attrPDirty.Count > 0)
                {
                    foreach (var a in _owner._attrPDirty)
                    {
                        _owner.OnAttrPChanged?.Invoke(a, _owner.GetAttrP(a));
                    }
                    _owner._attrPDirty.Clear();
                }
            }
        }
    }

    /// <summary>
    /// 追加可能な残容量（DropNew ポリシーで使用）。
    /// </summary>
    public int CombinedAttrPRemaining => Mathf.Max(0, CombinedAttrPMax - _attrPTotal);
    
    // 内部ストレージ：属性別ポイント
    private readonly Dictionary<SpiritualProperty, int> _attrPMap = new();
    private int _attrPTotal;
    
    // 追加履歴（最新から削るためのLIFO管理）。
    private readonly List<AttrChunk> _attrAddHistory = new();
    
    private struct AttrChunk
    {
        public SpiritualProperty Attr;
        public int Amount;
        public AttrChunk(SpiritualProperty attr, int amount)
        {
            Attr = attr;
            Amount = amount;
        }
    }
    
    /// <summary>
    /// 現在の属性ポイント（該当属性が未登録なら0）。
    /// </summary>
    public int GetAttrP(SpiritualProperty attr)
    {
        return _attrPMap.TryGetValue(attr, out var v) ? v : 0;
    }
    
    /// <summary>
    /// 属性ポイントの追加（DropNew）：
    /// CombinedAttrPMax に達している場合、または残容量が0の場合は追加できない。
    /// 実際に追加できた量を返す。
    /// </summary>
    public int TryAddAttrP(SpiritualProperty attr, int amount)
    {
        if (amount <= 0) return 0;
        var space = CombinedAttrPMax - _attrPTotal;
        if (space <= 0) return 0; // DropNew: 満杯なら追加不可
        var added = amount > space ? space : amount;
        if (added <= 0) return 0;
        if (_attrPMap.ContainsKey(attr)) _attrPMap[attr] += added; else _attrPMap[attr] = added;
        _attrPTotal += added;
        // 履歴に追加
        _attrAddHistory.Add(new AttrChunk(attr, added));
        NotifyAttrPChanged(attr);
        ValidateAttrPInvariants();
        return added;
    }

    // 履歴削減ヘルパー（同一属性の最新から削る）
    private void ReduceLatestForAttr(SpiritualProperty attr, int amount)
    {
        var toReduce = amount;
        for (int i = _attrAddHistory.Count - 1; i >= 0 && toReduce > 0; i--)
        {
            if (_attrAddHistory[i].Attr != attr) continue;
            var chunk = _attrAddHistory[i];
            var reduce = Math.Min(chunk.Amount, toReduce);
            chunk.Amount -= reduce;
            toReduce -= reduce;
            if (chunk.Amount > 0)
            {
                _attrAddHistory[i] = chunk;
            }
            else
            {
                _attrAddHistory.RemoveAt(i);
            }
        }
    }

    // 履歴削減ヘルパー（全属性横断で最新から削る）
    private void ReduceLatestAcrossAllAttrs(int amount, HashSet<SpiritualProperty> changed)
    {
        var overflow = amount;
        for (int i = _attrAddHistory.Count - 1; i >= 0 && overflow > 0; i--)
        {
            var chunk = _attrAddHistory[i];
            var reduce = Math.Min(chunk.Amount, overflow);
            // 属性マップからも同量減らす
            if (_attrPMap.TryGetValue(chunk.Attr, out var cur))
            {
                var left = cur - reduce;
                if (left > 0) _attrPMap[chunk.Attr] = left; else _attrPMap.Remove(chunk.Attr);
                changed?.Add(chunk.Attr);
            }
            _attrPTotal -= reduce;
            overflow -= reduce;
            chunk.Amount -= reduce;
            if (chunk.Amount > 0)
            {
                _attrAddHistory[i] = chunk; // 一部だけ削れたら更新
            }
            else
            {
                _attrAddHistory.RemoveAt(i); // 0になったら履歴から除去
            }
        }
    }
    
    /// <summary>
    /// 属性ポイントの消費。該当属性のプールからのみ消費（他属性の補填はしない）。
    /// 成功時 true、残量不足で失敗時 false。
    /// 履歴は「同属性の最新から（LIFO）」で減らし、上限縮小時の DropNew ポリシー
    /// （最新から削る）と整合が取れるようにする。
    /// </summary>
    public bool TrySpendAttrP(SpiritualProperty attr, int cost)
    {
        if (cost <= 0) return true;
        var have = GetAttrP(attr);
        if (have < cost) return false;
        var remain = have - cost;
        if (remain > 0)
        {
            _attrPMap[attr] = remain;
        }
        else
        {
            _attrPMap.Remove(attr);
        }
        _attrPTotal -= cost;
        if (_attrPTotal < 0) _attrPTotal = 0;
        // 履歴から「同属性の新しいものから」減算して整合を保つ
        ReduceLatestForAttr(attr, cost);
        NotifyAttrPChanged(attr);
        ValidateAttrPInvariants();
        return true;
    }
    
    /// <summary>
    /// スキルに設定されたノーマルPと属性Pを「一括でアトミックに」消費する。
    /// 事前に全コストを再チェックし、次に属性→最後にノーマルの順で清算する。
    /// 途中で失敗した場合は、既に消費した属性Pをロールバックして false を返す。
    /// </summary>
    public bool TryConsumeForSkillAtomic(BaseSkill skill)
    {
        if (skill == null)
        {
            Debug.LogError("TryConsumeForSkillAtomic: skill が null です");
            return false;
        }

        // 要求値を正規化
        int reqNormal = Mathf.Max(0, skill.RequiredNormalP);
        var reqAttr = skill.RequiredAttrP;

        // 第1段階: 事前チェック（副作用なし）
        if (P < reqNormal)
        {
            Debug.LogError($"TryConsumeForSkillAtomic: ノーマルP不足 need={reqNormal} have={P}");
            return false;
        }
        if (reqAttr != null)
        {
            foreach (var kv in reqAttr)
            {
                int need = Mathf.Max(0, kv.Value);
                if (need == 0) continue;
                var haveAttr = GetAttrP(kv.Key);
                if (haveAttr < need)
                {
                    Debug.LogError($"TryConsumeForSkillAtomic: 属性P不足 attr={kv.Key} need={need} have={haveAttr}");
                    return false;
                }
            }
        }

        // 第2段階: 実消費（属性→ノーマル）。失敗時は属性をロールバック。
        using (BeginAttrPBatch())
        {
            var consumedAttr = new List<KeyValuePair<SpiritualProperty, int>>();
            if (reqAttr != null)
            {
                foreach (var kv in reqAttr)
                {
                    int need = Mathf.Max(0, kv.Value);
                    if (need == 0) continue;
                    if (!TrySpendAttrP(kv.Key, need))
                    {
                        // ロールバック（ここまでに消費した属性Pを戻す）
                        foreach (var back in consumedAttr)
                        {
                            // DropNew仕様のため最新に戻るが、総量整合のために加算でよい
                            var restored = TryAddAttrP(back.Key, back.Value);
                            if (restored != back.Value)
                            {
                                Debug.LogWarning($"TryConsumeForSkillAtomic: 属性Pロールバック不足 attr={back.Key} expected={back.Value} restored={restored}");
                            }
                        }
                        Debug.LogError($"TryConsumeForSkillAtomic: 属性P消費に失敗 attr={kv.Key} need={need}（事前可否との差異）");
                        return false;
                    }
                    consumedAttr.Add(new KeyValuePair<SpiritualProperty, int>(kv.Key, need));
                }
            }

            if (!TrySpendP(reqNormal))
            {
                // ノーマルで失敗時も属性をロールバック
                foreach (var back in consumedAttr)
                {
                    var restored = TryAddAttrP(back.Key, back.Value);
                    if (restored != back.Value)
                    {
                        Debug.LogWarning($"TryConsumeForSkillAtomic: 属性Pロールバック不足 attr={back.Key} expected={back.Value} restored={restored}");
                    }
                }
                Debug.LogError($"TryConsumeForSkillAtomic: ノーマルP消費に失敗 need={reqNormal} have={P}（事前可否との差異）");
                return false;
            }

            return true;
        }
    }
    
    /// <summary>
    /// 属性ポイントを全消去。
    /// </summary>
    public void ClearAllAttrP()
    {
        var keys = _attrPMap.Keys.ToArray();
        _attrPMap.Clear();
        _attrPTotal = 0;
        _attrAddHistory.Clear();
        using (BeginAttrPBatch())
        {
            foreach (var k in keys)
            {
                NotifyAttrPChanged(k);
            }
        }
        ValidateAttrPInvariants();
    }
    
    /// <summary>
    /// 上限が縮小した場合などに、総量が CombinedAttrPMax を超えているとき余剰を削る。
    /// DropNew ポリシーに従い、_attrAddHistory を基準に
    /// 「直近に追加された順（最新から）」で横断的（全属性）に減らす。
    /// 履歴と属性マップ（_attrPMap）および総量（_attrPTotal）を同期して更新する。
    /// </summary>
    public void ReclampAttrPToCapDropNew()
    {
        // 仕様: 上限が縮小した際は「直近に追加された順（最新から）」で削減する。
        var cap = CombinedAttrPMax;
        if (_attrPTotal <= cap) return;
        var overflow = _attrPTotal - cap;
        var changed = new HashSet<SpiritualProperty>();
        using (BeginAttrPBatch())
        {
            ReduceLatestAcrossAllAttrs(overflow, changed);
            if (_attrPTotal < 0) _attrPTotal = 0;
            foreach (var a in changed)
            {
                NotifyAttrPChanged(a);
            }
        }
        ValidateAttrPInvariants();
    }

    // 開発時の不変条件チェック（Editor のみ）
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void ValidateAttrPInvariants()
    {
        try
        {
            int sumMap = 0;
            foreach (var v in _attrPMap.Values) sumMap += v;
            if (_attrPTotal != sumMap)
            {
                Debug.LogError($"[BaseStates] Invariant violation: _attrPTotal({_attrPTotal}) != sumMap({sumMap}) name={CharacterName}");
            }
            int sumHist = 0;
            for (int i = 0; i < _attrAddHistory.Count; i++)
            {
                var amt = _attrAddHistory[i].Amount;
                if (amt < 0)
                {
                    Debug.LogError($"[BaseStates] History negative amount at {i} name={CharacterName}");
                }
                sumHist += Math.Max(0, amt);
            }
            if (_attrPTotal < 0)
            {
                Debug.LogError($"[BaseStates] _attrPTotal negative: {_attrPTotal} name={CharacterName}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BaseStates] ValidateAttrPInvariants exception: {ex}");
        }
    }

    // ================================
    // UI表示用スナップショット
    // ================================
    [Serializable]
    public struct AttrPSnapshotEntry
    {
        public SpiritualProperty Attr;
        public int Amount;
        public float RatioOfTotal; // CombinedAttrPTotalに対する割合
        public float RatioOfMax;   // CombinedAttrPMaxに対する割合
    }

    /// <summary>
    /// UI表示用に属性ポイント内訳を取得する。既定は量の多い順でソート。
    /// </summary>
    public List<AttrPSnapshotEntry> GetAttrPSnapshot(bool sortDesc = true)
    {
        var total = (float)CombinedAttrPTotal;
        var max = (float)CombinedAttrPMax;
        IEnumerable<KeyValuePair<SpiritualProperty, int>> seq = _attrPMap;
        if (sortDesc) seq = seq.OrderByDescending(kv => kv.Value);
        var list = new List<AttrPSnapshotEntry>();
        foreach (var kv in seq)
        {
            var entry = new AttrPSnapshotEntry
            {
                Attr = kv.Key,
                Amount = kv.Value,
                RatioOfTotal = total > 0 ? kv.Value / total : 0f,
                RatioOfMax = max > 0 ? kv.Value / max : 0f
            };
            list.Add(entry);
        }
        return list;
    }
    
    /// <summary>
    /// UI表示用に属性ポイント内訳を「最近追加された属性が左（新しい→古い）」の順序で取得する。
    /// 並び順は、直近追加履歴（_attrAddHistory）における「最後に登場したインデックス」が大きいほど新しいとみなす。
    /// 履歴に存在しない属性は最も古い（末尾）に配置し、同順位は量の多い順で安定化する。
    /// </summary>
    public List<AttrPSnapshotEntry> GetAttrPSnapshotRecentFirst()
    {
        var total = (float)CombinedAttrPTotal;
        var max = (float)CombinedAttrPMax;

        // 各属性の「最後に追加された位置」を記録（大きいほど新しい）
        var lastIndex = new Dictionary<SpiritualProperty, int>();
        for (int i = 0; i < _attrAddHistory.Count; i++)
        {
            var a = _attrAddHistory[i].Attr;
            lastIndex[a] = i; // 最後に代入された値が最終出現位置になる
        }

        var ordered = _attrPMap
            .Where(kv => kv.Value > 0)
            .Select(kv => new
            {
                Attr = kv.Key,
                Amount = kv.Value,
                Index = lastIndex.TryGetValue(kv.Key, out var idx) ? idx : -1
            })
            .OrderByDescending(x => x.Index)      // 新しい→古い
            .ThenByDescending(x => x.Amount);     // 同順位は量の多い順

        var list = new List<AttrPSnapshotEntry>();
        foreach (var x in ordered)
        {
            list.Add(new AttrPSnapshotEntry
            {
                Attr = x.Attr,
                Amount = x.Amount,
                RatioOfTotal = total > 0 ? x.Amount / total : 0f,
                RatioOfMax = max > 0 ? x.Amount / max : 0f
            });
        }
        return list;
    }
    
    // ================================
    // スキル使用時: ポイント→属性ポイント変換
    // ================================
    
    // ノーマル使用時の倍率レンジ
    private const float NORMAL_TO_ATTR_MIN = 1.48f;
    private const float NORMAL_TO_ATTR_MAX = 1.66f;
    
    /// <summary>
    /// スキル使用時、消費したポイントをスキル属性の属性ポイントへ変換して加算する。
    /// - 複合時: ノーマル消費が1以上含まれていれば、全消費合計にノーマル倍率を適用。
    /// - それ以外: 同属性/他属性ごとに倍率を適用して加算。
    /// 返り値は実際に追加できた量（DropNewにより減衰の可能性あり）。
    /// 本関数はポイントの消費自体は行わない（呼び出し側で既に消費済み前提）。
    /// </summary>
    public int ConvertAndAddAttrPOnSkillUse(
        SpiritualProperty skillAttr,
        int spentNormalP,
        Dictionary<SpiritualProperty, int> spentAttrP)
    {
        if (spentAttrP == null) spentAttrP = new Dictionary<SpiritualProperty, int>();
        // 未定義属性の使用は全て例外化
        if (!IsAttrDefinedForConversion(skillAttr))
            throw new ArgumentOutOfRangeException(nameof(skillAttr), $"Unsupported SpiritualProperty for conversion: {skillAttr}");
        foreach (var key in spentAttrP.Keys)
        {
            if (!IsAttrDefinedForConversion(key))
                throw new ArgumentOutOfRangeException(nameof(spentAttrP), $"Unsupported SpiritualProperty for conversion: {key}");
        }
        int totalAdded = 0;
        
        // 複合時の特例: ノーマル>0なら全てノーマル倍率一括処理
        if (spentNormalP > 0)
        {
            var sumAll = spentNormalP + spentAttrP.Values.Sum();
            var m = GetNormalToAttrMultiplier();
            var minted = Mathf.FloorToInt(sumAll * m);
            totalAdded += TryAddAttrP(skillAttr, minted);
            return totalAdded;
        }
        
        // ノーマルなし: 各ソース別に同/他属性倍率で変換
        int mintedTotal = 0;
        
        // 1) 同属性ソース
        if (spentAttrP.TryGetValue(skillAttr, out var sameSpent) && sameSpent > 0)
        {
            var sameCap = GetSameAttrCap(skillAttr);
            var sameEff = sameCap * GetTenDayConvFactor(true, skillAttr, skillAttr);
            var add = Mathf.FloorToInt(sameSpent * Mathf.Max(0f, sameEff));
            mintedTotal += add;
        }
        
        // 2) 他属性ソース
        foreach (var kv in spentAttrP)
        {
            var fromAttr = kv.Key;
            if (fromAttr == skillAttr) continue;
            var amount = kv.Value;
            if (amount <= 0) continue;
            var otherCap = GetOtherAttrCap(skillAttr, fromAttr);
            var otherEff = otherCap * GetTenDayConvFactor(false, skillAttr, fromAttr);
            var add = Mathf.FloorToInt(amount * Mathf.Max(0f, otherEff));
            mintedTotal += add;
        }
        
        if (mintedTotal > 0)
        {
            totalAdded += TryAddAttrP(skillAttr, mintedTotal);
        }
        return totalAdded;
    }
    
    /// <summary>
    /// スキル詠唱後、ヒット結果に応じてポイントの変換/返金を一括で処理する。
    /// - Hit: 通常の変換をそのまま適用
    /// - Critical: 変換ロジックの結果（総ミント量）に 1.6 倍を掛けて加算
    /// - Graze: 変換ロジックの結果（総ミント量）にさらに 0.3 倍を掛けて加算
    /// - CompleteEvade: 消費基礎量の 0.4 を返金（ノーマルは P へ、属性は TryAddAttrP）
    /// 返り値は「属性ポイントに実際に加算できた量（変換分のみ。返金は含まない)」。
    /// </summary>
    private const float CRIT_OUTPUT_SCALE = 1.60f;    // クリティカル時、変換後の総量に対する倍率
    private const float GRAZE_OUTPUT_SCALE = 0.30f;   // かすり時、変換後の総量に対する倍率
    private const float EVADE_REFUND_SCALE = 0.40f;   // 完全回避時、返金倍率
    
    // ========= キャスト単位の命中集約API =========
    private HitResult _castBestHitOutcome = HitResult.none;
    private static int GetHitRank(HitResult hr)
    {
        switch (hr)
        {
            case HitResult.Critical: return 4;
            case HitResult.Hit: return 3;
            case HitResult.Graze: return 2;
            case HitResult.CompleteEvade: return 1;
            default: return 0;
        }
    }
    public void BeginSkillHitAggregation()
    {
        _castBestHitOutcome = HitResult.none;
    }
    public void AggregateSkillHit(HitResult hr)
    {
        if (GetHitRank(hr) > GetHitRank(_castBestHitOutcome)) _castBestHitOutcome = hr;
    }
    public HitResult EndSkillHitAggregation()
    {
        var res = _castBestHitOutcome;
        _castBestHitOutcome = HitResult.none;
        return res;
    }

    /// <summary>
    /// 詠唱したスキルとヒット結果に応じて、ポイントの精算を行う高水準API。
    /// - skill.RequiredNormalP / skill.RequiredAttrP を実消費相当として利用します。
    /// - 将来的に動的コストになる場合は、実消費を引数に取るオーバーロードを用意してください。
    /// </summary>
    public int SettlePointsAfterSkillOutcome(BaseSkill skill, HitResult outcome)
    {
        if (skill == null) return 0;
        var skillAttr = skill.SkillSpiritual;

        int spentNormalP = Mathf.Max(0, skill.RequiredNormalP);
        var spentAttrP = skill.RequiredAttrP != null
            ? new Dictionary<SpiritualProperty, int>(skill.RequiredAttrP)
            : new Dictionary<SpiritualProperty, int>();

        return SettlePointsAfterSkillOutcome(skillAttr, spentNormalP, spentAttrP, outcome);
    }

    /// <summary>
    /// 実消費量を明示的に渡して精算するAPI。
    /// </summary>
    public int SettlePointsAfterSkillOutcome(
        SpiritualProperty skillAttr,
        int spentNormalP,
        Dictionary<SpiritualProperty, int> spentAttrP,
        HitResult outcome)
    {
        if (spentAttrP == null) spentAttrP = new Dictionary<SpiritualProperty, int>();

        // 返金パスは skillAttr 未定義でも成立するが、変換パスでは禁止
        bool canConvert = IsAttrDefinedForConversion(skillAttr);

        switch (outcome)
        {
            case HitResult.Hit:
            {
                if (!canConvert)
                {
                    Debug.LogWarning($"SettlePointsAfterSkillOutcome: undefined skillAttr={skillAttr}. Skip convert.");
                    return 0;
                }
                return ConvertAndAddAttrPOnSkillUse(skillAttr, spentNormalP, spentAttrP);
            }
            case HitResult.Critical:
            {
                if (!canConvert)
                {
                    Debug.LogWarning($"SettlePointsAfterSkillOutcome: undefined skillAttr={skillAttr}. Skip convert(critical).");
                    return 0;
                }
                // クリティカルは「変換結果」に 1.6 倍を掛ける
                int minted = ComputeAttrMintedAmount(skillAttr, spentNormalP, spentAttrP);
                int scaled = Mathf.FloorToInt(minted * Mathf.Max(0f, CRIT_OUTPUT_SCALE));
                if (scaled <= 0) return 0;
                return TryAddAttrP(skillAttr, scaled);
            }
            case HitResult.Graze:
            {
                if (!canConvert)
                {
                    Debug.LogWarning($"SettlePointsAfterSkillOutcome: undefined skillAttr={skillAttr}. Skip convert(graze).");
                    return 0;
                }
                // かすりは「変換結果」に 0.3 倍を掛ける仕様
                int minted = ComputeAttrMintedAmount(skillAttr, spentNormalP, spentAttrP);
                int scaled = Mathf.FloorToInt(minted * Mathf.Max(0f, GRAZE_OUTPUT_SCALE));
                if (scaled <= 0) return 0;
                return TryAddAttrP(skillAttr, scaled);
            }
            case HitResult.CompleteEvade:
            {
                // 完全回避は返金のみ
                int refunded = 0;
                int refundN = Mathf.FloorToInt(spentNormalP * Mathf.Max(0f, EVADE_REFUND_SCALE));
                if (refundN > 0)
                {
                    P += refundN;
                }
                if (spentAttrP != null)
                {
                    foreach (var kv in spentAttrP)
                    {
                        int refundA = Mathf.FloorToInt(kv.Value * Mathf.Max(0f, EVADE_REFUND_SCALE));
                        if (refundA > 0) refunded += TryAddAttrP(kv.Key, refundA);
                    }
                }
                // 返り値は「変換で増えた量」を返す契約のため、返金は 0 扱い
                return 0;
            }
            case HitResult.none:
            default:
                return 0;
        }
    }

    /// <summary>
    /// 変換ロジックの「計算のみ」を行い、実際には追加しない。総ミント量を返す。
    /// ConvertAndAddAttrPOnSkillUse と同一の計算パスを辿る。
    /// </summary>
    private int ComputeAttrMintedAmount(
        SpiritualProperty skillAttr,
        int spentNormalP,
        Dictionary<SpiritualProperty, int> spentAttrP)
    {
        if (spentAttrP == null) spentAttrP = new Dictionary<SpiritualProperty, int>();
        if (!IsAttrDefinedForConversion(skillAttr))
            throw new ArgumentOutOfRangeException(nameof(skillAttr), $"Unsupported SpiritualProperty for conversion: {skillAttr}");
        foreach (var key in spentAttrP.Keys)
        {
            if (!IsAttrDefinedForConversion(key))
                throw new ArgumentOutOfRangeException(nameof(spentAttrP), $"Unsupported SpiritualProperty for conversion: {key}");
        }

        // 複合時の特例: ノーマル>0なら全てノーマル倍率一括処理
        if (spentNormalP > 0)
        {
            var sumAll = spentNormalP + spentAttrP.Values.Sum();
            var m = GetNormalToAttrMultiplier();
            return Mathf.FloorToInt(sumAll * m);
        }

        int mintedTotal = 0;
        // 1) 同属性ソース
        if (spentAttrP.TryGetValue(skillAttr, out var sameSpent) && sameSpent > 0)
        {
            var sameCap = GetSameAttrCap(skillAttr);
            var sameEff = sameCap * GetTenDayConvFactor(true, skillAttr, skillAttr);
            var add = Mathf.FloorToInt(sameSpent * Mathf.Max(0f, sameEff));
            mintedTotal += add;
        }
        // 2) 他属性ソース
        foreach (var kv in spentAttrP)
        {
            var fromAttr = kv.Key;
            if (fromAttr == skillAttr) continue;
            var amount = kv.Value;
            if (amount <= 0) continue;
            var otherCap = GetOtherAttrCap(skillAttr, fromAttr);
            var otherEff = otherCap * GetTenDayConvFactor(false, skillAttr, fromAttr);
            var add = Mathf.FloorToInt(amount * Mathf.Max(0f, otherEff));
            mintedTotal += add;
        }
        return mintedTotal;
    }
    /// <summary>
    /// ノーマルポインントの属性変換率を乱数から取得する関数。
    /// </summary>
    private float GetNormalToAttrMultiplier()
    {
        // RandomExtensions の Shared RNG を使用
        return RandomEx.Shared.NextFloat(NORMAL_TO_ATTR_MIN, NORMAL_TO_ATTR_MAX);
    }
    
    /// <summary>
    /// 十日能力による補正係数（0..1）を返す。現状は1.0（＝上限いっぱい）で固定。
    /// 後で TenDay 系の値によりスケールさせる実装に差し替える前提。
    /// </summary>
    private float GetTenDayConvFactor(bool isSameAttr, SpiritualProperty skillAttr, SpiritualProperty fromAttr)
    {
        return 1f;
    }
    
    /// <summary>
    /// 同属性変換率の上限（表の値）。未指定属性は例外。
    /// </summary>
    private float GetSameAttrCap(SpiritualProperty attr)
    {
        if (!IsAttrDefinedForConversion(attr))
            throw new ArgumentOutOfRangeException(nameof(attr), $"Unsupported SpiritualProperty for conversion: {attr}");
        switch (attr)
        {
            case SpiritualProperty.liminalwhitetile: return 0.999f;
            case SpiritualProperty.kindergarden: return 0.2f;
            case SpiritualProperty.sacrifaith: return 0f;
            case SpiritualProperty.cquiest: return 0f;
            case SpiritualProperty.devil: return 0.2f;
            case SpiritualProperty.doremis: return 0f;
            case SpiritualProperty.pillar: return 0.7f;
            case SpiritualProperty.godtier: return 0.3f;
            case SpiritualProperty.baledrival: return 0.4f;
            case SpiritualProperty.pysco: return 0f;
        }
        throw new ArgumentOutOfRangeException(nameof(attr), $"Unsupported SpiritualProperty for conversion: {attr}");
    }
    
    /// <summary>
    /// 他属性変換率の上限（表の値 + 例外）。未指定属性は例外。
    /// 基本は skillAttr（スキル属性）ごとの一律倍率。必要な例外は fromAttr で上書き。
    /// </summary>
    private float GetOtherAttrCap(SpiritualProperty skillAttr, SpiritualProperty fromAttr)
    {
        if (!IsAttrDefinedForConversion(skillAttr))
            throw new ArgumentOutOfRangeException(nameof(skillAttr), $"Unsupported SpiritualProperty for conversion: {skillAttr}");
        if (!IsAttrDefinedForConversion(fromAttr))
            throw new ArgumentOutOfRangeException(nameof(fromAttr), $"Unsupported SpiritualProperty for conversion: {fromAttr}");
        
        float baseCap;
        switch (skillAttr)
        {
            case SpiritualProperty.liminalwhitetile: baseCap = 0f; break;
            case SpiritualProperty.kindergarden: baseCap = 1.3f; break;
            case SpiritualProperty.sacrifaith: baseCap = 0f; break;
            case SpiritualProperty.cquiest: baseCap = 0.3f; break;
            case SpiritualProperty.devil: baseCap = 0.2f; break;
            case SpiritualProperty.doremis: baseCap = 0.8f; break;
            case SpiritualProperty.pillar: baseCap = 0.1f; break;
            case SpiritualProperty.godtier: baseCap = 0.4f; break;
            case SpiritualProperty.baledrival: baseCap = 0.0889f; break;
            case SpiritualProperty.pysco: baseCap = 0f; break;
        
            default:
                throw new ArgumentOutOfRangeException(nameof(skillAttr), $"Unsupported SpiritualProperty for conversion: {skillAttr}");
        }
        
        // 例外処理（解釈: 特定の fromAttr -> skillAttr のペア倍率）
        // キンダーガーデン: Psycho へは 2.0 倍
        if (skillAttr == SpiritualProperty.pysco && fromAttr == SpiritualProperty.kindergarden)
            return 2.0f;
        
        // シークイエスト: Devil からは 0.0
        if (skillAttr == SpiritualProperty.cquiest && fromAttr == SpiritualProperty.devil)
            return 0.0f;
        
        // デビル: Cquiest へは 0.6 / Psycho からは 0.0
        if (skillAttr == SpiritualProperty.devil && fromAttr == SpiritualProperty.cquiest)
            return 0.6f;
        if (skillAttr == SpiritualProperty.devil && fromAttr == SpiritualProperty.pysco)
            return 0.0f;
        
        return baseCap;
    }

    /// <summary>
    /// 変換表に定義済みの属性かどうか（未定義は使用禁止）。
    /// </summary>
    private static bool IsAttrDefinedForConversion(SpiritualProperty attr)
    {
        switch (attr)
        {
            case SpiritualProperty.liminalwhitetile:
            case SpiritualProperty.kindergarden:
            case SpiritualProperty.sacrifaith:
            case SpiritualProperty.cquiest:
            case SpiritualProperty.devil:
            case SpiritualProperty.doremis:
            case SpiritualProperty.pillar:
            case SpiritualProperty.godtier:
            case SpiritualProperty.baledrival:
            case SpiritualProperty.pysco:
                return true;
            default:
                return false;
        }
    }
    
    /// <summary>
    /// 属性ポイントの混合上限用倍率。NowPower に応じて返す。
    /// </summary>
    private float GetAttrCombinedCapMultiplier(ThePower p)
    {
        switch (p)
        {
            case ThePower.lowlow: return 0.60f;
            case ThePower.low: return 0.90f;
            case ThePower.medium: return 1.22f;
            case ThePower.high: return 2.00f;
            default: return 1.00f;
        }
    }
    
    /// <summary>
    /// 精神HPに応じてポイントを自然回復する関数。
    /// 回復量は精神Hp現在値を割った数とそれの実HP最大値との割合によるカット
    /// </summary>
    protected void MentalNaturalRecovelyPont()
    {
         // 精神HPを定数で割り回復量に変換する
        var baseRecovelyP = (int)MentalHP / PlayersStates.Instance.MentalHP_TO_P_Recovely_CONVERSION_FACTOR;
        
        // 精神HPと実HP最大値との割合
        var mentalToMaxHPRatio = MentalHP / MaxHP;
        
        var RecovelyValue = baseRecovelyP * mentalToMaxHPRatio;//回復量
        
        if(RecovelyValue < 0)RecovelyValue = 0;
        // ポイント回復
        P += (int)RecovelyValue;
    }
    /// <summary>
    /// 精神HPによるポイント自然回復のカウントアップ用変数
    /// </summary>
    int _mentalPointRecoveryCountUp;
    /// <summary>
    /// 精神HPによるポイント自然回復の最大カウント = 回復頻度
    /// </summary>
    int MentalPointRecovelyMaxCount 
    {
        get
        {
            // テント空洞と夜暗黒の基本値計算
            var tentVoidValue = TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid) * 2;
            var nightDarknessValue = TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness) * 1.6f;
            
            // ミザとスマイラー、元素信仰力の減算値計算
            var mizaValue = TenDayValues(false).GetValueOrZero(TenDayAbility.Miza) / 4f * TenDayValues(false).GetValueOrZero(TenDayAbility.Smiler);
            var elementFaithValue = TenDayValues(false).GetValueOrZero(TenDayAbility.ElementFaithPower) * 0.7f;
        
            // 最終計算
            var finalValue = (int)(tentVoidValue + nightDarknessValue - (mizaValue + elementFaithValue));
            if(finalValue < 2) finalValue = 2;//最低回復頻度ターンは2
            return finalValue;
        }
    }
    /// <summary>
    /// 精神HPによるポイント自然回復の判定と処理
    /// </summary>
    void TryMentalPointRecovery()
    {
        _mentalPointRecoveryCountUp++;
        if(_mentalPointRecoveryCountUp >= MentalPointRecovelyMaxCount)
        {
            _mentalPointRecoveryCountUp = 0;
            MentalNaturalRecovelyPont();
        }
    }
    /// <summary>
    /// 前回ターンが前のめりかの記録
    /// </summary>
    public bool _tempVanguard;
    /// <summary>
    /// 前回ターンに生きてたかどうかの比較のため
    /// </summary>
    public bool _tempLive;

    /// <summary>
    /// bm初回の先制攻撃者かどうか
    /// </summary>
    public bool BattleFirstSurpriseAttacker = false;


    /// <summary>
    /// リカバリターン/再行動クールタイムの「基礎」設定値。
    /// </summary>
    [SerializeField]
    private int maxRecoveryTurn;
    /// <summary>
    /// パッシブ由来のリカバリターン/再行動クールタイムの設定値。
    /// </summary>
    int PassivesMaxRecoveryTurn()
    {
        var result = 0;
        foreach (var passive in _passiveList)
        {
            result += passive.MaxRecoveryTurnModifier();//全て加算する。
        }
        return result;
    }
    /// <summary>
    ///     リカバリターン/再行動クールタイムの設定値。
    /// </summary>
    public int MaxRecoveryTurn
    {
        get
        {
            return maxRecoveryTurn + PassivesMaxRecoveryTurn();//パッシブによる補正値を加算
        }
    }

    /// <summary>
    ///     recovelyTurnの基礎バッキングフィールド
    /// </summary>
    private int recoveryTurn;

    /// <summary>
    /// skillDidWaitCountなどで一時的に通常recovelyTurnに追加される一時的に再行動クールタイム/追加硬直値
    /// </summary>
    private int _tmpTurnsToAdd;
    /// <summary>
    /// 一時的に必要ターン数から引く短縮ターン
    /// </summary>
    private int _tmpTurnsToMinus;
    /// <summary>
    /// 一時保存用のリカバリターン判別用の前ターン変数
    /// </summary>
    private int _tmp_EncountTurn;
    /// <summary>
    /// recovelyTurnTmpMinusという行動クールタイムが一時的に短縮
    /// </summary>
    public void RecovelyTurnTmpMinus(int MinusTurn)
    {
        _tmpTurnsToMinus += MinusTurn;
    }
    /// <summary>
    /// recovelyCountという行動クールタイムに一時的に値を加える
    /// </summary>
    public void RecovelyCountTmpAdd(int addTurn)
    {
        if(!IsActiveCancelInSkillACT)//行動がキャンセルされていないなら
        {
            _tmpTurnsToAdd += addTurn;
        }
    }
    /// <summary>
    /// このキャラが戦場にて再行動を取れるかどうかと時間を唱える関数
    /// </summary>
    public bool RecovelyBattleField(int nowTurn)
    {
        var difference = Math.Abs(nowTurn - _tmp_EncountTurn);//前ターンと今回のターンの差異から経過ターン
        //もし前のめりならば、二倍で進む
        if(manager.IsVanguard(this))
        {
            difference *= 2;
        }

        _tmp_EncountTurn = nowTurn;//今回のターンを次回の差異計算のために一時保存
        if ((recoveryTurn += difference) >= MaxRecoveryTurn + _tmpTurnsToAdd -_tmpTurnsToMinus)//累計ターン経過が最大値を超えたら
        {
            //ここでrecovelyTurnを初期化すると　リストで一括処理した時にカウントアップだけじゃなくて、
            //選ばれたことになっちゃうから、0に初期化する部分はBattleManagerで選ばれた時に処理する。
            return true;
        }
        return false;
    }
    /// <summary>
    /// 戦場へ参戦回復出来るまでのカウントスタート
    /// </summary>
    public void RecovelyWaitStart()
    {
        recoveryTurn = 0;
        RemoveRecovelyTmpAddTurn();//一時追加ターンをリセット
        RemoveRecovelyTmpMinusTurn();//一時短縮ターンをリセット
    }
    /// <summary>
    /// キャラに設定された追加硬直値をリセットする
    /// </summary>
    public void RemoveRecovelyTmpAddTurn()
    {
        _tmpTurnsToAdd = 0;
    }
    /// <summary>
    /// キャラに設定された再行動短縮ターンをリセットする
    /// </summary>
    public void RemoveRecovelyTmpMinusTurn()
    {
        _tmpTurnsToMinus = 0;
    }
    /// <summary>
    /// 戦場へ参戦回復が出来るようにする
    /// </summary>
    public void RecovelyOK()
    {
        recoveryTurn = MaxRecoveryTurn;
    }

    /// <summary>
    /// 「標準のロジック」の割り込みカウンターが発動するかのオプション
    /// AllyClassはUIから、EnemyはSkillAI等で継承して設定する。
    /// とりあえずtrueで設定
    /// </summary>
    public virtual bool IsInterruptCounterActive => true;

    //HP
    CombinedStatesBar HPBar => UI?.HPBar;
    [SerializeField]
    private float _hp;
    public float HP
    {
        get { return _hp; }
        set
        {
            Debug.Log($"HP:{value}");
            if (value > MaxHP)//最大値を超えないようにする
            {
                _hp = MaxHP;
            }
            else _hp = value;
            if(HPBar != null)
            {
                HPBar.HPPercent = value / MaxHP;
            }

            //精神HPのチェック
            if(_mentalHP > MentalMaxHP)//最大値超えてたらカットする。
            {
                _mentalHP = MentalMaxHP;
            }
        }
    }
    [SerializeField]
    private float _maxhp;
    public float MaxHP => _maxhp;

    //精神HP
    [SerializeField]
    float _mentalHP;
    /// <summary>
    /// 精神HP
    /// </summary>
    public float MentalHP 
    {
        get 
        {
            if(_mentalHP > MentalMaxHP)//最大値超えてたらカットする。
            {
                _mentalHP = MentalMaxHP;
            }
            return _mentalHP;
        }
        set
        {
            if(value > MentalMaxHP)//最大値を超えないようにする。
            {
                _mentalHP = MentalMaxHP;
            }
            else _mentalHP = value;
            if(HPBar != null)
            {
                HPBar.MentalRatio = value / MaxHP;//精神HPを設定
                HPBar.DivergenceMultiplier = GetMentalDivergenceThreshold();//UIの乖離指標の幅を設定
            }
        }
    }
    /// <summary>
    /// 精神HP最大値
    /// </summary>
    public float MentalMaxHP => CalcMentalMaxHP();

    /// <summary>
    /// 精神HPの最大値を設定する　パワーでの分岐やHP最大値に影響される
    /// </summary>
    float CalcMentalMaxHP()
    {
        if(NowPower == ThePower.high)
        {
            return _hp * 1.3f + _maxhp *0.08f;
        }else
        {
            return _hp;
        }
    }
    /// <summary>
    /// 精神HPは攻撃時にb_atk分だけ回復する
    /// </summary>
    void MentalHealOnAttack()
    {
        MentalHP += b_ATK.Total;
    }
    void MentalHPHealOnTurn()
    {
        MentalHP += TenDayValues(false).GetValueOrZero(TenDayAbility.Rain);
    }
    
    void MentalHPOnDeath()
    {
            switch (MyImpression)
        {
            case SpiritualProperty.liminalwhitetile:
                // そのまま（変化なし）
                break;
            case SpiritualProperty.kindergarden:
                // 10割回復
                MentalHP = MentalMaxHP;
                break;
            case SpiritualProperty.sacrifaith:
                // 10割回復　犠牲になって満足した
                MentalHP = MentalMaxHP;
                break;
            case SpiritualProperty.cquiest:
                // 10%加算 + 元素信仰力
                MentalHP += MentalMaxHP * 0.1f + TenDayValues(false).GetValueOrZero(TenDayAbility.ElementFaithPower) / 3;
                break;
            case SpiritualProperty.devil:
                // 10%減る
                MentalHP -= MentalMaxHP * 0.1f;
                break;
            case SpiritualProperty.doremis:
                // 春仮眠の夜暗黒に対する多さ 割
                var darkNight = TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness);
                var springNap = TenDayValues(false).GetValueOrZero(TenDayAbility.SpringNap);
                if (springNap > 0)
                {
                    MentalHP = MentalMaxHP * (springNap / darkNight);
                }
                break;
            case SpiritualProperty.pillar:
                // 8割固定
                MentalHP = MentalMaxHP * 0.8f;
                break;
            case SpiritualProperty.godtier:
                // 35%加算
                MentalHP += MentalMaxHP * 0.35f;
                break;
            case SpiritualProperty.baledrival:
                // 7割回復
                MentalHP = MentalMaxHP * 0.7f;
                break;
            case SpiritualProperty.pysco:
                // 20%加算
                MentalHP += MentalMaxHP * 0.2f;
                break;
        }
    
        // 最大値を超えないように調整
        if (MentalHP > MentalMaxHP)
        {
            MentalHP = MentalMaxHP;
        }
        
        // 負の値にならないように調整
        if (MentalHP < 0)
        {
            MentalHP = 0;
        }
    }
    /// <summary>
    /// 実HPに比べて何倍離れているのだろうか。
    /// </summary>
    /// <returns></returns>
    public float GetMentalDivergenceThreshold()
    {
        var ExtraValue = (TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness) -
         TenDayValues(false).GetValueOrZero(TenDayAbility.KereKere)) * 0.01f;//0クランプいらない
        var EnokunagiValue = TenDayValues(false).GetValueOrZero(TenDayAbility.Enokunagi) * 0.005f;
        switch (NowCondition)
        {
            case HumanConditionCircumstances.Angry:
                return 0.47f + ExtraValue;
            case HumanConditionCircumstances.Elated:
                return 2.6f+ ExtraValue;
            case HumanConditionCircumstances.Painful:
                return 0.6f+ ExtraValue;
            case HumanConditionCircumstances.Confused:
                return 0.3f+ ExtraValue;
            case HumanConditionCircumstances.Resolved:
                return 1.2f+ ExtraValue;
            case HumanConditionCircumstances.Optimistic:
                return 1.4f+ ExtraValue;
            case HumanConditionCircumstances.Normal:
                return 0.9f+ ExtraValue;
            case HumanConditionCircumstances.Doubtful:
                return 0.7f+ ExtraValue - EnokunagiValue;//疑念だとエノクナギの影響で乖離しやすくなっちゃうよ
            default:
                return 0f;
        }
    }
    /// <summary>
    /// 精神HPの乖離が起こるまでの発動持続ターン最大値を取得
    /// </summary>
    int GetMentalDivergenceMaxCount()
    {
        if(TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness)> 0)//ゼロ除算対策
        {
            var maxCount = (int)((TenDayValues(false).GetValueOrZero(TenDayAbility.SpringNap) - TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid ) / 2) / TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness));
            if(maxCount > 0)return maxCount;//0より大きければ返す
        }
        return 0 ;

    }
    /// <summary>
    /// 精神HPと実HPの乖離発生処理全般
    /// </summary>
    void MentalDiverGence()
    {
        // 乖離率は 実HPに対する精神HPの割合で決まる。
        float divergenceRatio = Mathf.Abs(MentalHP - HP) / HP;

        if(divergenceRatio > GetMentalDivergenceThreshold())//乖離してるなら
        {
            if(_mentalDivergenceCount >= GetMentalDivergenceMaxCount())//カウントが最大値を超えたら
            {
                _mentalDivergenceRefilCount = GetMentalDivergenceRefulMaxCount();//再度行われないようにカウント開始
                //精神HPが現在HPより上に乖離してるなら アッパー系の乖離メゾット
                if(MentalHP > HP)
                {
                    MentalUpperDiverGenceEffect();
                }else
                {//精神HPが現在HPより下に乖離してるなら ダウナ系の乖離メゾット
                    MentalDownerDiverGenceEffect();
                }
            }

            if(_mentalDivergenceRefilCount <= 0)//再充填カウントがセットされてないので、乖離が発生していないなら持続カウントをプラス
            {
                _mentalDivergenceCount++;//持続カウントをプラス
            }
        }else
        {
            _mentalDivergenceCount = 0;//乖離から外れたらカウントをリセット
        }

    }
    /// <summary>
    /// 精神HPの乖離の再充填までのターン数を取得
    /// </summary>
    int GetMentalDivergenceRefulMaxCount()
    {
        var refil = TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid) * 3 - TenDayValues(false).GetValueOrZero(TenDayAbility.Miza) / 4 * TenDayValues(false).GetValueOrZero(TenDayAbility.Smiler);
        if(refil < 0)return 0;
        return (int)refil;
    }
    /// <summary>
    /// 再充填カウントがゼロより多いならばカウントダウンし、そうでなければtrue、つまり再充填されている。
    /// </summary>
    /// <returns></returns>
    bool IsMentalDiverGenceRefilCountDown()
    {
        if(_mentalDivergenceRefilCount > 0)
        {
            _mentalDivergenceRefilCount--;
            return true;
        }
        return false;//カウントは終わっている。
    }
    int _mentalDivergenceRefilCount = 0;
    int _mentalDivergenceCount = 0;

    /// <summary>
    /// 精神HPのアッパー乖離で起こる変化
    /// </summary>
    protected virtual void MentalUpperDiverGenceEffect()
    {//ここに書かれるのは基本効果
        ApplyPassiveBufferInBattleByID(4);//アッパーのパッシブを付与
    }
    /// <summary>
    /// 精神HPのダウナー乖離で起こる変化
    /// </summary>
    protected virtual void MentalDownerDiverGenceEffect()
    {//ここに書かれるのは基本効果
        
        if(MyType == CharacterType.TLOA)
        {
            HP = _hp * 0.76f;
        }else
        {//TLOA以外の種別なら
            ApplyPassiveBufferInBattleByID(3);//強制ダウナーのパッシブを付与
            if(rollper(50))
            {
                Power1Down();//二分の一でパワーが下がる。
            }
        }
    }
    

    /// <summary>
    /// vitalLayerでHPに到達する前に攻撃値を請け負う処理
    /// </summary>
    public void BarrierLayers(ref StatesPowerBreakdown dmg, ref StatesPowerBreakdown mentalDmg,BaseStates atker)
    {

        // 1) VitalLayer の順番どおりにダメージを適用していく
        //    ここでは「Priority が低い方(手前)が先に処理される想定」を前提に
        //    _vitalLaerList がすでに正しい順序でソートされていることを期待。

        for (int i = 0; i < _vitalLayerList.Count;)
        {
            var layer = _vitalLayerList[i];
            var skillPhy = atker.NowUseSkill.SkillPhysical;
            // 2) このレイヤーに貫通させて、返り値を「残りダメージ」とする
            layer.PenetrateLayer(ref dmg, ref mentalDmg, skillPhy);

            if (layer.LayerHP <= 0f)
            {
                // このレイヤーは破壊された
                _vitalLayerList.RemoveAt(i);
                // リストを削除したので、 i はインクリメントしない（要注意）
                //破壊慣れまたは破壊負け
                var kerekere = atker.TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                if (skillPhy == PhysicalProperty.heavy)//暴断なら破壊慣れ
                {
                    dmg += dmg * 0.015f * kerekere;
                }
                if (skillPhy == PhysicalProperty.volten)//vol天なら破壊負け
                {
                    dmg -= dmg * 0.022f * (atker.b_ATK.Total - kerekere);
                    //b_atk < kerekereになった際、減らずに逆に威力が増えるので、そういう場合の演出差分が必要
                }
            }
            else
            {
                // レイヤーが残ったら i を進める
                i++;
            }

            // 3) dmg が 0 以下になったら、もうこれ以上削る必要ない
            if (dmg.Total <= 0f)
            {
                break;
            }
        }

    }
    /// <summary>
    /// 自分の持つ精神属性の数を取得する。
    /// 精神ポテンシャルとも言うよ
    /// </summary>
    public int GetMySpiritualPotential()
    {
        //重複しないコレクションを作成
        HashSet<SpiritualProperty> spiritualPropertyHashSet = new HashSet<SpiritualProperty>
        {
            //デフォルト精神属性
            DefaultImpression
        };

        //スキルの精神属性
        foreach (var skill in SkillList)
        {
            spiritualPropertyHashSet.Add(skill.SkillSpiritual);
        }

        //重複しない精神属性の数を返す
        return spiritualPropertyHashSet.Count;
    }
    /// <summary>
    /// スキルの数による微小スケーリング定数
    /// </summary>
    const float skillCountMicroScaling = 0.04f;
    /// <summary>
    /// スキル数による思えの値最大値の微小スケーリング
    /// </summary>
    public float CalculateResonanceSkillCountMicroScaling(int skillCount ,float resonanceValue)
    {
        return resonanceValue * (1 - (skillCount - 1 * skillCountMicroScaling));
        //スキルが二つ以上なら数に応じて引かれる
    }
    /// <summary>
    /// 思慮係数による思えの値最大値のスケーリング
    /// scalingMax の値を増やすほど、思慮係数 = 大元の知能が極端に低いキャラの思え最大値がより膨れ上がる設計
    /// </summary>
    /// <returns></returns>
    public float CalculateResonanceThinkingScaling(float ResonanceValue,float scaleMax)
    {
        float scale = Mathf.Lerp(scaleMax,1f,_thinkingFactor/100.0f);
        return ResonanceValue * scale;
    }
    /// <summary>
    /// 思えの値　設定値
    /// 知能が低いほど高い(馬鹿は思えの鳥になりにくい)
    /// 思慮係数で知能の大体を決め　スキルの数での頭のほぐされ具合は微小に知能の高さとして影響する。
    /// </summary>
    public float ResonanceValue
    {
        get 
        { 
            //基本値
            var baseValue = TenDayValuesSum(false) * 0.56f;
            //スキル数による微小スケーリング
            baseValue = CalculateResonanceSkillCountMicroScaling(SkillList.Count, baseValue);
            //思慮係数によるスケーリング
            baseValue = CalculateResonanceThinkingScaling(baseValue, 11f);

            return baseValue + TenDayValues(false).GetValueOrZero(TenDayAbility.Baka) * 1.3f;//馬鹿を加算する 
        }
    }
    /// <summary>
    /// 思えの値用の各キャラクターに設定するユニークな思慮係数(知能？)
    /// 1~100でキャラの思慮深さを定義
    /// </summary>
    [SerializeField][Range(1,100)] float _thinkingFactor;
    /// <summary>
    /// 思えの値の現在の値
    /// </summary>
    float _nowResonanceValue;
    /// <summary>
    /// 現在の思えの値
    /// </summary>
    public float NowResonanceValue
    {
        get
        {
            return _nowResonanceValue;
        }
        set
        {
            _nowResonanceValue = value;
            //最小値未満なら最小値にする。
            if(_nowResonanceValue < 0) _nowResonanceValue = 0;
            //最大値超えたら最大値にする。
            if(_nowResonanceValue > ResonanceValue) _nowResonanceValue = ResonanceValue;
        }
    }
    /// <summary>
    /// 思えの値をフルリセットする
    /// </summary>
    public void ResetResonanceValue() { NowResonanceValue = ResonanceValue; }
    /// <summary>
    /// 思えの値を回復する
    /// </summary>
    public void ResonanceHeal(float heal)
    {
        NowResonanceValue += heal;
        //最大値超えたら最大値にする。
        if(NowResonanceValue > ResonanceValue) NowResonanceValue = ResonanceValue;
    }
    /// <summary>
    /// 思えの値現在値をランダム化する
    /// </summary>
    public void InitializeNowResonanceValue() { NowResonanceValue = RandomEx.Shared.NextFloat(ResonanceValue * 0.6f, ResonanceValue); }
    const float _resonanceHealingOnWalkingFactor = 1f;
    /// <summary>
    /// 歩行時の思えの値回復
    /// </summary>
    public void ResonanceHealingOnWalking() 
    { 
        ResonanceHeal(_resonanceHealingOnWalkingFactor + TenDayValues(false).GetValueOrZero(TenDayAbility.SpringNap) * 1.5f);
    }
    

    
    /// <summary>
    /// このキャラがどの辺りを狙っているか
    /// </summary>
    public DirectedWill Target = 0;

    /// <summary>
    /// このキャラの現在の範囲の意思　　複数持てる
    /// スキルの範囲性質にcanSelectRangeがある場合のみ、ない場合はskillのzoneTraitをそのまま代入される。
    /// </summary>
    public SkillZoneTrait RangeWill = 0;

    /// <summary>
    /// スキル範囲性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasRangeWill(params SkillZoneTrait[] skills)
    {
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }
        return (RangeWill & combinedSkills) == combinedSkills;
    }
    /// <summary>
    /// 指定されたスキルフラグのうち、一つでもRangeWillに含まれている場合はtrueを返し、
    /// 全く含まれていない場合はfalseを返します。
    /// </summary>
    public bool HasRangeWillsAny(params SkillZoneTrait[] skills)
    {
        // 受け取ったスキルフラグをビット単位で結合
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }

        // RangeWillに含まれるフラグとcombinedSkillsのビットAND演算
        // 結果が0でなければ、一つ以上のフラグが含まれている
        return (RangeWill & combinedSkills) != 0;
    }

    /// <summary>
    /// 指定されたスキルフラグのうち、一つでもRangeWillに含まれている場合はfalseを返し、
    /// 全く含まれていない場合はtrueを返します。
    /// </summary>
    public bool DontHasRangeWill(params SkillZoneTrait[] skills)
    {
        // 受け取ったスキルフラグをビット単位で結合
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }

        // RangeWillに含まれるフラグとcombinedSkillsのビットAND演算
        // 結果が0でなければ、一つ以上のフラグが含まれている
        bool containsAny = (RangeWill & combinedSkills) != 0;

        // 一つでも含まれていればfalse、含まれていなければtrueを返す
        return !containsAny;
    }
    /// <summary>
    /// 単体系スキル範囲性質のいずれかを持っているかを判定
    /// </summary>
    public bool HasAnySingleRangeWillTrait()
    {
        return (RangeWill & CommonCalc.SingleZoneTrait) != 0;
    }
    
    /// <summary>
    /// 単体系スキル範囲性質のすべてを持っているかを判定
    /// </summary>
    public bool HasAllSingleRangeWillTraits()
    {
        return (RangeWill & CommonCalc.SingleZoneTrait) == CommonCalc.SingleZoneTrait;
    }




    /// <summary>
    /// 使用中のスキルを強制続行中のスキルとする。　
    /// 例えばスキルの連続実行中の処理や発動カウント中のキャンセル不可能なスキルなどで使う
    /// </summary>
    public void FreezeSkill()
    {
        FreezeUseSkill = NowUseSkill;
    }
    /// <summary>
    /// 強制続行中のスキルをなくす
    /// </summary>
    public void Defrost()
    {
        FreezeUseSkill = null;
        FreezeRangeWill = 0;
    }

    /// <summary>
    /// スキルが強制続行中かどうか
    /// </summary>
    public bool IsFreeze => FreezeUseSkill != null;
    /// <summary>
    /// 強制続行中のスキルの範囲性質
    /// </summary>
    public SkillZoneTrait FreezeRangeWill;
    /// <summary>
    /// 強制続行中のスキルの範囲性質を設定する
    /// </summary>
    public void SetFreezeRangeWill(SkillZoneTrait NowRangeWill)
    {
        FreezeRangeWill = NowRangeWill;
    }
    /// <summary>
    /// パッシブの中に一つでもIsCantACTがtrueのものがあればtrue
    /// 行動できません。　が、CanCancelのパッシブがあるのならCanCancel限定のスキル行動画面へ移動する。
    /// </summary>
    public bool IsFreezeByPassives => _passiveList.Any(p => p.IsCantACT);
    /// <summary>
    /// 動けなくなるが、中断可能なパッシブを一つでも持っているのなら
    /// </summary>
    public bool HasCanCancelCantACTPassive => _passiveList.Any(p => p.CanCancel && p.IsCantACT);
    
    /// <summary>
    /// SkillACT内(damage関数やReactionSkill)などで行動をキャンセルされたかどうか。
    /// </summary>
    /// <returns></returns>
    public bool IsActiveCancelInSkillACT;

    /// <summary>
    ///     このキャラクターの種別
    /// </summary>
    public CharacterType MyType;


    [SerializeField] //フィールドをシリアライズ
    private SpiritualProperty _myImpression;
    /// <summary>
    ///     このキャラクターの属性 精神属性が入る
    /// </summary>
    public SpiritualProperty MyImpression
    {
        get => _myImpression;       // 取得は公開
        protected set => _myImpression = value;  // 変更は継承クラス内のみ許可
    }

    /// <summary>
    ///     このキャラクターの"デフォルト"属性 精神属性が入る
    ///     一定数歩行するとMyImpressionがこれに戻る
    ///     当然この属性自体もゲーム中で変化する可能性はある。
    /// </summary>
    public SpiritualProperty DefaultImpression;
    
    /// <summary>
    /// 暴断耐性
    /// </summary>
    public float HeavyResistance = 1.0f;
    /// <summary>
    /// ヴォル転耐性
    /// </summary>
    public float voltenResistance = 1.0f;
    /// <summary>
    /// 床ずれ耐性
    /// </summary>
    public float DishSmackRsistance = 1.0f;

    /// <summary>
    /// 現在のこのキャラの人間状況
    /// </summary>
    HumanConditionCircumstances NowCondition;
    /// <summary>
    /// 前回の人間状況　同じのが続いてるかの判断要
    /// </summary>
    HumanConditionCircumstances PreviousCondition;
    /// <summary>
    /// 人間状況の続いてるターン　想定連続ターン
    /// </summary>
    int ConditionConsecutiveTurn;
    /// <summary>
    /// 人間状況の累積連続ターン　強制変化用
    /// </summary>
    int TotalTurnsInSameCondition;
    /// <summary>
    /// 人間状況の短期継続ターンをリセットする
    /// </summary>
    void ResetConditionConsecutiveTurn()
    {
        ConditionConsecutiveTurn = 0;
    }
    /// <summary>
    /// 人間状況のターン変数をすべてリセット
    /// </summary>
    void ResetConditionTurns()
    {
        ConditionConsecutiveTurn = 0;
        TotalTurnsInSameCondition = 0;
    }
    /// <summary>
    /// 人間状況が変わった際に必要な処理
    /// 基本的にConditionInNextTurnで自動で処理されるから、各人間状況変化に個別には必要ない。
    /// ただし、時間変化の際は別途呼び出す必要がある。(ConditionInNextTurnを参照してください。)
    /// </summary>
    void ConditionTransition()
    {
            PreviousCondition = NowCondition;
            ResetConditionTurns();
    }
    /// <summary>
    /// 人間状況の次のターンへの変化
    /// </summary>
    void ConditionInNextTurn() 
    {
        // 状態が変わってたら
        if (PreviousCondition != NowCondition)
        {
            ConditionTransition();
        }else
        {//変わってなければターン経過
            ConditionConsecutiveTurn++;
            TotalTurnsInSameCondition++;
        }

        //ターン数が増えた後に時間変化の関数を実行  
        ApplyConditionChangeOnTimePass();
    }
    /// <summary>
    /// 戦闘開始時に決まる人間状況の初期値
    /// </summary>
    public void ApplyConditionOnBattleStart(float eneTenDays)
    {
        var myTenDays = TenDayValuesSum(false);
        // 安全策として、0除算を避ける
        float ratio = (eneTenDays == 0) 
            ? 999999f // 敵が0なら自分が勝ってる扱い(∞倍勝ち)
            : myTenDays / eneTenDays;

        // パワー(NowPower)は ThePower 型 (lowlow, low, medium, high など)
        // MyImpression は精神属性

        // 初期値はとりあえず普調にしておいて、後で条件を満たせば上書きする
        NowCondition = HumanConditionCircumstances.Normal;

        switch (MyImpression)
        {
            //--------------------------------
            // 1) ベール (baledrival)
            //--------------------------------
            case SpiritualProperty.baledrival:
                // 「高揚」：パワーが高 && 2倍負け( ratio <= 0.5 )
                if (NowPower == ThePower.high && ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                }
                else
                {
                    // それ以外は「楽観的」
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                break;

            //--------------------------------
            // 2) デビル (devil)
            //--------------------------------
            case SpiritualProperty.devil:
                // 「高揚」：1.8倍勝ち ( ratio >= 1.8 )
                if (ratio >= 1.8f)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                }
                else
                {
                    // それ以外 => 「普調」 (疑念にはならない)
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 3) 自己犠牲 (sacrifaith)
            //--------------------------------
            case SpiritualProperty.sacrifaith:
                // 覚悟：パワーが low より上(=low以上) かつ 2倍負け( ratio <= 0.5 )
                //   ※「パワーがlow“以上”」= (low, medium, highのいずれか)
                if (NowPower >= ThePower.low && ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Resolved;
                }
                // 疑念：パワーがlowlow && 1.6倍負け( ratio <= 1/1.6≒0.625 )
                else if (NowPower == ThePower.lowlow && ratio <= 0.625f)
                {
                    NowCondition = HumanConditionCircumstances.Doubtful;
                }
                else
                {
                    // それ以外 => 普調
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 4) ゴッドティア (godtier)
            //--------------------------------
            case SpiritualProperty.godtier:
                // 「楽観的」: 総量2.5倍勝ち( ratio >= 2.5 )
                if (ratio >= 2.5f)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                // 「覚悟」 : パワーがmedium以上 && 2倍負け( ratio <= 0.5 )
                else if (NowPower >= ThePower.medium && ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Resolved;
                }
                else
                {
                    // それ以外 => 普調
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 5) リーミナルホワイトタイル (liminalwhitetile)
            //--------------------------------
            case SpiritualProperty.liminalwhitetile:
                // 「楽観的」: 総量2倍勝ち( ratio >= 2.0 )
                if (ratio >= 2.0f)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                // 「疑念」 : 2倍負け( ratio <= 0.5 )
                else if (ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Doubtful;
                }
                else
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 6) キンダーガーデン (kindergarden)
            //--------------------------------
            case SpiritualProperty.kindergarden:
                // 「楽観的」: 1.7倍勝ち
                if (ratio >= 1.7f)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                // 「疑念」 : 1.5倍負け ( ratio <= 2/3 = 0.6667 )
                else if (ratio <= 0.6667f)
                {
                    NowCondition = HumanConditionCircumstances.Doubtful;
                }
                else
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 7) 支柱 (pillar) 
            //    戦闘開始時は「普調」だけ
            //--------------------------------
            case SpiritualProperty.pillar:
                NowCondition = HumanConditionCircumstances.Normal;
                break;

            //--------------------------------
            // 8) サイコパス (pysco)
            //    戦闘開始時は常に落ち着く => 普調
            //--------------------------------
            case SpiritualProperty.pysco:
                NowCondition = HumanConditionCircumstances.Normal;
                break;

            //--------------------------------
            // 9) ドレミス, シークイエスト, etc. 
            //    仕様外 or 未指定なら一旦「普調」にする
            //--------------------------------
            default:
                NowCondition = HumanConditionCircumstances.Normal;
                break;
        }
    }
    /// <summary>
    /// 人間状況の時間変化
    /// </summary>
    void ApplyConditionChangeOnTimePass()
    {
        bool changed = false; // 状態が変化したかどうか

        switch (NowCondition)
        {
            case HumanConditionCircumstances.Resolved:
                // 覚悟 → 高揚 (想定17)
                if (ConditionConsecutiveTurn >= 17)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Angry:
                // 怒り → 普調 (想定10)
                if (ConditionConsecutiveTurn >= 10)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                // 怒り → 高揚 (累積23)
                else if (TotalTurnsInSameCondition >= 23)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Doubtful:
                // 疑念 → 楽観的 (想定11)
                if (ConditionConsecutiveTurn >= 11)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                    changed = true;
                }
                // 疑念 → 混乱 (累積19)
                else if (TotalTurnsInSameCondition >= 19)
                {
                    NowCondition = HumanConditionCircumstances.Confused;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Confused:
                // 混乱 → 普調 (想定11)
                if (ConditionConsecutiveTurn >= 11)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                // 混乱 → 高揚 (累積22)
                else if (TotalTurnsInSameCondition >= 22)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Elated:
                // 高揚 → 普調 (想定13)
                if (ConditionConsecutiveTurn >= 13)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Painful:
                // 辛い → 普調 (想定14)
                if (ConditionConsecutiveTurn >= 14)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                break;

            // 楽観的, 普調 などは今回の仕様では変化しないので何もしない
            default:
                break;
        }

        if (changed)
        {
            ConditionTransition();
        }
    }
        
    /// <summary>
    /// 相性値の高い仲間が死んだ際の人間状況の変化
    /// </summary>
    public void ApplyConditionChangeOnCloseAllyDeath(int deathCount)
    {
        if(MyType == CharacterType.Life)//基本的に生命のみ
        {
            switch (NowCondition)//死によって、どの状況からどの状況へ変化するか
            {
                case HumanConditionCircumstances.Painful://辛い
                    NowCondition = HumanConditionCircumstances.Confused;//辛いと誰でも混乱する
                    break;
                case HumanConditionCircumstances.Optimistic://楽観的
                    switch(MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                            if(rollper(36))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.pysco:
                            if(deathCount > 1)
                            {//二人なら危機感を感じて普調になる
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount > 1)
                            {//二人なら怒り
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                //そうでないなら変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.devil:
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Angry;
                        break;
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount>1 && rollper(10))
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                    }
                    break;
                case HumanConditionCircumstances.Elated:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount>1)
                            {//二人なら混乱
                                NowCondition = HumanConditionCircumstances.Confused;
                            }else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            NowCondition = HumanConditionCircumstances.Confused;
                        break;
                        case SpiritualProperty.doremis:
                            if(deathCount == 1)
                            {//一人なら混乱する
                                NowCondition = HumanConditionCircumstances.Confused;
                            }else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.devil:
                            NowCondition = HumanConditionCircumstances.Normal;
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし　
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.pillar:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし　
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //シークイエストとサイコパスは変化なし
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pysco:
                            ResetConditionConsecutiveTurn();//変化なし
                            break;
                    }
                    break;
                case HumanConditionCircumstances.Resolved:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.devil:
                            if(deathCount>1)
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(44))
                            {
                                NowCondition =HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;
                case HumanConditionCircumstances.Angry:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.devil:
                            if(rollper(66.66f))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(28))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.sacrifaith:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Doubtful:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.sacrifaith:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                        break;
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount > 1 && rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break; 
                        case SpiritualProperty.devil:  
                            if(deathCount > 1 && rollper(21.666f))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;
                        case SpiritualProperty.doremis:
                            ResetConditionConsecutiveTurn();//変化なし
                        break;
                        case SpiritualProperty.pysco:
                            switch(RandomEx.Shared.NextInt(5))
                            {
                                case 0:
                                NowCondition = HumanConditionCircumstances.Optimistic;
                                break;
                                case 1:
                                NowCondition = HumanConditionCircumstances.Resolved;
                                break;
                                case 2:
                                //変化なし
                                ResetConditionConsecutiveTurn();
                                break;
                                case 3:
                                NowCondition = HumanConditionCircumstances.Doubtful;
                                break;
                                case 4:
                                NowCondition = HumanConditionCircumstances.Angry;
                                break;
                            }
                        break;
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.cquiest:
                            NowCondition = HumanConditionCircumstances.Normal;
                        break;
                    }
                    break;
                case HumanConditionCircumstances.Confused:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pillar:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;   
                        case SpiritualProperty.devil:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.godtier:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;
                case HumanConditionCircumstances.Normal:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.sacrifaith:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                        break;
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount > 1 && rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break; 
                        case SpiritualProperty.devil:  
                            if(deathCount > 1 && rollper(21.666f))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;
                        case SpiritualProperty.doremis:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.pysco:
                            switch(RandomEx.Shared.NextInt(5))
                            {
                                case 0:
                                NowCondition = HumanConditionCircumstances.Optimistic;
                                break;
                                case 1:
                                NowCondition = HumanConditionCircumstances.Resolved;
                                break;
                                case 2:
                                //変化なし
                                ResetConditionConsecutiveTurn();
                                break;
                                case 3:
                                NowCondition = HumanConditionCircumstances.Doubtful;
                                break;
                                case 4:
                                NowCondition = HumanConditionCircumstances.Angry;
                                break;
                            }
                        break;
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pillar:
                            ResetConditionConsecutiveTurn();//変化なし
                            break;
                    }
                break;
            }

        }
    }
    /// <summary>
    /// 敵を倒した際の人間状況の変化
    /// </summary>
    public void ApplyConditionChangeOnKillEnemy(BaseStates ene)
    {
        //実行した瞬間にそのスキルによって変化した精神属性により変化してほしいので、スキルの精神属性を使う
        ////(スキル属性のキャラ代入のタイミングについて　を参照)
        var imp = NowUseSkill.SkillSpiritual;
        if (MyType == CharacterType.Life) // 基本的に生命のみ
        {
            switch (NowCondition)
            {
                case HumanConditionCircumstances.Painful:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(66))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(33))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(57))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(33))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            var OptimisticPer = 0;//楽観的に行く確率
                            var eneKereKere = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.KereKere);
                            var eneWif = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.FlameBreathingWife);
                            var KereKere = TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                            var Wif = TenDayValues(true).GetValueOrZero(TenDayAbility.FlameBreathingWife);
                            if(KereKere >= eneKereKere && Wif > eneWif)
                            {
                                OptimisticPer = (int)(Wif - eneWif);
                            }
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(OptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            var NormalPer = 0;
                            var EneLeisure = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure);
                            var Leisure = TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure);
                            if(Leisure > EneLeisure)
                            {
                                NormalPer = (int)(Leisure - EneLeisure);
                            }
                            if(rollper(90 + NormalPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(15))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(9))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(35))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(7))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            var C_NormalEndWarPer = 0;
                            var C_NormalNightPer = 0;
                            var C_EneEndWar = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            var C_EneNight = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.NightInkKnight);
                            var C_EndWar = TenDayValues(true).GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            var C_Night = TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight);
                            if(C_EndWar > C_EneEndWar)
                            {
                                C_NormalEndWarPer = (int)(C_EndWar - C_EneEndWar);
                            }
                            if(C_Night > C_EneNight)
                            {
                                C_NormalNightPer = (int)(C_Night - C_EneNight);
                            }

                            if(rollper(80 + C_NormalEndWarPer + C_NormalNightPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(22))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(78))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(75))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(5))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            var VondPer = 0;
                            var Vond = TenDayValues(true).GetValueOrZero(TenDayAbility.Vond);
                            var EneVond = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Vond);
                            if(Vond > EneVond)
                            {
                                VondPer = (int)(Vond - EneVond);
                            }
                            if(rollper(97 + VondPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(4))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(25))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Optimistic:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(11))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var KinderOptimToElated_PersonaPer = TenDayValues(true).GetValueOrZero(TenDayAbility.PersonaDivergence);
                            if(KinderOptimToElated_PersonaPer> 776)KinderOptimToElated_PersonaPer = 776;//最低でも1%残るようにする
                            if(rollper(777 - KinderOptimToElated_PersonaPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            var SacrifaithOptimToElated_HumanKillerPer = TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller);
                            if(rollper(-50 + SacrifaithOptimToElated_HumanKillerPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(5))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            var baledrivalOptimToElated_HumanKillerPer = TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller);
                            if(rollper(3 + baledrivalOptimToElated_HumanKillerPer*2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            var DevilOptimToElatedPer = TenDayValues(true).GetValueOrZero(TenDayAbility.TentVoid) - TenDayValues(true).GetValueOrZero(TenDayAbility.Enokunagi);
                            if(rollper(60 - DevilOptimToElatedPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(1))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(6))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(4))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(38)){
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        default:
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Elated:
                    //変わらない
                    ResetConditionConsecutiveTurn();
                    break;

                case HumanConditionCircumstances.Resolved:
                    var ResolvedToOptimisticPer = TenDayValues(true).GetValueOrZero(TenDayAbility.FlameBreathingWife) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.FlameBreathingWife);
                    if(ResolvedToOptimisticPer < 0)
                    {
                        ResolvedToOptimisticPer = 0;
                    }
                    ResolvedToOptimisticPer = Mathf.Sqrt(ResolvedToOptimisticPer) * 2;
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(11 + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var ResolvedToOptimisticKinder_luck = TenDayValues(true).GetValueOrZero(TenDayAbility.Lucky);
                            if(rollper(77 + ResolvedToOptimisticKinder_luck + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            var ResolvedToOptimisticSacrifaith_UnextinguishedPath = TenDayValues(true).GetValueOrZero(TenDayAbility.UnextinguishedPath);
                            if(rollper(15 -ResolvedToOptimisticSacrifaith_UnextinguishedPath + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(10 + TenDayValues(true).GetValueOrZero(TenDayAbility.StarTersi) * 0.9f + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(40 + ResolvedToOptimisticPer + TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            var ResolvedToOptimisticDevil_BalePer= TenDayValues(true).GetValueOrZero(TenDayAbility.Vail) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Vail);
                            var ResolvedToOptimisticDevil_FaceToHandPer= TenDayValues(true).GetValueOrZero(TenDayAbility.FaceToHand) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.FaceToHand);
                            if(rollper(40 + ResolvedToOptimisticPer + (ResolvedToOptimisticDevil_BalePer - ResolvedToOptimisticDevil_FaceToHandPer)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(12 + (TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater) - TenDayValues(true).GetValueOrZero(TenDayAbility.Taraiton)) + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(4 + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            // 変化なし
                            ResetConditionConsecutiveTurn();
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(7 + ResolvedToOptimisticPer))
                            {
                                NowCondition =HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Angry:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var AngryEneVail = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Vail);
                            var AngryVail = TenDayValues(true).GetValueOrZero(TenDayAbility.Vail);
                            var AngryEneWaterThunder = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.WaterThunderNerve);
                            var AngryWaterThunder = TenDayValues(true).GetValueOrZero(TenDayAbility.WaterThunderNerve);
                            var AngryToElated_KinderPer = AngryVail - AngryEneVail + (AngryWaterThunder - AngryEneWaterThunder);
                            if(rollper(50 + AngryToElated_KinderPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(30 - TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire)))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller)))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            const float Threshold = 37.5f;
                            var AngryToElated_BaledrivalPer = Threshold;
                            var AngryToElated_Baledrival_VailValue = TenDayValues(true).GetValueOrZero(TenDayAbility.Vail)/2;
                            if(AngryToElated_Baledrival_VailValue >Threshold)AngryToElated_BaledrivalPer = AngryToElated_Baledrival_VailValue;
                            if(rollper(AngryToElated_BaledrivalPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(40 + (20 - TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire))))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(19))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(14))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(27))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Doubtful:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(30 + TenDayValues(true).GetValueOrZero(TenDayAbility.SpringNap)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(46))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(77))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(1 + TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) + TenDayValues(true).GetValueOrZero(TenDayAbility.Smiler)))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            var eneRainCoat = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Raincoat);
                            var EndWar = TenDayValues(true).GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            if(rollper(40 - (EndWar - eneRainCoat)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(44))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(80 + TenDayValues(true).GetValueOrZero(TenDayAbility.Rain)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(90 + TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm) / 4))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) * 1.2f))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(32 + TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper((TenDayValues(true).GetValueOrZero(TenDayAbility.UnextinguishedPath)-2) / 5))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            var DoubtfulToOptimistic_CPer = 0f;
                            if(ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure) < TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight) * 0.3f)
                            {
                                DoubtfulToOptimistic_CPer = TenDayValues(true).GetValueOrZero(TenDayAbility.ElementFaithPower);
                            }

                            if(rollper(38 + DoubtfulToOptimistic_CPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(33))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper((TenDayValues(true).GetValueOrZero(TenDayAbility.HeavenAndEndWar) - 6) / 2))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(27 - TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(85))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(70))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            const float Threshold = 49f;
                            var DoubtfulToNorml_Doremis_nightDarknessAndVoidValue = TenDayValues(true).GetValueOrZero(TenDayAbility.NightDarkness) + TenDayValues(true).GetValueOrZero(TenDayAbility.TentVoid);
                            var DoubtfulToNorml_DoremisPer = Threshold;
                            if(DoubtfulToNorml_Doremis_nightDarknessAndVoidValue < Threshold) DoubtfulToNorml_DoremisPer = DoubtfulToNorml_Doremis_nightDarknessAndVoidValue;
                            if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.NightDarkness) + 30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(DoubtfulToNorml_DoremisPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.StarTersi) / 1.7f))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Confused:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(70))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(44))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var ConfusedToPainful_Kindergarden_DokumamusiAndRainCoatAverage = 
                            (TenDayValues(true).GetValueOrZero(TenDayAbility.dokumamusi) + TenDayValues(true).GetValueOrZero(TenDayAbility.Raincoat)) / 2;
                            if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(80 - (ConfusedToPainful_Kindergarden_DokumamusiAndRainCoatAverage - TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm))))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20 + TenDayValues(true).GetValueOrZero(TenDayAbility.Raincoat) * 0.4f + TenDayValues(true).GetValueOrZero(TenDayAbility.dokumamusi) * 0.6f))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(70 + (TenDayValues(true).GetValueOrZero(TenDayAbility.Sort)-4)))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(60))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(80))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(11))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(34))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(75))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(6))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(27))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(64))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(7))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(60))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(60))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(3 - TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater)))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(90))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(67))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Normal:
                    var y = TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure);//余裕の差
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(30 + y*0.8f))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(20 - y))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(40 + y*2))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(70))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(14))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(30 +y))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(80))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(35 + y*1.1f))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(30 + y*0.1f))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(20 + y/4))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(15 + y * 0.95f))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(12))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            var NormalToElated_DoremisPer = 0f;
                            if(y > 0) NormalToElated_DoremisPer = TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) + TenDayValues(true).GetValueOrZero(TenDayAbility.Miza);
                            if(rollper(38 + y/2))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(20 + NormalToElated_DoremisPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// 相性値の高い味方が復活した際の人間状況の変化
    /// </summary>    
    public void ApplyConditionChangeOnCloseAllyAngel()
    {
        if (MyType == CharacterType.Life) // 基本的に生命のみ
        {
            switch (NowCondition)
            {
                case HumanConditionCircumstances.Painful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.baledrival:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        case SpiritualProperty.pysco:
                        case SpiritualProperty.liminalwhitetile:
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.devil:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Optimistic:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.baledrival:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;    
                    }
                    break;

                case HumanConditionCircumstances.Elated:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.devil:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.kindergarden:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        case SpiritualProperty.doremis:
                        case SpiritualProperty.cquiest:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Resolved:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.liminalwhitetile:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        case SpiritualProperty.doremis:
                        case SpiritualProperty.baledrival:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Angry:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.devil:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.liminalwhitetile:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pysco:
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Doubtful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.cquiest:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        default:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Confused:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.devil:
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.pillar:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Normal:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.pysco:
                        case SpiritualProperty.liminalwhitetile:
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.devil:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;
            }
        }
    } 
    /// <summary>
    /// 死亡と復活の間は何もないも同然なので復活時の変化はなく、死亡時のみ。
    /// つまり==復活した直後にその人間状況のまま開始すること前提==で考える。
    /// </summary>
    public void ApplyConditionChangeOnDeath()
    {
        switch (NowCondition)
        {
            //------------------------------
            // 辛い (Painful)
            //------------------------------
            case HumanConditionCircumstances.Painful:
                // 普調 (一律50%)
                if (rollper(50))
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                else
                {
                    // 変化なし
                    ResetConditionConsecutiveTurn();
                }
                break;

            //------------------------------
            // 楽観的 (Optimistic)
            //------------------------------
            case HumanConditionCircumstances.Optimistic:
                switch (MyImpression)
                {
                    // 楽観的 → 辛い
                    case SpiritualProperty.devil:
                    case SpiritualProperty.sacrifaith:
                        NowCondition = HumanConditionCircumstances.Painful;
                        break;

                    // 楽観的 → 普調
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.godtier:
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.kindergarden:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    case SpiritualProperty.pysco:
                        // サイコパスは 50% 普調 / 50% 変化なし
                        if (rollper(50))
                        {
                            NowCondition = HumanConditionCircumstances.Normal;
                        }
                        else
                        {
                            // 変化なし
                            ResetConditionConsecutiveTurn();
                        }
                        break;

                    // 楽観的 → 変化なし
                    case SpiritualProperty.baledrival:
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.doremis:
                        ResetConditionConsecutiveTurn();
                        break;

                    default:
                        // 変化なし
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 高揚 (Elated)
            //------------------------------
            case HumanConditionCircumstances.Elated:
                switch (MyImpression)
                {
                    // 変化なし
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.godtier:
                    case SpiritualProperty.devil:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 普調
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.kindergarden:
                    case SpiritualProperty.doremis:
                    case SpiritualProperty.pysco:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    // 楽観的
                    case SpiritualProperty.baledrival:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 辛いにはいかなそう => default で変化なし
                    default:
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 覚悟 (Resolved)
            //------------------------------
            case HumanConditionCircumstances.Resolved:
                switch (MyImpression)
                {
                    // 変化なし => ベール
                    case SpiritualProperty.baledrival:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 普調 => シークイエスト, ドレミス, デビル, ゴッドティア, キンダー
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.doremis:
                    case SpiritualProperty.devil:
                    case SpiritualProperty.godtier:
                    case SpiritualProperty.kindergarden:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    // 辛い => 支柱, リーミナル
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.liminalwhitetile:
                        NowCondition = HumanConditionCircumstances.Painful;
                        break;

                    // 疑念 => 自己犠牲, サイコ
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.pysco:
                        NowCondition = HumanConditionCircumstances.Doubtful;
                        break;

                    default:
                        // 変化なし
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 怒り (Angry)
            //------------------------------
            case HumanConditionCircumstances.Angry:
                switch (MyImpression)
                {
                    // 変化なし => リーミナル, 自己犠牲
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.sacrifaith:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 楽観的 => サイコ
                    case SpiritualProperty.pysco:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 普調 => else
                    default:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;
                }
                break;

            //------------------------------
            // 疑念 (Doubtful)
            //------------------------------
            case HumanConditionCircumstances.Doubtful:
                switch (MyImpression)
                {
                    // 怒り => 自己犠牲, ベール, デビル
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.baledrival:
                    case SpiritualProperty.devil:
                        NowCondition = HumanConditionCircumstances.Angry;
                        break;

                    // 普調 => サイコ, リーミナル, 支柱
                    case SpiritualProperty.pysco:
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.pillar:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    // 楽観的 => ドレミス, シークイエスト, キンダー, ゴッドティア
                    case SpiritualProperty.doremis:
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.kindergarden:
                    case SpiritualProperty.godtier:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 辛いにはいかない => default => 変化なし
                    default:
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 混乱 (Confused)
            //------------------------------
            case HumanConditionCircumstances.Confused:
                switch (MyImpression)
                {
                    // 変化なし => キンダー
                    case SpiritualProperty.kindergarden:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 高揚 => ベール, リーミナル
                    case SpiritualProperty.baledrival:
                    case SpiritualProperty.liminalwhitetile:
                        NowCondition = HumanConditionCircumstances.Elated;
                        break;

                    // 普調 => else
                    default:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;
                }
                break;

            //------------------------------
            // 普調 (Normal)
            //------------------------------
            case HumanConditionCircumstances.Normal:
                switch (MyImpression)
                {
                    // 変化なし => 支柱, サイコ
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.pysco:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 楽観的 => ゴッドティア
                    case SpiritualProperty.godtier:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 疑念 => リーミナル, キンダー, デビル
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.kindergarden:
                    case SpiritualProperty.devil:
                        NowCondition = HumanConditionCircumstances.Doubtful;
                        break;

                    // 怒り => 自己犠牲, シークイエスト, ベール
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.baledrival:
                        NowCondition = HumanConditionCircumstances.Angry;
                        break;

                    default:
                        // 変化なし
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            default:
                // それ以外(例えば none) => 変化なし
                ResetConditionConsecutiveTurn();
                break;
        }
    }
        
    //特別補正------------------------------------------------------------------------------------------------特別補正----------------------------------------------------------------------------------------------
    /// <summary>
    /// 特別補正用保持リスト
    /// </summary>
    private List<ModifierPart> _specialModifiers = new();
    /// <summary>
    /// 特別補正をセットする。
    /// オプション変数部分が固定値
    /// </summary>
    public void SetSpecialModifier(string memo,whatModify whatstate,float value = 1, StatesPowerBreakdown fixedModifier = null, bool isFixed = false)
    {
        if (_specialModifiers == null) _specialModifiers = new List<ModifierPart>();//nullチェック、処理
        _specialModifiers.Add(new ModifierPart(memo, whatstate, value, fixedModifier, isFixed));
    }
    /// <summary>
    /// 既にある特別補正をコピーする。
    /// </summary>
    public void CopySpecialModifier(ModifierPart mod)
    {
        if (_specialModifiers == null) _specialModifiers = new List<ModifierPart>();//nullチェック、処理
        _specialModifiers.Add(mod);
    }
    /// <summary>
    /// 特別な補正を利用  パーセンテージ補正用  戦闘の状況で要所要所で傾くイメージなので平均化　
    /// パッシブの乗算補正の積とブレンドとは違う(CalculateBlendedPercentageModifier)
    /// </summary>
    public float GetSpecialPercentModifier(whatModify mod)
    {
        return _specialModifiers.Where(m => m.IsFixedOrPercent == false && m.whatStates == mod)
            .Aggregate(1.0f, (total, m) => total * m.Modifier);//指定したステータスとパーセンテージ補正のリスト内全ての値を乗算
    }
    /// <summary>
    /// 特別な補正を利用  固定値補正用
    /// </summary>
    public StatesPowerBreakdown GetSpecialFixedModifier(whatModify mod)
    {
        var calculateList =_specialModifiers.Where(m => m.IsFixedOrPercent == true && m.whatStates == mod).ToList();
        return CalculateFixedModifierTotal(calculateList);
            
    }

    /// <summary>
    /// 特別補正の内訳リストを集約した一つの内訳にして返す処理
    /// 特別補正リストの固定値集約用
    /// </summary>
    private StatesPowerBreakdown CalculateFixedModifierTotal(List<ModifierPart> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
        {
            return new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        }
        
        StatesPowerBreakdown result = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        
        for (int i = 0; i < modifiers.Count; i++)
        {
            result = result + modifiers[i].FixedModifier;
        }
        
        return result;
    }

    /// <summary>
    /// 特別な補正の保持リストをただ返す。　主にフレーバー要素用。_conditionalMods;
    /// </summary>
    public List<ModifierPart> UseSpecialModifiers
    {
        get => _specialModifiers;
    }

    /// <summary>
    /// カウンター用の一時的な防御無視率 )特別補正_
    /// 比較する際にこちらの方が本来の無視率より多ければ、こちらの値が使用される。
    /// -1は使用されていない。というか直接比較されるから-以下の数字にしとけば絶対参照されない。
    /// </summary>
    float _exCounterDEFATK =-1;
    /// <summary>
    /// カウンター用防御無視率をセット
    /// </summary>
    public void SetExCounterDEFATK(float value)
    {
        _exCounterDEFATK = value;
    }

    //////------------------------キャラクター限定の特別補正----------------------------------------------------------------------------------------
    public List<CharacterConditionalModifier> _charaConditionalMods;
    /// <summary>
    /// キャラ限定補正を追加
    /// </summary>
    public void SetCharaConditionalModifierList(BaseStates target, string memo, 
    whatModify whatstate,float value, StatesPowerBreakdown fixedModifier = null, bool isFixed = false)
    {
        if (_charaConditionalMods == null)
            _charaConditionalMods = new List<CharacterConditionalModifier>();

        // ModifierPart を生成して CharacterConditionalModifier に渡す
        var part = new ModifierPart(memo, whatstate, value, fixedModifier, isFixed);
        _charaConditionalMods.Add(new CharacterConditionalModifier(target, part));
    }
   /// <summary>
    /// キャラ限定補正を、その敵と一致しているものだけ通常の特別補正リストに追加する
    /// </summary>
    public void ApplyCharaConditionalToSpecial(BaseStates target)
    {
        if (_charaConditionalMods == null || _charaConditionalMods.Count < 1) return;
        foreach (var cond in _charaConditionalMods.Where(x => x.Target == target))
        {
            // SetSpecialModifier(memo, whatstate, value, fixedModifier, isFixed)
            SetSpecialModifier(
                cond.Part.memo,
                cond.Part.whatStates,
                cond.Part.Modifier,
                cond.Part.FixedModifier,
                cond.Part.IsFixedOrPercent
            );
        }
    }
    //////　　　　　　　　　　キャラクター限定の特別補正----------------------------------------------------------------------------------------
    
    /// <summary>
    /// 特別補正などをすべて消す
    /// </summary>
    public void RemoveUseThings()
    {
        _exCounterDEFATK = -1;
        _specialModifiers = new List<ModifierPart>();
        _charaConditionalMods = new List<CharacterConditionalModifier>();
    }

    //特別補正------------------------------------------------------------------------------------------------特別補正-----------------------------------------------------------------------------------------------

    //実体リストやその他有効化管理などの関数は、各派生立場のクラスで実装する
    


    /// <summary>
    /// キャラクターが現在使用可能なスキルリスト
    /// </summary>
    public abstract IReadOnlyList<BaseSkill> SkillList { get; }
    /// <summary>
    /// 現在有効なTLOAスキルリスト
    /// </summary>
    public List<BaseSkill> TLOA_SkillList => SkillList.Where(x => x.IsTLOA).ToList();
    /// <summary>
    /// 完全な単体攻撃かどうか
    /// (例えばControlByThisSituationの場合はrangeWillにそのままskillのzoneTraitが入るので、
    /// そこに範囲系の性質(事故で範囲攻撃に変化)がある場合はfalseが返る
    /// </summary>
    /// <returns></returns>
    private bool IsPerfectSingleATK()
    {
        return DontHasRangeWill(SkillZoneTrait.CanSelectMultiTarget,
            SkillZoneTrait.RandomSelectMultiTarget, SkillZoneTrait.RandomMultiTarget,
            SkillZoneTrait.AllTarget);
    }
    /// <summary>
    /// 命中率計算
    /// </summary>
    /// <returns></returns>
    public virtual StatesPowerBreakdown EYE()
    {
        StatesPowerBreakdown eye = b_EYE;//基礎命中率

        eye *= GetSpecialPercentModifier(whatModify.eye);//命中率補正。リスト内がゼロならちゃんと1.0fが返る。
        PassivesPercentageModifier(whatModify.eye, ref eye);//パッシブの乗算補正
        eye += GetSpecialFixedModifier(whatModify.eye);//命中率固定値補正

        if(NowUseSkill != null)//スキルがある、攻撃時限定処理
        {
            //範囲意志によるボーナス
            var dict = NowUseSkill.HitRangePercentageDictionary;
            if(dict == null || dict.Count <= 0)
            {
                Debug.Log($"{CharacterName}の{NowUseSkill.SkillName}-"
                +"範囲意志によるボーナスがスキルに設定されていないため計算されませんでした。"
                +"「範囲意志ボーナスが設定されていないスキルなら正常です。」");
            }else{
                foreach (KeyValuePair<SkillZoneTrait, float> entry
                    in NowUseSkill.HitRangePercentageDictionary)//辞書に存在する物全てをループ
                {
                    if (HasRangeWill(entry.Key))//キーの内容が範囲意志と合致した場合
                    {
                        eye += entry.Value;//範囲意志による補正は非十日能力値

                        //基本的に範囲は一つだけのはずなので無用なループは避けてここで終了
                        break;
                    }
                }
            }

                    //単体攻撃による命中補正
            //複数性質を持っていない、完全なる単体の攻撃なら
            if (IsPerfectSingleATK())
            //ControlBySituationでの事故性質でも複数性質で複数事故が起こるかもしれないので、それも加味してる。
            {
                var agiPer = 6;//攻撃者のAgiの命中補正用 割る数
                if (NowUseSkill.SkillPhysical == PhysicalProperty.heavy)//暴断攻撃なら
                {
                    agiPer *= 2;//割る数が二倍に
                }
                eye += AGI() / agiPer;
            }

        }


        //パッシブの補正　固定値
        eye += _passiveList.Sum(p => p.EYEFixedValueEffect());

        //eye.ClampToZero();//クランプ　ここでやらない
        return eye;
    }

    /// <summary>
    /// 回避率計算
    /// </summary>
    public virtual StatesPowerBreakdown AGI()
    {
        StatesPowerBreakdown agi = b_AGI;//基礎回避率

        agi *= GetSpecialPercentModifier(whatModify.agi);//回避率補正。リスト内がゼロならちゃんと1.0fが返る。
        PassivesPercentageModifier(whatModify.agi, ref agi);//パッシブの乗算補正
        agi += GetSpecialFixedModifier(whatModify.agi);//回避率固定値補正

        if(manager == null)
        {
            Debug.Log("BattleManagerがnullです、恐らく戦闘開始前のステータス計算をしている可能性があります。回避率の前のめり補正をスキップします。");
        }
        else if (manager.IsVanguard(this))//自分が前のめりなら
        {
            agi /= 2;//回避率半減
        }
        //パッシブの補正　固定値
        agi += _passiveList.Sum(p => p.AGIFixedValueEffect());

        //agi.ClampToZero();//クランプ　ここでやらない
        return agi;
    }

    /// <summary>
    /// 攻撃力のステータス
    /// 引数の補正はいわゆるステータスに関係ないものとして、攻撃補正なので、
    /// 「実際に敵のHPを減らす際」のみに指定する。  = 精神HPに対するダメージにも使われない。
    /// </summary>
    /// <param name="AttackModifier"></param>
    /// <returns></returns>
    public virtual StatesPowerBreakdown ATK(float AttackModifier =1f)
    {
        StatesPowerBreakdown atk = b_ATK;//基礎攻撃力

        atk *= GetSpecialPercentModifier(whatModify.atk);//攻撃力補正
        PassivesPercentageModifier(whatModify.atk, ref atk);
        atk += GetSpecialFixedModifier(whatModify.atk);//攻撃力固定値補正

        atk *= AttackModifier;//攻撃補正  実際の攻撃時のみに参照される。

        //範囲意志によるボーナス
        foreach (KeyValuePair<SkillZoneTrait, float> entry
            in NowUseSkill.PowerRangePercentageDictionary)//辞書に存在する物全てをループ
        {
            if (HasRangeWill(entry.Key))//キーの内容が範囲意志と合致した場合
            {
                atk += entry.Value;//範囲意志による補正が掛かる

                //基本的に範囲は一つだけのはずなので無用なループは避けてここで終了
                break;
            }
        }

        //単体攻撃で暴断物理攻撃の場合のAgi攻撃補正
        if (IsPerfectSingleATK())
        {
            if (NowUseSkill.SkillPhysical == PhysicalProperty.heavy)
            {
                atk += AGI() / 6;
            }
        }
        //パッシブの補正　固定値を加算する
        atk += _passiveList.Sum(p => p.ATKFixedValueEffect());

        //atk.ClampToZero();//クランプ　ここでやらない
        return atk;
    }

    /// <summary>
    ///     防御力計算 シミュレートも含む(AimStyle不一致によるクランプのため)
    ///     オプションのAimStyleに値を入れるとそのAimStyleでシミュレート
    /// </summary>
    /// <returns></returns>
    public virtual StatesPowerBreakdown DEF(float minusPer=0f, AimStyle? SimulateAimStyle = null)
    {
        var def = b_DEF(); //基礎防御力が基本。

        if(SimulateAimStyle != null)//シミュレートするなら
        {
            def = b_DEF(SimulateAimStyle);//b_defをシミュレート
        }

        def *= GetSpecialPercentModifier(whatModify.def);//防御力補正
        PassivesPercentageModifier(whatModify.def, ref def);//パッシブの乗算補正
        def += GetSpecialFixedModifier(whatModify.def);//防御力固定値補正

        def *= PassivesDefencePercentageModifierByAttacker();//パッシブ由来の攻撃者を限定する補正

        var minusAmount = def * minusPer;//防御低減率

        //パッシブの補正　固定値
        def += _passiveList.Sum(p => p.DEFFixedValueEffect());

        def -= minusAmount;//低減

        //def.ClampToZero();//クランプ　ここでやらない
        return def;
    }
    /// <summary>
    /// 精神HP用の防御力
    /// </summary>
    public virtual StatesPowerBreakdown MentalDEF()
    {
        return b_DEF() * 0.7f * NowPower switch
        {
            ThePower.high => 1.4f,
            ThePower.medium => 1f,
            ThePower.low => 0.7f,
            ThePower.lowlow => 0.4f,
            _ => -4444444,//エラーだ
        };
    }

    //互角一撃の生存処理--------------------------------------------------------------------------互角一撃の生存処理------------------------------ーーーーーーーーーーー

    /// <summary>
    /// 互角一撃の状況で「即死しかけたが奇跡的に生き残る」確率(%)を返す。
    ///
    /// ◆大まかな流れ：
    ///  1) 精神属性 × パワー条件 を満たしているかどうか
    ///      - 満たしていなければ 0%
    ///  2) 人間状況ごとの基本値をベースにする
    ///      - 怒り/高揚/辛い/混乱 → 0%
    ///      - 覚悟 → 7%
    ///      - 楽観的 → 2%
    ///      - 普調 → 4%
    ///      - 疑念 → 1%
    ///  3) 特定の「精神属性 × 人間状況」組み合わせでさらに上書き
    ///      - 例: ゴッドティア × 楽観的 = 12% など
    /// </summary>
    public int GetMutualKillSurvivalChance()
    {
        var property = MyImpression;
        var power = NowPower;
        var condition = NowCondition;
        // (A) まず "パワー条件" をチェックして、
        //     クリアしていなければ0%を返す
        //     （属性ごとに分岐。ゴッドティアなど「パワー条件なし」はスルー）
        if (!CheckPowerCondition(property, power))
        {
            return 0; 
        }

        // (B) 次に "人間状況" ごとの基本値を設定
        int baseChance = GetBaseChanceByCondition(condition);

        // (C) 最後に「特定の属性×状況」で上書き（例: デビル×楽観的=0% など）
        baseChance = OverrideByPropertyAndCondition(property, condition, baseChance);

        // 返却値を 0～100 にクランプ（負になったり100超えたりしないように）
        if (baseChance < 0) baseChance = 0;
        if (baseChance > 100) baseChance = 100;

        return baseChance;
    }


    /// <summary>
    /// 属性ごとの「パワー条件」をチェックし、満たしていればtrue、ダメならfalseを返す。
    /// </summary>
    private bool CheckPowerCondition(SpiritualProperty property, ThePower power)
    {
        switch (property)
        {
            case SpiritualProperty.liminalwhitetile:
                // パワーが普通以上 (>= medium)
                return (power >= ThePower.medium);

            case SpiritualProperty.kindergarden:
                // パワーが高い (== high)
                return (power == ThePower.high);

            case SpiritualProperty.sacrifaith:
                // パワーが普通以上 (>= medium)
                return (power >= ThePower.medium);

            case SpiritualProperty.cquiest:
                // 「低い以上」と書かれていたため (>= low)
                // 低い(low), 普通(medium), 高い(high) はOK。 たるい(lowlow)はNG
                return (power >= ThePower.low);

            case SpiritualProperty.devil:
                // 本文に「パワーが高いと」としか書かれていない→ここでは「高いでないとダメ」と仮定
                return (power == ThePower.high);

            case SpiritualProperty.doremis:
                // パワーが普通以上
                return (power >= ThePower.medium);

            case SpiritualProperty.pillar:
                // パワーが普通以上
                return (power >= ThePower.medium);

            case SpiritualProperty.godtier:
                // 「パワー条件なし」
                return true;

            case SpiritualProperty.baledrival:
                // 「パワーが低い以上」→ ここでは (power >= ThePower.low) と解釈
                return (power >= ThePower.low);

            case SpiritualProperty.pysco:
                // パワーが普通以上
                return (power >= ThePower.medium);

            default:
                // それ以外( none など) は特に定義されていない場合、0%扱い
                return false;
        }
    }


    /// <summary>
    /// 人間状況ごとの「基本値」を返す。
    /// </summary>
    private int GetBaseChanceByCondition(HumanConditionCircumstances condition)
    {
        switch (condition)
        {
            case HumanConditionCircumstances.Angry:
            case HumanConditionCircumstances.Elated:
            case HumanConditionCircumstances.Painful:
            case HumanConditionCircumstances.Confused:
                return 0;

            case HumanConditionCircumstances.Resolved:
                return 7;
            case HumanConditionCircumstances.Optimistic:
                return 2;
            case HumanConditionCircumstances.Normal:
                return 4;
            case HumanConditionCircumstances.Doubtful:
                return 1;

            default:
                // ここに来ることはあまり想定外だが、念のため0%
                return 0;
        }
    }

    /// <summary>
    /// 属性 × 状況 の特別な組み合わせで「上書き」する。
    /// 例：ゴッドティア × 楽観的 => 12% など
    /// </summary>
    private int OverrideByPropertyAndCondition(
        SpiritualProperty property,
        HumanConditionCircumstances condition,
        int baseChance
    )
    {
        switch (property)
        {
            //=======================================
            // ■ゴッドティア (godtier)
            //=======================================
            case SpiritualProperty.godtier:
                // 楽観的なら 12% (通常2%を上書き)
                if (condition == HumanConditionCircumstances.Optimistic)
                {
                    return 12;
                }
                break;

            //=======================================
            // ■デビル (devil)
            //=======================================
            case SpiritualProperty.devil:
                // 楽観的なら 0% (通常2% => 0% 上書き)
                if (condition == HumanConditionCircumstances.Optimistic)
                {
                    return 0;
                }
                break;

            //=======================================
            // ■自己犠牲 (sacrifaith)
            //=======================================
            case SpiritualProperty.sacrifaith:
                // 怒り => 6% (通常 怒りは0% => 6%で上書き)
                if (condition == HumanConditionCircumstances.Angry)
                {
                    return 6;
                }
                break;

            //=======================================
            // ■ドレミス (doremis)
            //=======================================
            case SpiritualProperty.doremis:
                // 疑念 => 14% (通常1% => 14%)
                if (condition == HumanConditionCircumstances.Doubtful)
                {
                    return 14;
                }
                break;

            //=======================================
            // ■支柱 (pillar)
            //=======================================
            case SpiritualProperty.pillar:
                // 辛い => 6% (通常0% => 6%)
                if (condition == HumanConditionCircumstances.Painful)
                {
                    return 6;
                }
                break;

            //=======================================
            // ■ベールドライヴァル (baledrival)
            //=======================================
            case SpiritualProperty.baledrival:
                // 高揚 => 11% (通常0% => 11%)
                if (condition == HumanConditionCircumstances.Elated)
                {
                    return 11;
                }
                break;

            //=======================================
            // ■その他のケース
            //   (サイコパスやキンダーガーデン、リーミナルホワイトタイルなど)
            //   特に指定がなければ、 baseChance のまま
            //=======================================
            default:
                break;
        }

        // 上記で特に上書きされなければ baseChance のまま
        return baseChance;
    }
    //互角一撃の生存処理--------------------------------------------------------------------------互角一撃の生存処理------------------------------ーーーーーーーーーーー

    /// <summary>
    /// 基礎山型分布によるダメージ補正
    /// 返り値で攻撃が乱れたかどうか -15%以下なら乱れたのでTrueが返ります。
    /// </summary>
    bool GetBaseCalcDamageWithPlusMinus22Percent(ref StatesPowerBreakdown baseDamage)
    {
        // 1) 8d5501 を振る（8回ランダム）
        int diceSum = 0;
        for (int i = 0; i < 8; i++)
        {
            // Range(1, 5502) => [1..5501] の整数
            diceSum += RandomEx.Shared.NextInt(1, 5502);
        }

        // 2) 平均(22008)を引いて、0.00001f を掛ける
        //    → -0.22 ～ +0.22 (±22%)
        float offset = (diceSum - 22008) * 0.00001f;

        // 3) baseDamage に対して (1 + offset) 倍する
        //    → (1 - 0.22)～(1 + 0.22) = 0.78～1.22 倍
        StatesPowerBreakdown finalDamage = baseDamage * (1f + offset);

        // 5) float で返す（丸めたくないのでそのまま）
        baseDamage = finalDamage;//ダメージに代入。

        return offset <= -0.15f;//-15%以下なら乱れた
    }
    /// <summary>
    /// 防ぎ方(AimStyle)の不一致がある場合、クランプする
    /// </summary>
    private StatesPowerBreakdown ClampDefenseByAimStyle(BaseSkill skill,StatesPowerBreakdown def)
    {
        if(skill.NowAimStyle() != NowDeffenceStyle)
        {
            var MatchedMaxClampDef = DEF(skill.DEFATK, skill.NowAimStyle())*0.7f;//適切な防御力の0.7倍がクランプ最大値

            if(NowPower>ThePower.medium)//パワーが高い場合は 「適切な防御力をこしてた場合のみ」適切防御力の0.7倍にクランプ
            {
                //まず比較する、超していた場合にクランプ
                if(DEF()>DEF(0,skill.NowAimStyle()))//今回の防御力が適切な防御力を超してた場合、
                {
                    return MatchedMaxClampDef;//クランプされる。
                }
            }else//そうでない場合は、「適切な防御力を超してる越してない関係なく」適切防御力の0.7倍にクランプ(その最大値を絶対に超えない。)
            {
                
                if(def > MatchedMaxClampDef)
                {
                    return MatchedMaxClampDef;//最大値を超えたら最大値にクランプ
                }
            }
        }
        return def;//そのまま返す。
    }
    /// <summary>
    /// ダメージを渡し、がむしゃらの補正をかけて返す
    /// </summary>
    StatesPowerBreakdown GetFrenzyBoost(BaseStates atker,StatesPowerBreakdown dmg)
    {
        var boost =1.0f;
        var skill = atker.NowUseSkill;
        if(skill.NowConsecutiveATKFromTheSecondTimeOnward())//2回目以降の連続攻撃なら
        {
            var StrongFootEye = (EYE() + AGI()) /2f;
            var WeekEye = atker.EYE();
            var boostCoef = 0f;//ブースト係数

            if(StrongFootEye > WeekEye)//ちゃんと被害者側の命中回避平均値が攻撃者の命中より高い場合に限定する
            {
                boostCoef = Mathf.Floor((StrongFootEye.Total - WeekEye.Total) / 5);//十日能力の記録の必要がないので、totalを使う。
                boost += boostCoef * 0.01f;
                for(int i =0;i<skill.ATKCountUP-1;i++)//初回=単回攻撃の恐れがある場合は、がむしゃらは発動しないので、二回目から一回ずつ乗算されるようにしたいから-1
                {
                    dmg *= boost;
                }
            }
        }
        return dmg;//連続攻撃でないなら、そのまま返す
    }
    /// <summary>
    /// 互角一撃の生存判定
    /// </summary>
    void CalculateMutualKillSurvivalChance(float LiveHP,float dmg,BaseStates atker)
    {
        //deathの判定が入る前に、互角一撃の生存判定を行い、HP再代入
        //ダメージの大きさからして絶対に死んでるからDeath判定は要らず、だからDeath辺りでの判定がいらない。(DeathCallBackが起こらない)
        if(LiveHP >= _maxhp*0.2f)//HPが二割以上の時に、
        {
            if(atker.TenDayValuesSum(true) <= TenDayValuesSum(false) * 1.6f)//自分の十日能力の総量の1.6倍以下なら
            {
                if (dmg >= _maxhp * 0.34f && dmg <= _maxhp * 0.66f )//大体半分くらいの攻撃なら  
                {
                    //生存判定が入る
                    if(rollper(GetMutualKillSurvivalChance()))
                    {
                        HP = _maxhp * 0.07f;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 呼び出し側の攻撃時の最大余剰ダメージを取得する
    /// </summary>
    float GetOverkillOverflowMax()
    {
        var flowmax = 0f;

        //基本値
        flowmax = TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller) * 2 + TenDayValues(true).GetValueOrZero(TenDayAbility.dokumamusi) * 0.4f;

        switch(MyImpression)//精神属性で分岐　
        {
            case SpiritualProperty.liminalwhitetile:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.FlameBreathingWife) * 0.8f;
                break;
            case SpiritualProperty.kindergarden:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) * 2;
                break;
                
            case SpiritualProperty.sacrifaith:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) * 0.5f + TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight);
                break;
                
            case SpiritualProperty.pysco:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.Raincoat) * 6 * TenDayValues(true).GetValueOrZero(TenDayAbility.UnextinguishedPath);
                break;
                
            case SpiritualProperty.baledrival:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure) * 3;
                break;
                
            case SpiritualProperty.devil:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.NightDarkness) + TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm);
                break;
                
            case SpiritualProperty.cquiest:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.JoeTeeth) * 1.7f - TenDayValues(true).GetValueOrZero(TenDayAbility.ElementFaithPower) * 0.11f;
                break;
                
            case SpiritualProperty.godtier:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.CryoniteQuality);
                break;
                
            case SpiritualProperty.pillar:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.PersonaDivergence) - TenDayValues(true).GetValueOrZero(TenDayAbility.Pilmagreatifull);
                break;
                
            case SpiritualProperty.doremis:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.SpringNap) - TenDayValues(true).GetValueOrZero(TenDayAbility.ElementFaithPower); 
                break;
            case SpiritualProperty.none:
                //noneならそもそも最大余剰ダメージ発生せず
                break;
            default:
                //他の未実装の精神属性を追加し忘れた場合に気づける
                throw new NotImplementedException($"SpiritualProperty {MyImpression} is not handled.");
        }

        return flowmax;//これ自体がクランプ要素だから0クランプいらん
    }
    /// <summary>
    /// 呼び出し側の攻撃時の余剰ダメージの通過率
    /// </summary>
    /// <returns></returns>
    float GetOverkillOverflowPassRate()
    {
        var passRate = 0f;
        switch(NowCondition)
        {
            case HumanConditionCircumstances.Painful:
                if(MyImpression == SpiritualProperty.devil)
                {
                    passRate = 2;
                }else{
                    passRate = 0.43f;
                }
                break;
            case HumanConditionCircumstances.Optimistic:
                if(MyImpression == SpiritualProperty.cquiest)
                {
                    passRate = 1.1f;
                }else{
                    passRate = 1.01f;
                }
                break;
            case HumanConditionCircumstances.Elated:
                passRate = 1.2f;
                break;
            case HumanConditionCircumstances.Resolved:
                passRate = 1.0f;
                break;
            case HumanConditionCircumstances.Angry:
                if(MyImpression == SpiritualProperty.sacrifaith)
                {
                    passRate = 1.5f;
                }else if(MyImpression == SpiritualProperty.devil)
                {
                    passRate = 1.0f;
                }else
                {
                    passRate = 1.3f;
                }
                break;
            case HumanConditionCircumstances.Doubtful:
                if(MyImpression == SpiritualProperty.doremis)
                {
                    passRate = 0.93f;
                }else{
                    passRate = 0.77f;
                }
                break;
            case HumanConditionCircumstances.Confused:
                passRate = 0.7f;
                break;
            case HumanConditionCircumstances.Normal:
                passRate = 0.8f;
                break;
                
        }

        //+-20%入れ替わる
        passRate += RandomEx.Shared.NextFloat(-0.2f,0.2f);

        return passRate;
    }
    
    const float FINAL_BROKEN_RATE_MACHINE = 33;//機械がオーバーキルされてbrokenする最終判定率
    const float FINAL_BROKEN_RATE_LIFE = 93;//生物がオーバーキルされてbrokenする最終判定率
    /// <summary>
    /// オーバーキルされてbrokenするからの判断
    /// 殺された側がbrokenがtrueになるかの判断です。(だから殺された奴から呼び出そうよ)
    /// </summary>
    void OverKilledBrokenCalc(BaseStates Atker,float OverkillOverflow)
    {
        if(OverkillOverflow <= 0)return;//余剰ダメージが0以下なら終わり

        if(!(MyType == CharacterType.Machine || MyType == CharacterType.Life))
        {
            //被害者の自分が機械でも生物でもないなら発生せずに終わり
            return;
        }
        var OverkillOverflowMax = Atker.GetOverkillOverflowMax();//余剰ダメージの最大値を取得
        var OverkillOverflowPassRate = Atker.GetOverkillOverflowPassRate();//余剰ダメージの通過率を取得

        

        //通過した余剰ダメージ(最大値クランプ
        var OverkillOverflowPass = Mathf.Min(OverkillOverflow * OverkillOverflowPassRate,OverkillOverflowMax);
        var overkillBreakThreshold = _maxhp * OverKillBrokenRate;//オーバーキルされてbrokenする閾値

        if(OverkillOverflowPass <= overkillBreakThreshold) return;//通過した余剰ダメージが閾値を超えなかったら終わり

        //ここまで到達したら発生したが　被害者の種別による判定の発生の計算と　生命なら攻撃者の性質による発生の判定

        //機械なら　33%で完全破壊
        if(MyType == CharacterType.Machine)
        {
            if(rollper(FINAL_BROKEN_RATE_MACHINE))
            {
                broken = true;
            }
        }

        //人間なら攻撃者の性質による発生の判定
        if(MyType == CharacterType.Life)
        {
            //まず攻撃者の種別と、分岐では彼らの性質によりそもそも発生するかの判定

            if(Atker.MyType == CharacterType.Life)//攻撃者が生命なら
            {
                if(Atker.MyImpression != SpiritualProperty.pysco) return;//サイコパスでないなら終わり
            }
            if(Atker.MyType == CharacterType.Machine)//攻撃者が機械なら
            {
                var im = Atker.MyImpression;
                var hc = Atker.NowCondition;
                // サイコパスならOK
                bool isPsyco = im == SpiritualProperty.pysco;

                // ベイルの怒り ⇒ baledrival + Angry 状態
                bool isBaleRivalAngry = im == SpiritualProperty.baledrival && hc == HumanConditionCircumstances.Angry;

                // キンダーの高揚 ⇒ kindergarden + Elated 状態
                bool isKindergardenElated = im == SpiritualProperty.kindergarden && hc == HumanConditionCircumstances.Elated;

                // 上記3パターンのいずれにも当てはまらない場合は発生しない
                if (!(isPsyco || isBaleRivalAngry || isKindergardenElated))
                {
                    return; 
                }
            }
            //それ以外の種別なら生命に対して

            if(rollper(FINAL_BROKEN_RATE_LIFE))
            {
                broken = true;
            }
        }
    }
    /// <summary>
    /// 即死刃物クリティカル
    /// </summary>
    bool BladeCriticalCalculation(ref StatesPowerBreakdown dmg, ref StatesPowerBreakdown resonanceDmg, BaseStates Atker, BaseSkill skill)
    {
        var LiveHP = HP - dmg.Total;//もし即死が発生したときに、ダメージに加算される即死に足りないfloat
        var atkerBlade = Atker.TenDayValues(true).GetValueOrZero(TenDayAbility.Blades);
        var UnderBlade = TenDayValues(false).GetValueOrZero(TenDayAbility.Blades);
        var UnderPower = TenDayValuesSum(false);

        //まずしきい値発生から
        var CriticalHPThreshold = Mathf.Min(atkerBlade/150f,1f) * (5/12) *100;
        if(rollper(CriticalHPThreshold))
        {
            //攻撃者と被害者の刃物能力を比較してクリティカル発生率の計算
            var threshold = atkerBlade / UnderPower * 100;
            if(rollper(threshold))
            {
                //クリティカル発生
                dmg.TenDayAdd(TenDayAbility.Blades,LiveHP);//刃物に差分ダメージを追加 = 即死
                resonanceDmg.TenDayAdd(TenDayAbility.Blades, LiveHP);//思えダメージ用にも
                return true;
            }
        }

        return false;
    }
    /// <summary>
    /// 刃物即死クリティカルで生存するチャンス
    /// </summary>
    void CalculateBladeDeathCriticalSurvivalChance(BaseStates Atker)
    {
        if(NowPower < ThePower.high)return;//パワーが高くないなら発生しないって感じに

        var underBlade = TenDayValues(false).GetValueOrZero(TenDayAbility.Blades);
        var AtkerBlade = Atker.TenDayValues(true).GetValueOrZero(TenDayAbility.Blades);

        //刃物能力を乱数比較して被害者の方のが出たなら、
        if(rollComparison(underBlade,AtkerBlade))
        {   
            var AtkerBladeRate = AtkerBlade;
            if(Atker.NowPower == ThePower.high) AtkerBladeRate *= 1.5f;
            //生き残りHP
            var survivalHP = RandomEx.Shared.NextFloat(1,2.8f) + Mathf.Max(0,underBlade - AtkerBladeRate) * RandomEx.Shared.NextFloat(4,5);

            HP = survivalHP;//HPに生き残ったHP分を代入
        }
    }
    /// <summary>
    /// ダメージを物理耐性で減衰
    /// </summary>
    StatesPowerBreakdown ApplyPhysicalResistance(StatesPowerBreakdown dmg, BaseSkill skill)
    {
        switch(skill.SkillPhysical)
        {
            case PhysicalProperty.dishSmack:
                dmg *= DishSmackRsistance;
                break;
            case PhysicalProperty.heavy:
                dmg *= HeavyResistance;
                break;
            case PhysicalProperty.volten:
                dmg *= voltenResistance;
                break;
            //noneは物理耐性の計算無し
        }        
        return dmg;
    }
    /// <summary>
    /// 殺せないスキルの場合のクランプ
    /// </summary>
    void CantKillSkillClamp(BaseStates Atker, BaseSkill skill)
    {
        //もし攻撃者がTLOAスキルなら
        if(skill.IsTLOA)
        {
            if(Atker.HasPassive(5))//攻撃者が「TLOAではとどめがさせない」パッシブを持ってたら1割まで
            {
                HP = Mathf.Max(HP,MaxHP * 0.1f);//10%までしか減らせない
            }
            else//それ以外は3.4%まで
            {
                HP = Mathf.Max(HP,MaxHP * 0.034f);//3.4%までしか減らせない
            }
        }

        //もし攻撃者が殺せないスキルなら
        if(skill.Cantkill)
        {
            HP = Mathf.Max(HP,1);//1までしか減らせない
        }
    }
    /// <summary>
    /// かすりのダメージ倍率計算　基本値からステータス計算で減らす形
    /// 攻撃を受ける側で呼び出す
    /// </summary>
    float GetGrazeRate(BaseStates Attacker)
    {
        var baseRate = 0.35f;
        // EYEの差による減衰量を計算 (防御者EYEが攻撃者EYEに対する超過分) * 0.7%
        var atkEye = Attacker.EYE().Total;
        var defEye = EYE().Total;
        var eyeDifference = defEye - atkEye;
        //もし攻撃者のEYEが防御者のEYEより大きいなら基本値に対する現象が発生しないので、
        if(eyeDifference < 0)eyeDifference = 0;//-にならないようにゼロに
        var reduction = eyeDifference * 0.007f;//0.7%のレートを掛ける

        // 最終的なかすりダメージ倍率を計算
        var finalRate = baseRate - reduction;
        finalRate = Mathf.Max(0.02f, finalRate);//2％

        return finalRate; // 最終的なレートは 0.02f ～ 0.35f の範囲になる
    }
    /// <summary>
    /// 命中段階で分かれるダメージ分岐
    /// ダメージを受ける側で呼び出す
    /// </summary>
    void HitDmgCalculation(ref StatesPowerBreakdown dmg,ref StatesPowerBreakdown ResonanceDmg,HitResult hitResult,BaseStates Attacker)
    {
        var criticalRate = 1.5f;
        var GrazeRate = GetGrazeRate(Attacker);
        switch(hitResult)
        {
            case HitResult.CompleteEvade://完全回避
                Debug.LogError("完全回避したはずなのにDamageメゾットが呼び出されています。");
                return;
            case HitResult.Graze://かすり
                dmg *= GrazeRate;
                ResonanceDmg *= GrazeRate;
                return;
            case HitResult.Hit://ヒット
                return;//そのまま
            case HitResult.Critical://クリティカル
                dmg *=criticalRate;
                ResonanceDmg *= criticalRate;
                return;
        }
    }
    /// <summary>
    /// 魔法スキルの計算式は、基本の-計算と÷計算がブレンドするからそれ用の関数
    /// -計算から÷計算へtの値を元にシフトしていく(÷計算の最低保証性が攻撃力が防御力に負けるにつれて高まるって感じ)
    /// </summary>
    /// <param name="atkPoint"></param>
    /// <param name="defPoint"></param>
    /// <returns></returns>
    public StatesPowerBreakdown MagicBlendVSCalc(StatesPowerBreakdown atkPoint, StatesPowerBreakdown defPoint)
    {
        float k = 0.02f;      // 調整可能な傾きパラメータ
        float t = 140f;      // 調整可能な閾値シフト
        float epsilon = 0.0001f;

        // ロジスティック関数を用いて重みを計算
        // atk - def が大きければ weight は 1 に近づき、差分計算（通常スキル的な挙動）をして、
        // atk - def が小さい（またはマイナス）なら weight は 0 に近づき、比率計算（最低ダメージ保証的な挙動）の比率が高まる
        float weight = 1.0f / (1.0f + Mathf.Exp(-k * (atkPoint.Total - defPoint.Total - t)));//重みなのでtotal

        // 差分に基づくダメージ（通常スキルの挙動に近い）       
        StatesPowerBreakdown damage_diff = atkPoint - defPoint;
        
        
        // 除算に基づくダメージ（魔法特有の挙動、最低ダメージ保証の要素を持たせる）  （思った以上に効果ないから+10とかしとくわ）
        StatesPowerBreakdown damage_ratio = atkPoint / (defPoint + epsilon) + 8 + damage_diff / 1.6f;


        if(damage_diff.Total <= 0)//攻撃と防御の差が0以下なら　除算計算オンリー
        {
            return damage_ratio;
        }
        // 両者をブレンドする
        // weight が 1 に近いときは damage_diff が支配的（通常スキル的挙動）、
        // weight が 0 に近いときは damage_ratio が支配的に
        StatesPowerBreakdown baseDamage = (weight * damage_diff) + ((1.0f - weight) * damage_ratio);
        return baseDamage;
    }
    /// <summary>
    /// TLOAスキルの威力減衰
    /// 呼び出し側のダメージを受ける自分のHPの割合が条件　詳しくはTLOAスキル　を参照
    /// </summary>
    public void ApplyTLOADamageReduction(ref StatesPowerBreakdown damage,ref StatesPowerBreakdown resDamage)
    {
        //HPが38%以下ならTLOAは0.7倍まで減衰する。
        if(this.HP / this.MaxHP < 0.38f)
        {
            damage *= 0.7f;
            resDamage *= 0.7f;
        }

        return;
    }
    /// <summary>
    /// 魔法スキルのダメージ計算
    /// </summary>
    public StatesPowerBreakdown MagicDamageCalculation(BaseStates Atker, float SkillPower,StatesPowerBreakdown def)
    {
        return (MagicBlendVSCalc(Atker.ATK(Atker.SkillAttackModifier),def) * (SkillPower * 0.5f)) + SkillPower * Atker.ATK() * 0.09f;//(攻撃-対象者の防御) にスキルパワー加算と乗算
    }
    /// <summary>
    /// 魔法スキルの精神ダメージ計算
    /// </summary>
    public StatesPowerBreakdown MagicMentalDamageCalculation(BaseStates Atker,float mentalATKBoost,float SkillPowerForMental)
    {
        return  (Atker.ATK() * mentalATKBoost / MentalDEF() * (SkillPowerForMental * 0.6f)) + SkillPowerForMental * 0.7f ;//精神攻撃
    }
    /// <summary>
    /// 魔法スキル以外のダメージ計算
    /// </summary>
    public StatesPowerBreakdown NonMagicDamageCalculation(BaseStates Atker, float SkillPower,StatesPowerBreakdown def)
    {
        return ((Atker.ATK(Atker.SkillAttackModifier) - def) * SkillPower) + SkillPower;//(攻撃-対象者の防御) にスキルパワー加算と乗算
    }
    /// <summary>
    /// 魔法スキル以外の精神ダメージ計算
    /// </summary>
    public StatesPowerBreakdown NonMagicMentalDamageCalculation(BaseStates Atker,float mentalATKBoost,float SkillPowerForMental)
    {
        return  ((Atker.ATK() * mentalATKBoost - MentalDEF()) * SkillPowerForMental) + SkillPowerForMental ;//精神攻撃
    }
    
    /// <summary>
    ///オーバライド可能なダメージ関数
    /// </summary>
    /// <param name="atkPoint"></param>
    public virtual StatesPowerBreakdown Damage(BaseStates Atker, float SkillPower,float SkillPowerForMental,HitResult hitResult,ref bool isdisturbed)
    {
        var skill = Atker.NowUseSkill;

        //ダメージ直前のパッシブ効果
        PassivesOnBeforeDamage(Atker);


        //もしカウンター用の防御無視率が攻撃者が持ってたら(本来の防御無視率より多ければ)
        var defatk = skill.DEFATK;
        if (Atker._exCounterDEFATK > defatk) defatk = Atker._exCounterDEFATK;

        var def = DEF(defatk);//防御力

        def = ClampDefenseByAimStyle(skill,def);//防ぎ方(AimStyle)の不一致がある場合、クランプする

        StatesPowerBreakdown dmg, mentalDmg;
        var mentalATKBoost = Mathf.Max(Atker.TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure) - TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure),0)
        * Atker.MentalHP * 0.2f;//相手との余裕の差と精神HPの0.2倍を掛ける 

        //下の魔法スキル以外の計算式を基本計算式と考えましょう
        if(skill.IsMagic)//魔法スキルのダメージ計算
        {
            dmg = MagicDamageCalculation(Atker, SkillPower, def);
            mentalDmg = MagicMentalDamageCalculation(Atker, mentalATKBoost, SkillPowerForMental);
        }
        else//それ以外のスキルのダメージ計算
        {
            dmg = NonMagicDamageCalculation(Atker, SkillPower, def);
            mentalDmg = NonMagicMentalDamageCalculation(Atker, mentalATKBoost, SkillPowerForMental);
        }
        
        isdisturbed = false;//攻撃が乱れたかどうか　　受けた攻撃としての視点から乱れていたかどうか
        if(NowPower > ThePower.lowlow)//たるくなければ基礎山形補正がある。
        {
            isdisturbed = GetBaseCalcDamageWithPlusMinus22Percent(ref dmg);//基礎山型補正
        }

        //物理耐性による減衰
        dmg = ApplyPhysicalResistance(dmg,skill);

        //パッシブによるダメージの減衰率による絶対削減
        PassivesDamageReductionEffect(ref dmg);
        //がむしゃらな補正
        dmg = GetFrenzyBoost(Atker,dmg);

        //慣れ補正
        dmg *= AdaptToSkill(Atker, skill, dmg);

        //ここまでで被害者に向かう純正ダメージです。

        //思えのダメージ保存　追加HPは通らない
        //ただクリティカルで増幅してほしいので、各クリティカル処理で本来のdmg同様にダメージを計算するべく引数に加える必要がある。
        var ResonanceDmg = dmg;

        //vitalLayerを通る処理
        BarrierLayers(ref dmg,ref mentalDmg, Atker);

        //刃物スキルであり、ダメージがまだ残っていて、自分の体力がダメージより多いのなら、刃物即死クリティカル
        bool BladeCriticalDeath = false;
        if(skill.IsBlade && dmg.Total > 0 && HP > dmg.Total)BladeCriticalDeath = BladeCriticalCalculation(ref dmg,ref ResonanceDmg,Atker,skill);

        //命中段階による最終ダメージ計算
        HitDmgCalculation(ref dmg,ref ResonanceDmg, hitResult,Atker);

        //TLOAスキルの威力減衰 本体HPの割合に対するダメージの削り切れる限界というもの。
        ApplyTLOADamageReduction(ref dmg,ref ResonanceDmg);

        if(isdisturbed)
        {//もし乱れ攻撃なら、味方(自分も含む)のスレームパッシブのイースターノジール効果を発動を判定
            manager.MyGroup(this).PartySlaimsEasterNoshiirEffectOnEnemyDisturbedAttack(Atker,ref dmg,ref ResonanceDmg);
        }
        
        //思えのダメージ発生  各クリティカルのダメージを考慮するためクリティカル後に
        ResonanceDamage(ResonanceDmg, skill, Atker);

        var totalDmg = dmg.Total;//直接引くように変数に代入
        if(totalDmg < 0)totalDmg = 0;//0未満は0にする　逆に回復してしまうのを防止
        var tempHP = HP;//計算用にダメージ受ける前のHPを記録

        
        HP -= totalDmg;
        CantKillSkillClamp(Atker,skill);//殺せない系再代入クランプ処理
        Debug.Log("攻撃が実行された");
        schizoLog.AddLog(Atker.CharacterName + "が" + this.CharacterName + "を攻撃した-「" + totalDmg + "」ダメージを与えた");

        //攻撃者がダメージを殺すまでに与えたダメージ辞書に記録する
        Atker.RecordDamageDealtToEnemyUntilKill(dmg.Total,this);

        var totalMentalDmg = mentalDmg.Total;//直接引くように変数に代入
        if(totalMentalDmg < 0)totalMentalDmg = 0;//0未満は0にする
        //パッシブによる絶対的なダメージ食らわないクランプ処理  下回ると代入するダメージの防ぎ
        DontDamagePassiveEffect(Atker);

        MentalHP -= totalMentalDmg;//実ダメージで精神HPの最大値がクランプされた後に、精神攻撃が行われる。

        

        if(!skill.IsBlade)//刃物スキルでなければ発生
        {
            CalculateMutualKillSurvivalChance(tempHP,totalDmg,Atker);//互角一撃の生存によるHP再代入の可能性
        }
        if(BladeCriticalDeath)//刃物即死発生したのなら
        {
           CalculateBladeDeathCriticalSurvivalChance(Atker);//生存チャンス
        }
        

        //余剰ダメージを計算
        var OverKillOverFlow = totalDmg - tempHP;//余剰ダメージ

        //死んだら攻撃者のOnKillを発生
        if(Death())
        {
            Atker.OnKill(this);//攻撃者のOnkill発生

            //overKillの処理
            OverKilledBrokenCalc(Atker,OverKillOverFlow);//攻撃者、引かれる前のHP,ダメージを渡す。
        }

        //もし"攻撃者が"割り込みカウンターパッシブだったら
        var CounterPower = Atker.GetPassiveByID(1) as InterruptCounterPassive;
        if (CounterPower != null)
        {
            //攻撃者の割り込みカウンターパッシブの威力が下がる
            ///とりあえずOnAfterAttackに入れた

            //割り込みカウンターをされた = さっき「自分は連続攻撃」をしていた
            //その連続攻撃の追加硬直値分だけ、「食らわせ」というパッシブを食らう。

            //ただし範囲攻撃で巻き添えの場合もあるから追加で判定　
            if(!RecentACTSkillData.IsDone && RecentACTSkillData.Target == Atker)//直近の攻撃行動で割り込みされてたか And 割り込みしてきた(攻撃対象)のが今の割り込みパッシブ攻撃者か
            {
                var DurationTurn = RecentACTSkillData.Skill.SKillDidWaitCount;//食らうターン
                if(DurationTurn > 0)//持続ターンが存在すれば、
                {
                    ApplyPassiveBufferInBattleByID(2,Atker);//パッシブ、食らわせを入手する。
                    var hurt = GetBufferPassiveByID(2);
                    if(CanApplyPassive(hurt))//適合したなら(適合条件がある)
                    {
                        hurt.DurationTurn = DurationTurn;//持続ターンを入れる
                    }
                }
            }



            

        }

        //攻撃者のキャラ単位への攻撃後のパッシブ効果
        Atker.PassivesOnAfterAttackToTargetWithHitresult(this,hitResult);
        //攻撃者の完全単体選択なら。
        if(Atker.Target == DirectedWill.One)
        Atker.PassivesOnAfterPerfectSingleAttack(this);//攻撃者のパッシブの完全単体選択発動効果

        return dmg;
    }
    /// <summary>
    /// AIのブルートフォースダメージシミュレイト用関数
    /// </summary>
    public float SimulateDamage(BaseStates attacker, BaseSkill skill, SkillAnalysisPolicy policy)
    {
        //スキルパワー取得
        //damage関数と違う内容　spread,敵による分散値が乗算されない
        var simulateHP = HP;//計算用HP
        var simulateMentalHP = MentalHP;//計算用精神HP

        //スキルパワーの精神属性による計算
        var modifier = GetSkillVsCharaSpiritualModifier(skill.SkillSpiritual, attacker);
        var SpiritualModifierPercentage = 0.2f;//IsTLOAスキル以外は20%の精神補正
        if(skill.IsTLOA) SpiritualModifierPercentage = 0.4f;//IsTLOAなら40%の精神補正


        var modifierForSkillPower = modifier.GetValue(SpiritualModifierPercentage);//精神補正値
        var modifierForSkillPowerForMental = modifier.GetValue();//精神補正値100%
        if(attacker.NowUseWeapon.IsBlade) modifierForSkillPower = 1.0f;//刃物武器なら精神補正なし
        
        //精神補正のオプションがfalseなら精神補正なし
        if(!policy.spiritualModifier)
        {
            modifierForSkillPower = 1.0f;
            modifierForSkillPowerForMental = 1.0f;
        }
        
        //TLOAスキルならゆりかごされたスキルレベルを参照する(IsTLOAを渡して判断)
        var skillPower = skill.SkillPowerCalc(skill.IsTLOA) * modifierForSkillPower;
        var skillPowerForMental = skill.SkillPowerForMentalCalc(skill.IsTLOA) * modifierForSkillPowerForMental;

        //防御力（ポリシーに応じた簡易/完全シミュレーション）
        StatesPowerBreakdown def;
        if (policy.SimlatePerfectEnemyDEF)
        {
            // 従来通りの完全なDEF計算を使用（パッシブ補正やAimStyle排他などすべて含む）
            def = DEF();
        }
        else if (policy.SimlateEnemyDEF)
        {
            // 基本防御力のみ：b_b_def + 共通TenDay係数（AimStyle排他・パッシブ補正などは含めない）
            var basic = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_def);
            foreach (var kv in DefensePowerConfig.CommonDEF)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    basic.TenDayAdd(kv.Key, td * kv.Value);
                }
            }
            def = basic;
        }
        else
        {
            // DEFを考慮しない
            def = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        }


        //精神攻撃ブーストはシミュレートでは考慮しない
        var mentalATKBoost = 1.0f;

        //ダメージを計算
        StatesPowerBreakdown dmg, mentalDmg;
        if(skill.IsMagic)//魔法スキルのダメージ計算
        {
            dmg = MagicDamageCalculation(attacker, skillPower, def);
            mentalDmg = MagicMentalDamageCalculation(attacker, mentalATKBoost, skillPowerForMental);
        }
        else//それ以外のスキルのダメージ計算
        {
            dmg = NonMagicDamageCalculation(attacker, skillPower, def);
            mentalDmg = NonMagicMentalDamageCalculation(attacker, mentalATKBoost, skillPowerForMental);
        }

        //物理耐性による減衰(オプション)
        if(policy.physicalResistance)
        {
            dmg = ApplyPhysicalResistance(dmg,skill);
        }

        //追加HP（バリア層）のシミュレート処理（オプション）
        if(policy.SimlateVitalLayerPenetration)
        {
            SimulateBarrierLayers(ref dmg, ref mentalDmg, attacker);
        }

        //シミュレートするダメージの種類で分岐
        if(policy.damageType == SimulateDamageType.dmg)
        {
            return dmg.Total;
        }
        if(policy.damageType == SimulateDamageType.mentalDmg)
        {
            return mentalDmg.Total;
        }
        Debug.LogError("BaseStatesのダメージシミュレート関数に渡されたダメージタイプが正しくありません");
        return 0;
    }

    /// <summary>
    /// バリア層のシミュレート処理（実際のバリア層を変更せずにダメージ計算のみ行う）
    /// PenetrateLayerのロジックを再現しつつ、実体を変更しない
    /// </summary>
    private void SimulateBarrierLayers(ref StatesPowerBreakdown dmg, ref StatesPowerBreakdown mentalDmg, BaseStates atker)
    {
        // バリア層のシミュレート用データを作成（実体は変更しない）
        var simulateVitalLayers = new List<(float layerHP, float maxLayerHP, BaseVitalLayer originalLayer)>();
        
        // 元のバリア層リストから必要な情報をコピー
        foreach(var layer in _vitalLayerList)
        {
            simulateVitalLayers.Add((layer.LayerHP, layer.MaxLayerHP, layer));
        }

        for (int i = 0; i < simulateVitalLayers.Count;)
        {
            var (layerHP, maxLayerHP, originalLayer) = simulateVitalLayers[i];
            var skillPhy = atker.NowUseSkill.SkillPhysical;
            
            // PenetrateLayerのロジックを再現（実体を変更せずに）
            var (newDmg, newMentalDmg, newLayerHP, isDestroyed) = SimulatePenetrateLayer(
                dmg, mentalDmg, layerHP, originalLayer, skillPhy);
            
            dmg = newDmg;
            mentalDmg = newMentalDmg;

            if (isDestroyed)
            {
                // このレイヤーは破壊された
                simulateVitalLayers.RemoveAt(i);
                // リストを削除したので、 i はインクリメントしない
                
                //破壊慣れまたは破壊負け
                var kerekere = atker.TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                if (skillPhy == PhysicalProperty.heavy)//暴断なら破壊慣れ
                {
                    dmg += dmg * 0.015f * kerekere;
                }
                if (skillPhy == PhysicalProperty.volten)//vol天なら破壊負け
                {
                    dmg -= dmg * 0.022f * (atker.b_ATK.Total - kerekere);
                    //b_atk < kerekereになった際、減らずに逆に威力が増えるので、そういう場合の演出差分が必要
                }
            }
            else
            {
                // レイヤーが残ったら i を進める
                simulateVitalLayers[i] = (newLayerHP, maxLayerHP, originalLayer);
                i++;
            }

            // dmg が 0 以下になったら、もうこれ以上削る必要ない
            if (dmg.Total <= 0f)
            {
                break;
            }
        }
    }

    /// <summary>
    /// PenetrateLayerのロジックをシミュレート（実体を変更しない）
    /// </summary>
    /// <returns>(新しいdmg, 新しいmentalDmg, 新しいlayerHP, 破壊されたかどうか)</returns>
    private (StatesPowerBreakdown dmg, StatesPowerBreakdown mentalDmg, float layerHP, bool isDestroyed) 
        SimulatePenetrateLayer(StatesPowerBreakdown dmg, StatesPowerBreakdown mentalDmg, float layerHP, 
                              BaseVitalLayer originalLayer, PhysicalProperty impactProperty)
    {
        // 1) 物理属性に応じた耐性率を取得
        float resistRate = 1.0f;
        switch (impactProperty)
        {
            case PhysicalProperty.heavy:
                resistRate = originalLayer.HeavyResistance;
                break;
            case PhysicalProperty.volten:
                resistRate = originalLayer.voltenResistance;
                break;
            case PhysicalProperty.dishSmack:
                resistRate = originalLayer.DishSmackRsistance;
                break;
        }

        // 2) 軽減後の実ダメージ
        StatesPowerBreakdown dmgAfter = dmg * resistRate;

        // 3) レイヤーHPを削る（シミュレート）
        StatesPowerBreakdown leftover = layerHP - dmgAfter; // leftover "HP" => もしマイナスなら破壊

        //精神dmgが現存する攻撃に削られる前のLayerを通る
        mentalDmg -= layerHP * (1 - originalLayer.MentalPenetrateRatio);//精神HPの通過率の分だけ通るので、つまり100%ならmentalDMgの低減はないということ。

        if (leftover <= 0f)
        {
            // 破壊された
            StatesPowerBreakdown overkill = -leftover; // -negative => positive
            var tmpHP = layerHP;//仕組みC用に今回受ける時のLayerHPを保存。
            
            // 仕組みの違い
            switch (originalLayer.ResistMode)
            {
                case BarrierResistanceMode.A_SimpleNoReturn:
                    // Aは一度軽減した分は戻さない: overkill をそのまま次へ
                    return (overkill, mentalDmg, 0f, true);

                case BarrierResistanceMode.B_RestoreWhenBreak:
                    // Bは「軽減後ダメージ」分を元に戻す => leftover を "÷ resistRate" で拡大
                    StatesPowerBreakdown restored = overkill / resistRate;
                    return (restored, mentalDmg, 0f, true);

                case BarrierResistanceMode.C_IgnoreWhenBreak:
                    // Cは元攻撃 - 現在のLayerHP
                    StatesPowerBreakdown cValue = dmg - tmpHP;
                    return (cValue, mentalDmg, 0f, true);
                    
                case BarrierResistanceMode.C_IgnoreWhenBreak_MaxHP:
                    // Cは元攻撃 - 最大LayerHP
                    StatesPowerBreakdown cmValue = dmg - originalLayer.MaxLayerHP;
                    return (cmValue, mentalDmg, 0f, true);
            }
        }
        else
        {
            // バリアで耐えた（破壊されなかった）
            float newLayerHP = leftover.Total;//レイヤーHPに戻すのでtotal
            StatesPowerBreakdown zeroDmg = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0f); // 余剰ダメージなし
            return (zeroDmg, mentalDmg, newLayerHP, false);
        }
        
        // デフォルト（通常ここには来ない）
        return (dmg, mentalDmg, layerHP, false);
    }
    /// <summary>
    /// パッシブの毒ダメや、パッシブリンク等の単純なfloatダメージ用
    /// TLOAスキルなどの殺せない系のクランプ処理だけ入る感じ
    /// </summary>
    /// <param name="Atker">攻撃者</param>
    /// <param name="damage">ダメージ</param>
    /// <param name="LayerDamage">VitalLayerを通すかどうか</param>
    public void RatherDamage(BaseStates Atker,StatesPowerBreakdown damage,bool LayerDamage,float DamageRatio)
    {
        StatesPowerBreakdown notUseDamage = damage;//使わないが、引数に渡す必要がある
        damage *= DamageRatio;//ダメージの倍率を掛ける

        //vitalLayerを通る処理
        if(LayerDamage)
        {
            BarrierLayers(ref damage,ref notUseDamage, Atker);
        }

        HP -= damage.Total;
        CantKillSkillClamp(Atker,Atker.NowUseSkill);//殺せない系再代入クランプ処理
    }
    /// <summary>
    /// 思えダメージの精神属性合致補正
    /// </summary>
    const float SPIRITUAL_MODIFIER = 1.29f;
    /// <summary>
    /// 思えダメージが発生する強さの比率のしきい値
    /// </summary>
    const float RESONANCE_POWER_THRESHOLD = 1.4f;
    /// <summary>
    /// 思えのダメージの基礎値
    /// </summary>
    const float BASEDAMGE_TENDAYS = 0.06f;
    /// <summary>
    /// 思えダメージの精神属性の種類数、精神ポテンシャルによる除算DEFの数に掛ける係数
    /// </summary>
    const float SPRITUAL_POTENTIAL_DEF_COED =  0.06f;
    /// <summary>
    /// 思えダメージをランダマイズ出来るスキル数のしきい値
    /// </summary>
    const int SKILL_COUNT_THRESHOLD_DAMAGE_RANDOMIZE = 6;
    /// <summary>
    /// 思えダメージをスキルの数でランダマイズする
    /// </summary>
    float ResonanceDamageRandomizeBySkillCount(float dmg)
    {
        //スキル数がしきい値以下ならランダマイズしない
        if(SkillList.Count <= SKILL_COUNT_THRESHOLD_DAMAGE_RANDOMIZE) return dmg;
        //ランダマイズする数
        var RandomizeCalcCount = SkillList.Count - SKILL_COUNT_THRESHOLD_DAMAGE_RANDOMIZE;
        
        var maxFactor = 1.0f;
        var minFactor = 1.0f;
        for(int i = 0; i < RandomizeCalcCount; i++)
        {
            var RandomizeUpper = RandomEx.Shared.NextFloat(0.01f,0.015f);//ランダマイズの上振れ
            var RandomizeLower = RandomEx.Shared.NextFloat(0.01f,0.03f);//ランダマイズの下振れ

            maxFactor += RandomizeUpper;
            minFactor -= RandomizeLower;
        }
        if(minFactor < 0) minFactor = 0;
        if(minFactor > maxFactor) minFactor = maxFactor;//念のため
        return dmg * RandomEx.Shared.NextFloat(minFactor,maxFactor);
    }
    /// <summary>
    /// 思えのダメージ処理
    /// ダメージと精神ダメージを食らう前に判定され、追加HPで防がれない
    /// </summary>
    public void ResonanceDamage(StatesPowerBreakdown dmg, BaseSkill skill, BaseStates Atker)
    {   
        //攻撃者がこちらに対してどれだけ強いか
        var powerRatio = Atker.TenDayValuesSum(false) / TenDayValuesSum(false);//思えダメージはスキルの十日能力補正なし
        //相手が自分より定数倍強いとダメージが発生する
        if(powerRatio < RESONANCE_POWER_THRESHOLD) return;

        //被害者の精神HP現在値とHPの平均値と最大HPの割合
        var myBodyAndMentalAverageRatio = (HP + MentalHP) / 2 / MaxHP;
        //思えの食らう割合。
        var ResonanceDangerRatio = 1.0f - myBodyAndMentalAverageRatio;
        //最大0.8　8割分食らってる所までダメージが伸びきることを想定する
        ResonanceDangerRatio = Mathf.Min(0.8f,ResonanceDangerRatio);

        //攻撃者と攻撃スキルの精神属性が一致してた場合にかかる補正は各case文で計算
        
        //十日能力による基礎思えダメージ
        var BaseDmg =dmg.TenDayValuesSum * BASEDAMGE_TENDAYS;//十日能力のダメージ分定数分

        //相手がどのくらい強いかの倍率
        var DamageMultipilerByPowerRatio = powerRatio - (RESONANCE_POWER_THRESHOLD - 1.0f);
        
        //人間状況による分岐
        switch(NowCondition)
        {
            case HumanConditionCircumstances.Painful:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.UnextinguishedPath);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.FlameBreathingWife);

                if(skill.SkillSpiritual == SpiritualProperty.devil || skill.SkillSpiritual == SpiritualProperty.liminalwhitetile)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.devil || Atker.MyImpression == SpiritualProperty.liminalwhitetile)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Optimistic:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.NightDarkness);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.StarTersi);

                if(skill.SkillSpiritual == SpiritualProperty.doremis)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.doremis)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Elated:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.dokumamusi);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.SpringWater);
                //どの精神属性も効かない
                break;
            case HumanConditionCircumstances.Resolved:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.TentVoid);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Vond);
                if(skill.SkillSpiritual == SpiritualProperty.pysco)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.pysco)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Angry:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.HeatHaze);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Rain);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.ColdHeartedCalm);
                if(skill.SkillSpiritual == SpiritualProperty.liminalwhitetile)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.liminalwhitetile)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Doubtful:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.HumanKiller);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.PersonaDivergence);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Enokunagi);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Blades);
                if(skill.SkillSpiritual == SpiritualProperty.doremis || skill.SkillSpiritual == SpiritualProperty.pysco)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.doremis || Atker.MyImpression == SpiritualProperty.pysco)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Confused:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.SilentTraining);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Miza);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Raincoat);
                if(skill.SkillSpiritual == SpiritualProperty.doremis || skill.SkillSpiritual == SpiritualProperty.sacrifaith)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.doremis || Atker.MyImpression == SpiritualProperty.sacrifaith)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
        }

        var finalDamage = BaseDmg * ResonanceDangerRatio * DamageMultipilerByPowerRatio;

        //被害者側の精神属性の種類　= 精神ポテンシャルで除算をする。
        var potential = GetMySpiritualPotential();
        finalDamage *= 1 - (potential * SPRITUAL_POTENTIAL_DEF_COED);

        //スキルの数による除算
        finalDamage = ResonanceDamageRandomizeBySkillCount(finalDamage);
        //ダメージを反映
        NowResonanceValue -= finalDamage;
    }
    /// <summary>
    /// 人間状況が普調なら　行動を起こすたびに思えの値が回復する。
    /// AttackCharaで
    /// </summary>
    public void ResonanceHealingOnBattle()
    {
        if(NowCondition == HumanConditionCircumstances.Normal)
        {
            //とりあえず最大値 3~11%ランダム回復ってことで。
            var HealAmount = ResonanceValue * RandomEx.Shared.NextFloat(0.03f,0.11f);
            ResonanceHeal(HealAmount);
        }
    }
    
    /// <summary>
    /// ヒール
    /// </summary>
    /// <param name="HealPoint"></param>
    public virtual float Heal(float HealPoint)
    {
        if(!Death())
        {
            HP += HealPoint * (PassivesHealEffectRate() / 100f);//パッシブ由来の補正がかかる
            Debug.Log("ヒールが実行された");
            return HealPoint;
        }

        return 0f;
    }
    /// <summary>
    /// 精神HPのヒール処理
    /// </summary>
    /// <param name="HealPoint"></param>
    public virtual void MentalHeal(float HealPoint)
    {
        if(!Death())
        {
            MentalHP += HealPoint;
            Debug.Log("精神ヒールが実行された");
        }
    }
    /// <summary>
    /// 命中凌駕の判定関数　引数倍命中が回避を凌駕してるのなら、スキル命中率に影響を与える
    /// </summary>
    private float AccuracySupremacy(float atkerEye, float undAtkerAgi, float multiplierThreshold = 2.5f)
    {
        var supremacyMargin = 0f;
        var modifyAgi = undAtkerAgi * multiplierThreshold;//補正されたagi
        if (atkerEye >= modifyAgi)//攻撃者のEYEが特定の倍被害者のAGIを上回っているならば、
        {
            supremacyMargin = (atkerEye - modifyAgi) / 2;//命中が引数倍された回避を超した分　÷　2
        }
        return supremacyMargin;
    }
    
    /// <summary>
    /// スキルにより影響された回避補正率
    /// </summary>
    float _skillEvasionModifier = 1f;
    /// <summary>
    /// スキルにより影響された攻撃補正率
    /// </summary>
    float _skillAttackModifier = 1f;
    
    /// <summary>
    /// 平準化する回避補正率
    /// </summary>
    float _BaseEvasionModifier = 1f;
    /// <summary>
    /// 平準化する攻撃補正率
    /// </summary>
    float _BaseAttackModifier = 1f;
    /// <summary>
    /// 回避率でAGIに掛けるスキルにより影響された回避補正率　
    /// 落ち着きターン経過により減衰する。
    /// 回避率の計算の際に最終回避率としてAGIと掛ける
    /// </summary>
    float SkillEvasionModifier
    {
        get
        {
            var calmDownModifier = CalmDownCount;//落ち着きカウントによる補正
            var calmDownModifierMax = CalmDownCountMax;
            if (calmDownModifier < 0)calmDownModifier = 0;//落ち着きカウントがマイナスにならないようにする

            // カウントダウンの進行度に応じて線形補間
            // カウントが減るほど _BaseEvasionModifier に近づく
            float progress = 1.0f - (calmDownModifier / calmDownModifierMax);
            
            // 線形補間: _skillEvasionModifier から _BaseEvasionModifier へ徐々に変化
            return _skillEvasionModifier + (_BaseEvasionModifier - _skillEvasionModifier) * progress;
        }
    }
    /// <summary>
    /// 攻撃力でATKに掛けるスキルにより影響された攻撃補正率
    /// 落ち着きターン経過により減衰する。
    /// 実際の敵HPに対する減算処理のみに参照し、尚且つなるべく素に近いATK()の補正積み重ねの初期に掛けられる。
    /// </summary>
    float SkillAttackModifier
    {
        get
        {
            var calmDownModifier = CalmDownCount;//落ち着きカウントによる補正
            var calmDownModifierMax = CalmDownCountMax;
            if (calmDownModifier < 0)calmDownModifier = 0;//落ち着きカウントがマイナスにならないようにする

            // カウントダウンの進行度に応じて線形補間
            // カウントが減るほど _BaseAttackModifier に近づく
            float progress = 1.0f - (calmDownModifier / calmDownModifierMax);
            
            // 線形補間: _skillAttackModifier から _BaseAttackModifier へ徐々に変化
            return _skillAttackModifier + (_BaseAttackModifier - _skillAttackModifier) * progress;
        }
    }
    /// <summary>
    /// 落ち着きカウント
    /// 回避率や次攻撃者率　の平準化に用いられる
    /// </summary>
    int CalmDownCount = 0;
    /// <summary>
    /// 落ち着きカウントの最大値
    /// 影響された補正率がどの程度平準化されているかの計算に用いるために保存する。
    /// </summary>
    int CalmDownCountMax;
    /// <summary>
    /// 落ち着きカウントの最大値算出
    /// </summary>
    int CalmDownCountMaxRnd => RandomEx.Shared.NextInt(4, 8);
    /// <summary>
    /// 落ち着きカウントのカウント開始準備
    /// スキル回避率もセット
    /// </summary>
    public void CalmDownSet(float EvasionModifier = 1f, float AttackModifier = 1f)
    {
        CalmDownCountMax = CalmDownCountMaxRnd;//乱数から設定して、カウントダウンの最大値を設定
        CalmDownCount = CalmDownCountMax;//カウントダウンを最大値に設定
        CalmDownCount++;//NextTurnで即引かれるので調整　　落ち着き#カウント対処を参照して
        _skillEvasionModifier = EvasionModifier;//スキルにより影響された回避補正率をセット
        _skillAttackModifier = AttackModifier;//スキルにより影響された攻撃補正率をセット
    }
    /// <summary>
    /// 落ち着きカウントダウン
    /// </summary>
    void CalmDownCountDec()
    {
        CalmDownCount--;
    }
    /// <summary>
    /// 意図的に落ち着きカウントをゼロにすることにより、落ち着いた判定にする。
    /// </summary>
    void CalmDown()
    {
        CalmDownCount = 0;
    }
    /// <summary>
    /// 命中回避計算で使用する回避率
    /// </summary>
    float EvasionRate(float baseAgi,BaseStates Attacker)
    {
        float evasionRate;

        evasionRate = baseAgi * SkillEvasionModifier;

        if(Attacker.BattleFirstSurpriseAttacker)//bm最初のターンで先手攻撃を受ける場合
        evasionRate  = baseAgi * 0.7f;//0.7倍で固定

        //パッシブ由来のキャラクタ限定回避補正
        evasionRate *= PassivesEvasionPercentageModifierByAttacker();

        return evasionRate;
    }
    /// <summary>
    /// ミニマムヒットチャンスの発生確率を計算。
    /// </summary>
    float CalcMinimumHitChancePer(BaseStates Attacker,BaseStates Defender)
    {
        const float synergy_threshold = 7f;//高め合いボーナスの発生しきい値　　0~100で
        const float  synergy_bonus_per = 0.2f;//高め合いボーナスの係数　ボーナスの大小を調整するのならここで。
        const float KereKereModifier_per = 0.09f;//ケレケレによる追加補正時にケレケレに掛ける係数

        var AtkerKerenRate = Attacker.PassivesAttackACTKerenACTRate();//攻撃側のケレン行動パーセント
        var DefenderKerenRate = Defender.PassivesDefenceACTKerenACTRate();//防御側のケレン行動パーセント

        //基本、攻撃側か防御側のどちらか大きい方が使われます
        var MinimumHitChanceRate = Math.Max(AtkerKerenRate, DefenderKerenRate);

        //もし両方とも高め合いボーナスの発生しきい値を上回っていたら発生
        if(AtkerKerenRate > synergy_threshold && DefenderKerenRate > synergy_threshold)
        {
            //どちらもデフォ値を引いて、加算する。
            var synergyPotential = AtkerKerenRate - KerenACTRateDefault + (DefenderKerenRate - KerenACTRateDefault);
            //調整用の係数を掛ける
            synergyPotential *= synergy_bonus_per;
            //基本確立に加算
            MinimumHitChanceRate += synergyPotential;
        }

        //大きい方の値が偶然の定数を上回っていた場合、その大きい方の十日能力ケレケレにより加算される。(高め合いボーナス計算後に)
        if(AtkerKerenRate>=DefenderKerenRate)//攻撃者側が大きいなら  攻撃者側のがバトルの主導は握りがちだと思うので、同値の場合も含める。
        {
            if(AtkerKerenRate > KerenACTRateDefault)//偶然の定数を上回っていたら
            {
                var AtkerKereKere = Attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                MinimumHitChanceRate += AtkerKereKere * KereKereModifier_per;
            }
        }
        else
        {
            if(DefenderKerenRate > KerenACTRateDefault)//偶然の定数を上回っていたら
            {
                var DefenderKereKere = Defender.TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                MinimumHitChanceRate += DefenderKereKere * KereKereModifier_per;
            }
        }
        
        
        return MinimumHitChanceRate;
    }
    /// <summary>
    /// 攻撃者と防御者とスキルを利用してヒットするかの計算
    /// </summary>
    private HitResult IsReactHIT(BaseStates Attacker)
    {
        schizoLog.AddLog("IsReactHITが呼ばれた",true);
        var skill = Attacker.NowUseSkill;
        var minusMyChance = 0f;
        var minimumHitChancePer = CalcMinimumHitChancePer(Attacker,this);//ミニマムヒットチャンスの発生確率
        if(skill.IsMagic)minimumHitChancePer = 2f;

        //vanguardじゃなければ攻撃者の命中減少
        if (!manager.IsVanguard(Attacker))
        {
            //スキルのその場DontMove性の担保のため、前のめりの選択がないスキルは後衛でも命中低下しない
            if(skill.CanSelectAggressiveCommit)
            {//だから前のめり選べるスキルの場合のみ命中低下する。
                minusMyChance += AGI().Total * 0.2f;//チャンス計算だけだからTotal    
            }
        }

        if (minusMyChance > Attacker.EYE().Total)//マイナス対策
        {
            minusMyChance = Attacker.EYE().Total;
        }

        var minimumHitChanceResult= HitResult.CompleteEvade;//命中回避計算外のミニマムヒットチャンス
        if(rollper(minimumHitChancePer))//ミニマムヒットチャンス  ケレン行動パーセントの確率でかすりとクリティカルの計算
        {
            //三分の一で二分の一計算、三分の二ならステータス計算に入ります
            //三分の1でかすりとクリティカルは完全二分の一計算
            if(RandomEx.Shared.NextFloat(3) < 1)
            {
                if(RandomEx.Shared.NextFloat(2) < 1)
                {
                    minimumHitChanceResult = HitResult.Critical;
                    schizoLog.AddLog("ミニマムヒットチャンスの確率計算-クリティカル",true);
                }
                else
                {
                    minimumHitChanceResult = HitResult.Graze;
                    schizoLog.AddLog("ミニマムヒットチャンスの確率計算-かすり",true);
                }
            }else
            {//残り三分の二で、ステータス比較の計算
                var atkerCalcEYEAGI = Attacker.EYE().Total + Attacker.AGI().Total *0.6f;//minusMychanceは瀬戸際の攻防計算なので使用しない
                var defCalcEYEAGI = EYE().Total * 0.8f + AGI().Total;

                // 確率計算に使用する実効値を準備       多い方の値を1.7倍することで、より強さの絶対性を高める。
                float effectiveAtkerCalc = atkerCalcEYEAGI;
                float effectiveDefCalc = defCalcEYEAGI;
                // 多い方の値を1.7倍する
                if (atkerCalcEYEAGI > defCalcEYEAGI)
                {
                    effectiveAtkerCalc = atkerCalcEYEAGI * 1.7f;
                }
                else if (defCalcEYEAGI > atkerCalcEYEAGI)
                {
                    effectiveDefCalc = defCalcEYEAGI * 1.7f;
                }
                // atkerCalcEYEAGI == defCalcEYEAGI の場合は、どちらも変更しない（元の確率計算と同じ）

                
                if(RandomEx.Shared.NextFloat(effectiveAtkerCalc + effectiveDefCalc) < effectiveAtkerCalc)
                {
                    minimumHitChanceResult = HitResult.Critical;//攻撃者側のステータスが乱数で出たなら、クリティカル
                    schizoLog.AddLog("ミニマムヒットチャンスのステータス比較計算-クリティカル",true);
                }
                else
                {
                    minimumHitChanceResult = HitResult.Graze;//そうでなければかすり
                    schizoLog.AddLog("ミニマムヒットチャンスのステータス比較計算-かすり",true);
                }
            }
        }

        //術者の命中+被害者の自分の回避率　をMAXに　ランダム値が術者の命中に収まったら　命中。
        schizoLog.AddLog(Attacker.CharacterName + "の命中率:" + Attacker.EYE().Total +CharacterName + "の回避率:" + EvasionRate(AGI().Total,Attacker),true);
        if (RandomEx.Shared.NextFloat(Attacker.EYE().Total + EvasionRate(AGI().Total,Attacker)) < Attacker.EYE().Total - minusMyChance || minimumHitChanceResult != HitResult.CompleteEvade)
        {
            var hitResult = minimumHitChanceResult;//ミニマムヒット前提でヒット結果変数に代入
            //ミニマムヒットがなく、かつ、通常の命中率が満たされた場合
            if(minimumHitChanceResult == HitResult.CompleteEvade)
            {
                hitResult = HitResult.Hit;//スキル命中に渡すヒット結果に通常のHitを代入
                schizoLog.AddLog("IsReactHit-Hit",true);
            }
            schizoLog.AddLog($"通常Hitにより更なるスキル命中計算を実行",true);
            //スキルそのものの命中率 スキル命中率は基本独立させて、スキル自体の熟練度系ステータスで補正する？
            return skill.SkillHitCalc(this,AccuracySupremacy(Attacker.EYE().Total, AGI().Total), hitResult);
        }
        //回避されたので、まずは魔法スキルなら魔法かすりする　三分の一で
        //事前魔法かすり判定である。(攻撃性質スキル以外はスキル命中のみで魔法かすり判定をするという違いがある為。)
        if(skill.IsMagic && RandomEx.Shared.NextFloat(3) < 1)
        {
            //スキルそのものの命中率 スキル命中率は基本独立させて、スキル自体の熟練度系ステータスで補正する？
            return skill.SkillHitCalc(this,AccuracySupremacy(Attacker.EYE().Total, AGI().Total), HitResult.Graze, true);
        }


        //スキルが爆破型で、なおかつ被害者の自分が前のめりなら完全回避のはずがかすりになる
        if(skill.DistributionType == AttackDistributionType.Explosion && manager.IsVanguard(this))
        {
            var hitResult = HitResult.Graze;
            //が、AGI比較て勝ってたらそれを免除し本来の完全回避へ

            //三倍以上越してると84%で避けられる
            if(Attacker.AGI().Total * 3 < AGI().Total)
            {
                if(rollper(84))
                {
                    hitResult = HitResult.CompleteEvade;
                }
            }else
            {
                //攻撃者のAGIを1.6倍以上越していると、二分の一で避けられる。
                if(Attacker.AGI().Total * 1.6 < AGI().Total)
                {
                    if(RandomEx.Shared.NextFloat(2) < 1)
                    {
                        hitResult = HitResult.CompleteEvade;
                    }
                }
            }

            //爆破型なのでかすりだが、そもそものスキル命中の計算をする介する
            return skill.SkillHitCalc(this,AccuracySupremacy(Attacker.EYE().Total, AGI().Total), hitResult);
        }


        return HitResult.CompleteEvade;
    }

/// <summary>
/// nightinknightの値に応じて現在の「引き締める」補正段階を返す関数 </summary>
/// <returns>補正段階 は増えていく。/returns>
int GetTightenMindCorrectionStage()
{
    float nightinknightValue = TenDayValues(false).GetValueOrZero(TenDayAbility.NightInkKnight);

    nightinknightValue /= 10;
    nightinknightValue = Mathf.Floor(nightinknightValue);
    if(NowPower == ThePower.high && RandomEx.Shared.NextFloat(1) < 0.5f)  nightinknightValue += 1;//パワーが高く、二分の一の確率を当てると、補正段階が1増える

    return (int)nightinknightValue;
}

/// <summary>
/// 今回攻撃された際のAimStyle で短期記憶(TransformCount など)を更新する
/// </summary>
private bool UpdateAimStyleMemory(AimStyle newAimStyle, int tightenStage)
{
    // 現在の短期記憶
    var mem = _aimStyleMemory;

    // 1) まだ何も対応していない or 前回の TargetAimStyle と違う ならリセット
    if (mem.TargetAimStyle == null || mem.TargetAimStyle.Value != newAimStyle)
    {
        // 新しく対応を始める
        mem.TargetAimStyle      = newAimStyle;
        mem.TransformCount      = 0;

        // TightenStage を加味して「対応に必要なカウントMax」を求める
        mem.TransformCountMax   = CalcTransformCountMax(tightenStage, newAimStyle);

    }
    
        // 変革カウントを進める
        int increment = CalcTransformCountIncrement(tightenStage);

        mem.TransformCount += increment;

        // 更新を反映
        _aimStyleMemory = mem;

        if(mem.TransformCount >= mem.TransformCountMax)//カウント上限を超えたらリセットし変更成功の項を返す
        {
            mem.TransformCount = 0;
            mem.TargetAimStyle = null;
            mem.TransformCountMax = 0;
            // 更新を反映
            _aimStyleMemory = mem;
           return true;
        }
    return false;
    
}
/// <summary>
/// AimStyleを食らった時、何カウント増やすかを決める
/// ※ tightenStageが高いほど変革スピードが速い、など
/// </summary>
private int CalcTransformCountIncrement(int tightenStage)
{
    var rndmin = 0;
    var rndmax = tightenStage;
    if(NowPower< ThePower.medium)rndmax -= 1;
    if(tightenStage <2)return 1;//1以下なら基本値のみ
    if(tightenStage>5) rndmin = tightenStage/6;//6以上なら、補正段階の1/6が最小値
    return 1 + RandomEx.Shared.NextInt(rndmin, rndmax);//2以降なら補正段階分乱数の最大値が増える
}
/// <summary>
/// 引き締め段階(tightenStage)と、新AimStyle に応じて必要な最大カウントを算出
/// </summary>
    private int CalcTransformCountMax(int tightenStage, AimStyle AttackerStyle)
    {
        //AIMSTYLEの組み合わせ辞書により、必要な最大カウントを計算する
        var count = DefenseTransformationThresholds[(AttackerStyle, NowDeffenceStyle)];
        if(tightenStage>=2)
        {
            if(RandomEx.Shared.NextFloat(1)<0.31f + TenDayValues(false).GetValueOrZero(TenDayAbility.NightInkKnight)*0.01f)
        {
                count -= 1;

        }
        }
        
        if(tightenStage >= 5){
            if(RandomEx.Shared.NextFloat(1)<0.8f)
                {
                    count-=1;
                }
        }

    return  count;
    }

    /// <summary>防ぎ方の切り替え </summary>
    private void SwitchDefenceStyle(BaseStates atker)
    {
        if(atker.NowBattleProtocol == BattleProtocol.none)
        {
            NowDeffenceStyle = AimStyle.none;//戦闘規格がない(フリーハンドスキル)なら、防ぎ方もnone(防御排他ステがない)
            return;
        } 
        var skill = atker.NowUseSkill;
        var pattern = DefaultDefensePatternPerProtocol[atker.NowBattleProtocol];

        if(!skill.NowConsecutiveATKFromTheSecondTimeOnward()){//単回攻撃または初回攻撃なら  (戦闘規格noneが入ることを想定)

            var per = 1f;
            if(GetTightenMindCorrectionStage()>=2)per=0.75f;//補正段階が2以上になるまで75%の確率で切り替えます、それ以降は100%で完全対応

           if(RandomEx.Shared.NextFloat(1) < pattern.a)//パターンAなら 
           {
            skill.DecideNowMoveSet_A0_B1(0);
            skill.SetSingleAimStyle(pattern.aStyle);//攻撃者側スキルにデフォルトの狙い流れを設定

            if(RandomEx.Shared.NextFloat(1)<per){
                NowDeffenceStyle =  pattern.aStyle;
                //攻撃者のAimStyle = 被害者のAimStyle　となるので狙い流れを対応できている。
            }else{
                NowDeffenceStyle = GetRandomAimStyleExcept(pattern.aStyle);//aStyle以外のAimStyleをランダムに選びます
            }
           }
           else                                         //パターンBなら
           {
            skill.DecideNowMoveSet_A0_B1(1);
            skill.SetSingleAimStyle(pattern.bStyle);//攻撃者側スキルにデフォルトの狙い流れを設定

            if(RandomEx.Shared.NextFloat(1)<per){
                NowDeffenceStyle =  pattern.bStyle;
                
            }else{
                NowDeffenceStyle = GetRandomAimStyleExcept(pattern.bStyle);//bStyle以外のAimStyleをランダムに選びます
            }
           }

           
        }else{                                              //連続攻撃中なら　　(戦闘規格noneを連続攻撃のmovesetに入れないこと前提)
            var AtkAimStyle = skill.NowAimStyle();//攻撃者の現在のAimStyleを取得
            
            if (AtkAimStyle == NowDeffenceStyle) return;// 既に同じAimStyleなら何もしない

            var TightenMind = GetTightenMindCorrectionStage();//現在の自分の引き締め値を入手

            if(UpdateAimStyleMemory(AtkAimStyle, TightenMind))//まず短期記憶を更新または新生する処理
            {
                if(atker.NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
                {
                    if(RandomEx.Shared.NextFloat(1)<0.3f)return;
                }
                NowDeffenceStyle = AtkAimStyle;
            }//カウントアップ完了したなら、nowDeffenceStyleに記録されたAimStyleを適用するだけ
            
        }



    }
    /// <summary>
    /// 連続攻撃時、狙い流れの物理属性適正とスキルの物理属性の一致による1.1倍ブーストがあるかどうかを判定し行使する関数です
    /// </summary>
    void CheckPhysicsConsecutiveAimBoost(BaseStates attacker)
    {
        var skill = attacker.NowUseSkill;
        if(!skill.NowConsecutiveATKFromTheSecondTimeOnward())return;//連続攻撃でないなら何もしない

        if((skill.NowAimStyle() ==AimStyle.Doublet && skill.SkillPhysical == PhysicalProperty.volten) ||
            ( skill.NowAimStyle() ==AimStyle.PotanuVolf && skill.SkillPhysical == PhysicalProperty.volten) ||
            (skill.NowAimStyle() ==AimStyle.Duster) && skill.SkillPhysical == PhysicalProperty.dishSmack)
            {
                attacker.SetSpecialModifier("連続攻撃時、狙い流れの物理属性適正とスキルの物理属性の一致による1.1倍ブースト",
                whatModify.atk, 1.1f);
            }
    }

    /// <summary>
    /// 連続攻撃中の割り込みカウンターが可能かどうかを判定する
    /// </summary>
    private bool TryInterruptCounter(BaseStates attacker)//attacker = 割り込みカウンターの被害者ね
    {
        if(!IsInterruptCounterActive)return false;//割り込みカウンターActiveがfalseなら発動しない

        var skill = attacker.NowUseSkill;
        if(NowPower >= ThePower.medium)//普通のパワー以上で
        {//割り込みカウンターは
            var eneVond = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.Vond);
            var myVond =  TenDayValues(false).GetValueOrZero(TenDayAbility.Vond);
            var plusAtkChance = myVond> eneVond ? myVond - eneVond : 0f;//ヴォンドの差による微加算値
            if(RandomEx.Shared.NextFloat(1) < skill.DEFATK/3 + plusAtkChance*0.01f)
            {
                var mypersonDiver = TenDayValues(false).GetValueOrZero(TenDayAbility.PersonaDivergence);
                var myTentvoid = TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid);
                var eneSort = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.Sort);
                var eneRain = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.Rain);
                var eneCold = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm);
                var ExVoid = PlayersStates.Instance.ExplosionVoid;
                var counterValue = (myVond + mypersonDiver/(myTentvoid-ExVoid)) * 0.9f;//カウンターする側の特定能力値
                var attackerValue = Mathf.Max(eneSort - eneRain/3,0)+eneCold;//攻撃者の特定能力値


                if(RandomEx.Shared.NextFloat(counterValue+attackerValue) < counterValue &&
                 RandomEx.Shared.NextFloat(1)<0.5f)
                {
                    //まず連続攻撃の無効化
                    attacker.DeleteConsecutiveATK();
                    attacker.IsActiveCancelInSkillACT = true;//スキルの行動を無効化された。
                    
                    //無効化のみ、次のターンで攻撃可能、それに加えて割り込みカウンターのパッシブが加わる。
                    //その三パターンで分かれる。　　最後のパッシブ条件のみ直接割り込みカウンターPassiveの方で設定している。

                    //割り込みカウンターのパッシブ付与しますが、適合するかどうかはそのpassiveの条件次第です。
                    var counterID = 1;
                    ApplyPassiveBufferInBattleByID(counterID);
                    var CounterPower = GetBufferPassiveByID(counterID);
                    if (CanApplyPassive(CounterPower))//適合したら
                    {
                        var attackerCounterPower = attacker.GetPassiveByID(counterID);
                        if(attackerCounterPower != null) //もし攻撃者が割り込みカウンターパッシブなら、
                        {
                            //攻撃者の割り込みカウンターパッシブのパワー+1で生成
                            CounterPower.SetPassivePower(attackerCounterPower.PassivePower +1);
                        }
                    }

                    //次のターンで攻撃、つまり先約リストの予約を判定する。　
                    if(HasCharacterType(CharacterType.Life))
                    {//生命なら、必ず反撃可能
                        
                        //割り込みカウンターの反撃は割り込んだ際の、敵の攻撃の防御無視率の方が、反撃スキルの防御無視率より多ければ、
                        // 食らいそうになった敵スキルの防御無視率をそのまま利用する。
                        var CounterDEFATK = skill.DEFATK;
                        

                        //攻撃を食らった際、中断不可能なカウンターまたはfreezeConecutiveの場合、武器スキルでしか返せない。
                        var isfreeze = false;
                        if(NowUseSkill.NowConsecutiveATKFromTheSecondTimeOnward() && NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive) ||
                        NowUseSkill.IsTriggering) 
                        {
                            NowUseSkill = NowUseWeapon.WeaponSkill;
                            isfreeze = true;
                        }
                        manager.Acts.Add(this,manager.GetCharacterFaction(this),"割り込みカウンター",null,isfreeze,null,CounterDEFATK);//通常の行動予約 
                    }

                    //無効化は誰でも可能です　以下のtrueを返して、呼び出し側で今回の攻撃の無効化は行います。
                    return true;
                }
            }
        }
        return false;
    }
    /// <summary>
    /// 悪いパッシブ付与処理
    /// </summary>
    bool BadPassiveHit(BaseSkill skill,BaseStates grantor)
    {
        var hit = false;
        foreach (var id in skill.SubEffects.Where(id => PassiveManager.Instance.GetAtID(id).IsBad))
        {//付与する瞬間はインスタンス作成のディープコピーがまだないので、passivemanagerで調査する
            ApplyPassiveBufferInBattleByID(id,grantor);
            hit = true;//goodpassiveHitに説明
        }
        return hit;
    }
    /// <summary>
    /// 悪いパッシブ解除処理
    /// </summary>
    bool BadPassiveRemove(BaseSkill skill)
    {
        var done = false;

        // skill.canEraceEffectIDs のIDを順にチェックして、
        // _passiveList の中で同じIDを持つパッシブを検索
        // そのパッシブが IsBad == true なら Remove する
        var rndList = new List<int>(skill.canEraceEffectIDs);
        rndList.Shuffle();
        var decrement = 0;
        for(var i = 0; i < skill.Now_CanEraceEffectCount; i++)
        {
            // ID が一致するパッシブを検索 (存在しない場合は null)
            var found = _passiveList.FirstOrDefault(p => p.ID == rndList[i]);//解除するときはちゃんとインスタンスがあるので、passiveList内のインスタンスを調査する
            if (found != null && found.IsBad)
            {
                RemovePassiveByID(rndList[i]);
                done = true;
                decrement++;
            }
        }
        skill.Now_CanEraceEffectCount -= decrement;//使った分現在の消せる数を減らす
        return done;
    }
    /// <summary>
    /// 悪い追加HP付与処理
    /// </summary>
    bool BadVitalLayerHit(BaseSkill skill)
    {
        var done = false;
        foreach (var id in skill.subVitalLayers.Where(id => VitalLayerManager.Instance.GetAtID(id).IsBad))
        {
            ApplyVitalLayer(id);
            done = true;
        }
        return done;
    }
    /// <summary>
    /// 悪い追加HP解除処理
    /// </summary>
    bool BadVitalLayerRemove(BaseSkill skill)
    {
        var done = false;
        var rndList = new List<int>(skill.canEraceVitalLayerIDs);
        rndList.Shuffle();
        var decrement = 0;
        for(var i=0; i<skill.Now_CanEraceVitalLayerCount; i++)
        {
            var found = _vitalLayerList.FirstOrDefault(v => v.id == rndList[i]);
            if (found != null && found.IsBad)
            {
                RemoveVitalLayerByID(rndList[i]);
                done = true;
                decrement++;
            }
        }
        skill.Now_CanEraceVitalLayerCount -= decrement;//使った分現在の消せる数を減らす
        return done;
    }
    /// <summary>
    /// 良いパッシブ付与処理
    /// </summary>
    bool GoodPassiveHit(BaseSkill skill,BaseStates grantor)
    {
        var hit = false;
        foreach (var id in skill.SubEffects.Where(id => !PassiveManager.Instance.GetAtID(id).IsBad))
        {
            ApplyPassiveBufferInBattleByID(id,grantor);
            hit = true;//スキル命中率を介してるのだから、適合したかどうかはヒットしたかしないかに関係ない。 = ApplyPassiveで元々適合したかどうかをhitに代入してた
            //。。。バッファーリストの関係で一々シミュレイト用関数作るのがめんどくさかったけど、この考えが割と合理的だったからそうしたけどね。
            //それに、このhitしたのに敵になかったら、プレイヤーはこのパッシブの適合条件で適合しなかったことを察せれるし。

        }
        return hit;
    }
    /// <summary>
    /// 良いパッシブ解除処理
    /// </summary>
    bool GoodPassiveRemove(BaseSkill skill)
    {
        var done = false;
        var rndList = new List<int>(skill.canEraceEffectIDs);
        rndList.Shuffle();
        var decrement = 0;
        for(var i=0; i<skill.Now_CanEraceEffectCount; i++)
        {
            var found = _passiveList.FirstOrDefault(p => p.ID == rndList[i]);
            if(found != null && !found.IsBad)
            {
                RemovePassiveByID(rndList[i]);
                done = true;
                decrement++;
            }
        }
        skill.Now_CanEraceEffectCount -= decrement;//使った分現在の消せる数を減らす
        return done;
    }
    /// <summary>
    /// 良いスキルパッシブ付与処理
    /// </summary>
    async UniTask<bool> GoodSkillPassiveHit(BaseSkill skill)
    {
        var hit = false;
        foreach (var pas in skill.AggressiveSkillPassiveList.Where(pas => !pas.IsBad))//スキルパッシブスキルに装弾されたスキルパッシブ
        {
            //スキルパッシブ付与対象のスキル
            var targetedSkill =  await skill.SelectSkillPassiveAddTarget(this);
            if(targetedSkill == null)continue;//付与対象のスキルがなかったら、次のループへ
            //付与対象のスキルで回す
            foreach(var targetSkill in targetedSkill)
            {
                targetSkill.ApplySkillPassiveBufferInBattle(pas);//スキルパッシブ追加用バッファに追加
            }

            //ヒットしたことを返すフラグ
            hit = true;//スキル命中率を介してるのだから。。。。[適合条件はスキルパッシブにはない]
        }
        return hit;
    }
    /// <summary>
    /// 悪いスキルパッシブ付与処理
    /// </summary>
    async UniTask<bool> BadSkillPassiveHit(BaseSkill skill)
    {
        var hit = false;
        foreach (var pas in skill.AggressiveSkillPassiveList.Where(pas => pas.IsBad))//スキルパッシブスキルに装弾されたスキルパッシブ
        {
            //スキルパッシブ付与対象のスキル
            var targetedSkill = await skill.SelectSkillPassiveAddTarget(this);
            if(targetedSkill == null)continue;//付与対象のスキルがなかったら、次のループへ
            //付与対象のスキルで回す
            foreach(var targetSkill in targetedSkill)
            {
                targetSkill.ApplySkillPassiveBufferInBattle(pas);//追加用バッファに追加
            }

            //ヒットしたことを返すフラグ
            hit = true;//スキル命中率を介してるのだから。。。。[適合条件はスキルパッシブにはない]
        }
        return hit;
    }
    /// <summary>
    /// 良いスキルパッシブ解除処理
    /// 各スキルパッシブのisbadで場合分けせずに、スキルそのもので場合分けするため、
    /// スキル攻撃性質でgoodかbad指定での友好的、敵対的の命中計算の場合分けをしているため、
    /// ここでgoodかbadを区別して処理する必要はないのでパッシブ除去と違いgood,badの冠詞は付かない
    /// </summary>
    bool SkillPassiveRemove(BaseSkill skill)
    {
        var done = false;
        //個別指定の反応式
        foreach(var hold in skill.ReactionCharaAndSkillList)//反応するキャラとスキルのリスト
        {
            //そもそもキャラ名が違っていたら、飛ばす
            if(CharacterName != hold.CharaName) continue;

            foreach(var targetSkill in SkillList)//対象者である自分の有効スキルすべてで回す。
            {
                if(targetSkill.SkillName == hold.SkillName)//スキル名まで一致したら
                {
                    targetSkill.SkillRemoveSkillPassive();//スキル効果ですべて抹消
                    done = true;
                    break;//スキルが一致して、他のスキルネームで検証する必要がなくなったので、次の対象スキルへ
                }
            }
        }

        return done;
    }
    /// <summary>
    /// 良い追加HP付与処理
    /// </summary>
    /// <param name="skill"></param>
    bool GoodVitalLayerHit(BaseSkill skill)
    {
        var done = false;
        foreach (var id in skill.subVitalLayers.Where(id => !VitalLayerManager.Instance.GetAtID(id).IsBad))
        {
            ApplyVitalLayer(id);
            done = true;
        }
        return done;
    }
    /// <summary>
    /// 良い追加HP解除処理
    /// </summary>
    /// <param name="skill"></param>
    bool GoodVitalLayerRemove(BaseSkill skill)
    {
        var done = false;
        var rndList = new List<int>(skill.canEraceVitalLayerIDs);
        rndList.Shuffle();
        var decrement = 0;
        for(var i=0; i<skill.Now_CanEraceVitalLayerCount; i++)
        {
            var found = _vitalLayerList.FirstOrDefault(v => v.id == rndList[i]);
            if (found != null && !found.IsBad)
            {
                RemoveVitalLayerByID(rndList[i]);
                done = true;
                decrement++;
            }
        }
        skill.Now_CanEraceVitalLayerCount -= decrement;//使った分現在の消せる数を減らす
        return done;
    }
    /// <summary>
    /// 直接攻撃じゃない敵対行動系
    /// </summary>
    async UniTask<HostileEffectResult>
    ApplyNonDamageHostileEffects(BaseStates Atker,BaseSkill skill,HitResult hitResult)
    {
        var badPassiveHit = false;
        var badVitalLayerHit = false;
        var goodPassiveRemove = false;
        var goodVitalLayerRemove = false;
        var badSkillPassiveHit = false;
        var goodSkillPassiveRemove = false;

        var rndFrequency = 90;//ランダム発動率　基本的に100%発動する　　　　　　　　　　　　　　　　　　　　　　　　　　　微妙に攻撃タイプ以外の敵対行動を発動しにくくして、攻撃することの優位性を高める　ドキュメント記述なし
        if(hitResult == HitResult.Graze)
        {
            rndFrequency = 50;//かすりHitなので、二分の一で発動
        }        

        if (skill.HasType(SkillType.addPassive))
        {
            if (rollper(rndFrequency))
            {
                //悪いパッシブを付与しようとしてるのなら、命中回避計算
                badPassiveHit = BadPassiveHit(skill,Atker);
            }else
            {
                Debug.Log("悪いパッシブを付与が上手く発動しなかった。");
            }
        }

        if (skill.HasType(SkillType.AddVitalLayer))
        {
            if (rollper(rndFrequency))
            {
                //悪い追加HPを付与しようとしてるのなら、命中回避計算
                badVitalLayerHit = BadVitalLayerHit(skill);
            }else
            {
                Debug.Log("悪い追加HPを付与が上手く発動しなかった。");
            }
        }
        if(skill.HasType(SkillType.RemovePassive))
        {
            if (rollper(rndFrequency))
            {
                //良いパッシブを取り除こうとしてるのなら、命中回避計算
                goodPassiveRemove = GoodPassiveRemove(skill);
            }else
            {
                Debug.Log("良いパッシブを取り除くのが上手く発動しなかった。");
            }
        }
        if (skill.HasType(SkillType.RemoveVitalLayer))
        {
            if (rollper(rndFrequency))
            {
                //良い追加HPを取り除こうとしてるのなら、命中回避計算
                goodVitalLayerRemove = GoodVitalLayerRemove(skill);
            }else
            {
                Debug.Log("良い追加HPを取り除くのが上手く発動しなかった。");
            }
        }
        if(skill.HasType(SkillType.removeGoodSkillPassive))
        {
            if (rollper(rndFrequency))
            {
                //良いスキルパッシブを取り除こうとしてるのなら、命中回避計算
                goodSkillPassiveRemove = SkillPassiveRemove(skill);
            }else
            {
                Debug.Log("良い「スキル」パッシブを取り除くのが上手く発動しなかった。");
            }
        }
        if(skill.HasType(SkillType.addSkillPassive))
        {
            if (rollper(rndFrequency))
            {
                //悪いスキルパッシブを付与しようとしてるのなら、命中回避計算
                badSkillPassiveHit = await BadSkillPassiveHit(skill);
            }else
            {
                Debug.Log("悪い「スキル」パッシブを付与が上手く発動しなかった。");
            }
        }

        return new HostileEffectResult(badPassiveHit,
            badVitalLayerHit,
            goodPassiveRemove,
            goodVitalLayerRemove,
            badSkillPassiveHit,
            goodSkillPassiveRemove);
    }//async化すると非同期処理ではoutで引数渡す形にするとfalseで返ってだめらしいから戻り値構造体で返す

    /// <summary>
    /// 精神補正用にスキルの属性を精神属性用に特殊分岐した後に渡す関数
    /// </summary>
    SpiritualProperty GetCastImpressionToModifier(SpiritualProperty skillSpiritual,BaseStates attacker)
    {
        SpiritualProperty imp;
        switch (skillSpiritual)
        {
            case SpiritualProperty.mvoid:
            case SpiritualProperty.air:
                //精神補正無し
                imp = SpiritualProperty.none;//noneなら補正無しなので
                break;
            case SpiritualProperty.Galvanize:
                imp = attacker.MyImpression;
                break;
            case SpiritualProperty.memento:
                imp = attacker.DefaultImpression;
                break;
            default://基本的に精神属性をそのまま補正に渡す
                imp = skillSpiritual;
                break;
        }
        Debug.Log($"スキルの属性解釈 : {skillSpiritual},キャラ:{attacker.CharacterName}");
        return imp;
    }
    /// <summary>
    /// スキル攻撃とその被害者の場合の精神属性の補正を取り出す。
    /// 被害者側から呼び出す。 引数に攻撃スキルと攻撃者を
    /// </summary>
    FixedOrRandomValue GetSkillVsCharaSpiritualModifier(SpiritualProperty skillImp,BaseStates attacker)
    {
        var castSkillImp = GetCastImpressionToModifier(skillImp,attacker);//補正に投げる特殊スキル属性を純正な精神属性に変換

        if(castSkillImp == SpiritualProperty.none) return new FixedOrRandomValue(100);//noneなら補正なし(100%なので無変動)

        var key = (castSkillImp, MyImpression);
        if (!SpiritualModifier.ContainsKey(key))
        {
            Debug.LogWarning($"SpiritualModifier辞書にキー {key} が存在しません。攻撃スキルcastSkillImp={castSkillImp}({(int)castSkillImp}), スキルを受ける側MyImpression={MyImpression}。"
            +"デフォルト値100%を返します。\n(ReactionSkillの攻撃スキルと被害者の精神補正計算中)");
            return new FixedOrRandomValue(100);
        }

        var resultModifier = SpiritualModifier[key];//スキルの精神属性と自分の精神属性による補正
        
        if(attacker.DefaultImpression == skillImp)
        {
            resultModifier.RandomMaxPlus(12);//一致してたら12%程乱数上昇
        }
        Debug.Log($"スキルの精神属性補正 : {resultModifier}({skillImp}, {CharacterName})");
        return resultModifier;
    }
    /// <summary>
    /// 直接攻撃スキルのを「食らう側のヒット判定のラッパー
    /// 命中回避を用いるかどうかなどをここで計算する。
    /// </summary>
    /// <returns></returns>
    HitResult ATKTypeSkillReactHitCalc(BaseStates attacker,BaseSkill atkSkill)
    {
        HitResult hitResult = HitResult.CompleteEvade;//念のため初期値を
        var HitResultSet = false;

        //善意攻撃であるのなら、スキル命中率のみ

        //攻撃者と被害者(自分)が味方同士で、かつ、
        if(manager.IsFriend(attacker, this))
        {
            //自分の持ってるパッシブに一つでも「行動不能」と「現存してる追加HPが生存条件である」プロパティの二つが同時に含まれていれば
            //または、「行動不能」と「RemoveOnDamage」の二つが同時に含まれていれば
            foreach (var pas in _passiveList)//自分の持ってるパッシブで回す
            {
                if(pas.IsCantACT)//行動不能のパッシブなら
                {
                    if(pas.RemoveOnDamage)//RemoveOnDamageが有効なら
                    {
                        hitResult = atkSkill.SkillHitCalc(this);//一個でも条件を満たせば善意攻撃なのでループを抜けていい
                        HitResultSet = true;
                        break;
                    }
                    if(pas.VitalLayers == null) continue;//パッシブに追加HPが無ければ飛ばす

                    if(pas.HasRemainingSurvivalVitalLayer(this))//生存条件としてのVitalLayerを今持っているかどうか
                    {
                        hitResult = atkSkill.SkillHitCalc(this);//一個でも条件を満たせば善意攻撃なのでループを抜けていい
                        HitResultSet = true;
                        break;
                    }
                }
            }
        }
        if(!HitResultSet)
        {
            schizoLog.AddLog($"攻撃スキルヒット判定でisreactHitが呼ばれた。",true);
            hitResult = IsReactHIT(attacker);//善意ヒット判定が未代入なら通常のヒット判定
        }

        Debug.Log($"攻撃スキルヒット判定結果 : {hitResult}({attacker.CharacterName}の{atkSkill.SkillName}の{CharacterName}に対する判定)");
        return hitResult;
    }
    /// <summary>
    /// AllyEvade 計算  ➜  既存 HitResult と合成して返すショートハンド
    /// 味方別口回避
    /// </summary>
    HitResult MixAllyEvade(HitResult existingHit, BaseStates attacker)
    {
        var allyEvade = AllyEvadeCalculation(attacker);
        var hitresult = AllyEvade_HitMixDown(existingHit, allyEvade);
        Debug.Log($"味方別口回避結果 : {existingHit} → {hitresult}");
        return hitresult;
    }
    /// <summary>
    /// 命中結果の合算
    /// 主に味方別口回避と既存の計算結果を混ぜるため
    /// MIXテーブルの通りにAIに実装してもらたｗ
    /// </summary>
    HitResult AllyEvade_HitMixDown(HitResult existingHit, HitResult allyEvadeHit)
    {
        if(allyEvadeHit == HitResult.none)
        {//味方別口回避がなければ、既存のをそのまま返す
            return existingHit;
        }

        // 完全回避 + 完全回避 または かすり + 完全回避
        if ((existingHit == HitResult.CompleteEvade && allyEvadeHit == HitResult.CompleteEvade) ||
            (existingHit == HitResult.CompleteEvade && allyEvadeHit == HitResult.Graze) ||
            (existingHit == HitResult.Graze && allyEvadeHit == HitResult.CompleteEvade))
        {
            return HitResult.CompleteEvade;
        }
        
        // HIT + 完全回避 = かすり
        if ((existingHit == HitResult.Hit && allyEvadeHit == HitResult.CompleteEvade) ||
            (existingHit == HitResult.CompleteEvade && allyEvadeHit == HitResult.Hit))
        {
            return HitResult.Graze;
        }
        
        // クリティカル + 完全回避 = HITまたは1/2の確率でかすり
        if ((existingHit == HitResult.Critical && allyEvadeHit == HitResult.CompleteEvade) ||
            (existingHit == HitResult.CompleteEvade && allyEvadeHit == HitResult.Critical))
        {
            // 50%の確率でかすり、それ以外はHIT
            return rollper(50) ? HitResult.Graze : HitResult.Hit;
        }
        
        // かすり + HIT = かすり
        if ((existingHit == HitResult.Graze && allyEvadeHit == HitResult.Hit) ||
            (existingHit == HitResult.Hit && allyEvadeHit == HitResult.Graze))
        {
            return HitResult.Graze;
        }
        
        // クリティカル + HIT = HIT
        if ((existingHit == HitResult.Critical && allyEvadeHit == HitResult.Hit) ||
            (existingHit == HitResult.Hit && allyEvadeHit == HitResult.Critical))
        {
            return HitResult.Hit;
        }
        
        // クリティカル + かすり = HIT
        if ((existingHit == HitResult.Critical && allyEvadeHit == HitResult.Graze) ||
            (existingHit == HitResult.Graze && allyEvadeHit == HitResult.Critical))
        {
            return HitResult.Hit;
        }
        
        // それ以外の場合は、同じ値ならそのまま返す
        if (existingHit == allyEvadeHit)
        {
            return existingHit;
        }
        
        // 上記以外のケースは、ここには来ないはずだが、安全のため
        // 値が小さい方（より回避側）を選択
        return ((int)existingHit < (int)allyEvadeHit) ? existingHit : allyEvadeHit;


    }

    /// <summary>
    /// パーティー属性という雰囲気における味方同士の攻撃の別口回避
    /// 既存の計算とは独立している
    /// </summary>
    HitResult AllyEvadeCalculation(BaseStates attacker)
    {
        if(manager.IsFriend(attacker, this))//まず味方同士かどうかを判断
        {
            float evasionRate;//回避弱体補正
            var mygroup = manager.MyGroup(this);
            //パーティー属性により発生判定と、回避倍率を決める
            switch(mygroup.OurImpression)
            {
                case PartyProperty.Odradeks:
                evasionRate = 0.25f;
                break;
                case PartyProperty.TrashGroup:
                evasionRate = 0.5f;
                break;
                default:
                return HitResult.none;//発生しなかった。
            }

            //相性値による回避率のすげ替え
            var agi = AGI().Total;
            if(mygroup.CharaCompatibility[(attacker,this)] >= 88)//攻撃者から自分への味方同士の相性値が特定以上なら
            {
                //また、攻撃者のAGIが自分より大きければ、計算に使うAGIをすげ替える
                agi = Mathf.Max(agi,attacker.AGI().Total);
            }

            if (RandomEx.Shared.NextFloat(attacker.EYE().Total + agi * evasionRate) < attacker.EYE().Total)
            {
                //三分の一でかすり
                if(rollper(33))return HitResult.Graze;

                return HitResult.CompleteEvade;
            }
        
        }
        return HitResult.none;//発生せず
    }
    /// <summary>
    /// 現在のNowUseSkillにパッシブ由来の追加パッシブを`適用する
    /// </summary>
    void ApplyExtraPassivesToSkill(BaseStates ene)
    {
        var AllyOrEnemy = allyOrEnemy.Enemyiy;//基本は敵攻撃
        if(manager.IsFriend(ene, this))//もし味方なら
        {
            AllyOrEnemy = allyOrEnemy.alliy;
        };
        // 追加付与パッシブIDの取得（null 安全）
        var baseExtra = ExtraPassivesIdOnSkillACT(AllyOrEnemy);
        var extraList = baseExtra != null ? new List<int>(baseExtra) : new List<int>();

        if (extraList.Count > 0)
        {
            // パッシブ付与バッファーリストにリストを渡す（1件以上ある場合のみ）
            NowUseSkill.SetBufferSubEffects(extraList);

            // もし実行スキルが付与スキル性質を持っていなかったら、一時的に付与
            if (!NowUseSkill.HasType(SkillType.addPassive))
            {
                NowUseSkill.SetBufferSkillType(SkillType.addPassive);
            }
        }
        else
        {
            if (NowUseSkill == null)
            {
                Debug.LogError("NowUseSkill is null");
                return;
            }
            // 念のためクリア（前回ターゲットで付与されていた可能性に備える）
            NowUseSkill.EraseBufferSubEffects();
            // SkillType バッファはこの時点では付与していないため基本不要だが、保全で未使用時は触らない
        }
    }
    /// <summary>
    /// 一人に対するスキル実行が終わった時のコールバック
    /// </summary>
    void OnAttackerOneSkillActEnd()
    {
        //バッファをクリア
        NowUseSkill.EraseBufferSkillType();//攻撃性質のバッファ
        NowUseSkill.EraseBufferSubEffects();//スキルの追加パッシブ付与リスト
        
    }
    /// <summary>
    /// 一人に対するスキル実行が始まった時のコールバック
    /// </summary>
    void OnAttackerOneSkillActStart(BaseStates UnderAtker)
    {
        ApplyExtraPassivesToSkill(UnderAtker);//攻撃者にスキルの追加パッシブ性質を適用
        NowUseSkill.CalcCradleSkillLevel(UnderAtker);//「攻撃者の」スキルのゆりかご計算
        NowUseSkill.RefilCanEraceCount();//除去スキル用の消せるカウント回数の補充
    }
    /// <summary>
    /// スキルに対するリアクション ここでスキルの解釈をする。
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="UnderIndex">攻撃される人の順番　スキルのPowerSpreadの順番に同期している</param>
    public virtual async UniTask<string> ReactionSkill(BaseStates attacker, float spread)
    {
        // ポイント精算用: 最も強いヒット結果を保持
        HitResult bestHitOutcome = HitResult.none;
        int Rank(HitResult hr)
        {
            switch (hr)
            {
                case HitResult.Critical: return 4;
                case HitResult.Hit: return 3;
                case HitResult.Graze: return 2;
                case HitResult.CompleteEvade: return 1;
                default: return 0;
            }
        }
        void AccumulateHitResult(HitResult hr)
        {
            if (Rank(hr) > Rank(bestHitOutcome)) bestHitOutcome = hr;
        }
        
        schizoLog.AddLog($"-ReactionSkill",true);
        Debug.Log($"{attacker.CharacterName}の{attacker.NowUseSkill.SkillName}に対する{CharacterName}のReactionSkillが始まった");
        Debug.Log($"スキルを受ける{CharacterName}の精神属性 = {MyImpression}:(ReactionSkill)");
        attacker.OnAttackerOneSkillActStart(this);//攻撃者の一人へのスキル実行開始時のコールバック

        var skill = attacker.NowUseSkill;

        //スキルパワーの精神属性による計算
        var modifier = GetSkillVsCharaSpiritualModifier(skill.SkillSpiritual, attacker);
        var SpiritualModifierPercentage = 0.2f;//IsTLOAスキル以外は20%の精神補正
        if(skill.IsTLOA) SpiritualModifierPercentage = 0.4f;//IsTLOAなら40%の精神補正

        var modifierForSkillPower = modifier.GetValue(SpiritualModifierPercentage);//精神補正値
        if(attacker.NowUseWeapon.IsBlade) modifierForSkillPower = 1.0f;//刃物武器なら精神補正なし
        Debug.Log($"{attacker.CharacterName}の{skill.SkillName}の精神補正率 = {SpiritualModifierPercentage}と精神補正値 = {modifier.GetValue(SpiritualModifierPercentage)}\n-精神補正値の準備終了(ReactionSkill)");
        
        //TLOAスキルならゆりかごされたスキルレベルを参照する(IsTLOAを渡して判断)
        var skillPower = skill.SkillPowerCalc(skill.IsTLOA) * modifierForSkillPower * spread;
        var skillPowerForMental = skill.SkillPowerForMentalCalc(skill.IsTLOA) * modifier.GetValue() * spread;//精神HPへのパワーは精神補正100%

        Debug.Log($"{attacker.CharacterName}の{skill.SkillName}のスキルパワー = {skillPower} ,精神用スキルパワー = {skillPowerForMental}\n-スキルパワーの準備終了(ReactionSkill)");

        //メッセージテキスト用
        var txt = "";

        //発動するかどうか
        var thisAtkTurn = true;
        //攻撃が乱れたかどうか
        var isdisturbed = false;

        //被害記録用の一時保存boolなど
        var BadPassiveHit = false;
        var BadPassiveRemove = false;
        var GoodPassiveRemove = false;
        var GoodPassiveHit = false;
        var BadVitalLayerHit = false;
        var BadVitalLayerRemove = false;
        var GoodVitalLayerHit = false;
        var GoodVitalLayerRemove = false;
        var BadSkillPassiveHit = false;
        var GoodSkillPassiveHit = false;
        var BadSkillPassiveRemove = false;
        var GoodSkillPassiveRemove = false;
        var isHeal = false;
        var isAtkHit = false;
        var healAmount = 0f;
        var damageAmount = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);

        

        //スキルの持ってる性質を全て処理として実行
        Debug.Log($"{attacker.CharacterName}の{skill.SkillName}のスキル性質 = {skill.SkillType}(ReactionSkill)");

        //Manual1
        if(skill.HasType(SkillType.Manual1_GoodHitCalc))//良い攻撃
        {
            var hitResult = skill.SkillHitCalc(this);//良い攻撃なのでスキル命中のみ
            hitResult = MixAllyEvade(hitResult,attacker);//味方別口回避の発生と回避判定
            AccumulateHitResult(hitResult);

            skill.ManualSkillEffect(this,hitResult);//効果
        }
        if(skill.HasType(SkillType.Manual1_BadHitCalc))//悪い攻撃
        {
            var hitResult = IsReactHIT(attacker);//攻撃タイプでないので直接IsReactHitね
            hitResult = MixAllyEvade(hitResult,attacker);//味方別口回避の発生と回避判定
            AccumulateHitResult(hitResult);

            skill.ManualSkillEffect(this,hitResult);//効果
        }

        if (skill.HasType(SkillType.Attack))
        {
            Debug.Log($"{attacker.CharacterName}の{skill.SkillName}は攻撃タイプスキルで{CharacterName}はそれに対する個別反応を開始(ReactionSkill)");
            var hitResult = ATKTypeSkillReactHitCalc(attacker, skill);
            //味方別口回避の発生と回避判定
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult != HitResult.CompleteEvade)//完全回避以外なら = HITしてるなら
            {
                Debug.Log($"HITした{attacker.CharacterName}の{CharacterName}に対して{skill.SkillName}がかすり以上");
                //割り込みカウンターの判定
                if (skill.NowConsecutiveATKFromTheSecondTimeOnward())//連続攻撃されてる途中なら
                {
                    if(!skill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))//ターンをまたいだ物じゃないなら
                    {
                        Debug.Log($"割り込みカウンターの判定{attacker.CharacterName}の{skill.SkillName}に対するもので");
                        thisAtkTurn = !TryInterruptCounter(attacker);//割り込みカウンターの判定
                        if(!thisAtkTurn)
                        {
                            PassivesOnInterruptCounter();//割り込みカウンター発生時の効果
                        }
                    }
                }

                //攻撃される側からのパッシブ由来のスキル発動の可否を判定
                thisAtkTurn = PassivesOnBeforeDamageActivate(attacker);

                if(thisAtkTurn)
                {
                    Debug.Log($"{attacker.CharacterName}の{skill.SkillName}が{CharacterName}を攻撃した(発動成功)");
                    //防ぎ方の切り替え
                    SwitchDefenceStyle(attacker);
                    //連続攻撃の物理属性ブースト判定
                    CheckPhysicsConsecutiveAimBoost(attacker);
                    
                    //成功されるとダメージを受ける
                    damageAmount = Damage(attacker, skillPower,skillPowerForMental,hitResult,ref isdisturbed);
                    isAtkHit = true;//攻撃をしたからtrue

                    var result = await ApplyNonDamageHostileEffects(attacker,skill,hitResult);
                    BadPassiveHit = result.BadPassiveHit;
                    BadVitalLayerHit = result.BadVitalLayerHit;
                    GoodPassiveRemove = result.GoodPassiveRemove;
                    GoodVitalLayerRemove = result.GoodVitalLayerRemove;
                    BadSkillPassiveHit = result.BadSkillPassiveHit;
                    GoodSkillPassiveRemove = result.GoodSkillPassiveRemove;
                }
                else
                {
                    Debug.Log($"命中はしたが発動しなかった。{attacker.CharacterName}の{CharacterName}に対して{skill.SkillName}が");
                }
            }else   
            {
                schizoLog.AddLog(attacker.CharacterName + "は外した");
            }
        }
        else//atktypeがないと各自で判定
        {
            //schizoLog.AddLog($"攻撃以外のスキルヒット判定の為にでisreactHitが呼ばれた。",true);
            var hitResult = IsReactHIT(attacker);
            //味方別口回避の発生と回避判定
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult != HitResult.CompleteEvade)
            {
                var result = await ApplyNonDamageHostileEffects(attacker,skill,hitResult);     
                BadPassiveHit = result.BadPassiveHit;
                BadVitalLayerHit = result.BadVitalLayerHit;
                GoodPassiveRemove = result.GoodPassiveRemove;
                GoodVitalLayerRemove = result.GoodVitalLayerRemove;
                BadSkillPassiveHit = result.BadSkillPassiveHit;
                GoodSkillPassiveRemove = result.GoodSkillPassiveRemove;   
            }
        }

        //回復系は常に独立
        if(skill.HasType(SkillType.DeathHeal))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                Angel();//降臨　アイコンがノイズで満たされるようなエフェクト
                isHeal = true;
                manager.MyGroup(this).PartyApplyConditionChangeOnCloseAllyAngel(this);//所属してるグループが自分の復活により相性値の高い味方の人間状況の変化
            }
        }

        if (skill.HasType(SkillType.Heal))
        {
            
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);//味方別口回避の発生と回避判定
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                healAmount= Heal(skillPower);
                isHeal = true;
            }
        }

        if (skill.HasType(SkillType.MentalHeal))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                MentalHeal(skillPower);
                isHeal = true;
            }
        }

        //付与や除去系の友好的スキル(敵対的な物はApplyNonDamageHostileEffectsで処理)
        //攻撃要素あるにかかわらず、回復系と同じで独立している。

        if (skill.HasType(SkillType.addPassive))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                //良いパッシブを付与しようとしてるのなら、スキル命中計算のみ
                GoodPassiveHit = this.GoodPassiveHit(skill, attacker);
            }
        }
        if (skill.HasType(SkillType.AddVitalLayer))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                //良い追加HPを付与しようとしてるのなら、スキル命中のみ
               this.GoodVitalLayerHit(skill);
                GoodVitalLayerHit = true;
            }
        }
        if (skill.HasType(SkillType.addSkillPassive))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                //良いパッシブを付与しようとしてるのなら、スキル命中計算のみ
                GoodSkillPassiveHit = await this.GoodSkillPassiveHit(skill);
            }
        }
        if (skill.HasType(SkillType.removeBadSkillPassive))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                //悪いパッシブを取り除くのなら、スキル命中のみ
                BadSkillPassiveRemove = SkillPassiveRemove(skill);
            }
        }


        if(skill.HasType(SkillType.RemovePassive))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                //悪いパッシブを取り除くのなら、スキル命中のみ
                BadPassiveRemove = this.BadPassiveRemove(skill);
            }
        }
        if (skill.HasType(SkillType.RemoveVitalLayer))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                //悪い追加HPを取り除こうとしてるのなら、スキル命中のみ
                BadVitalLayerRemove = this.BadVitalLayerRemove(skill);
            }
        }

        
        var arrowThicknessDamagePercent = 0.05f;//ダメージを表す矢印の太さ用　デフォは10%
        



        Debug.Log("ReactionSkillの反応部分終了、最後の処理の記録を開始");
        //攻撃者がヒットしたかどうかをタイプにより記録
        bool isAttackerHit;
        if (skill.HasType(SkillType.Attack))
        {
            isAttackerHit = thisAtkTurn;
        }else{
                isAttackerHit = BadPassiveHit || BadPassiveRemove || GoodPassiveHit || GoodPassiveRemove || 
                GoodVitalLayerHit || GoodVitalLayerRemove || BadVitalLayerHit || BadVitalLayerRemove ||
                BadSkillPassiveHit || GoodSkillPassiveHit || BadSkillPassiveRemove || GoodSkillPassiveRemove || isHeal;
        }
        //攻撃がいわゆるヒットをしたならば、
        if (isAttackerHit)
        {
            ImposedImpressionFromSkill(skill.SkillSpiritual,attacker);//特殊なスキル属性に影響されるか
            RivahalDream(attacker,skill);//ライバハルの上昇

            //攻撃者の成長処理 HIT分のスキルの印象構造の十日能力が上昇する。
            float growRate;
            if (skill.HasType(SkillType.Attack))
            {
                growRate = 0.7f;
            }
            else
            {
                growRate = 0.9f;
            }
            // 攻撃者とと攻撃相手の総量の比率を使用して比率を計算
            float clampedRatio = attacker.CalculateClampedStrengthRatio(TenDayValuesSum(false));

            //攻撃者のHIT分の成長を記録
            attacker.TenDayGrowthListByHIT.Add((growRate * clampedRatio, skill.TenDayValues(skill.IsTLOA)));//成長量にTLOAならゆりかごを考慮

            arrowThicknessDamagePercent = 0.2f;//ヒットしたら矢印の太さちょっと増やしとく

        }


        //ここで攻撃者の攻撃記録を記録する
        attacker.ActDoOneSkillDatas.Add(new ACTSkillDataForOneTarget(thisAtkTurn,isdisturbed,skill,this,isAttackerHit));//発動したのか、何のスキルなのかを記録
        attacker.OnAttackerOneSkillActEnd();//攻撃者の一人へのスキル実行終了時のコールバック
        //被害の記録
        damageDatas.Add(new DamageData//クソ長い
        (isAtkHit,BadPassiveHit,BadPassiveRemove,GoodPassiveHit,GoodPassiveRemove,GoodVitalLayerHit,GoodVitalLayerRemove,
        BadVitalLayerHit,BadVitalLayerRemove,GoodSkillPassiveHit,GoodSkillPassiveRemove,BadSkillPassiveHit,BadSkillPassiveRemove,
        isHeal,skill,damageAmount.Total,healAmount,attacker));

        if(isAtkHit)//このboolは「攻撃性質」のスキルを食らったかどうかの判定になる。
        {
            //グループ全員分の「味方と自分」がダメージを食らった際のコールバックを呼び出す
            manager.MyGroup(this).PartyPassivesOnAfterAlliesDamage(attacker);
            //パッシブのダメージ食らった後のコールバックを呼び出す
            PassivesOnAfterDamage(attacker,damageAmount);
            //自分のグループの全体現在HPに対してくらったダメージの割合が線の太さに反映される
            var totalHP = manager.MyGroup(this).OurNowHP;
            var groupeHP = totalHP > 0f ? damageAmount.Total / totalHP : 1f;//もしグループHPが0以下なら　1fで100%の太さを指定(死体蹴りだから)
            arrowThicknessDamagePercent = groupeHP;
        }

        // 集約: 攻撃者へ最強ヒット結果を渡してキャスト単位で後で精算
        attacker.AggregateSkillHit(bestHitOutcome);

        //今回の攻撃結果を矢印の描画キューに
        BattleSystemArrowManager.Instance.Enqueue(attacker,this,arrowThicknessDamagePercent);

        return txt;
    }
    /// <summary>
    ///スキルの精神属性が特殊な場合、自分の精神属性が変化をしてしまう。
    /// </summary>
    void ImposedImpressionFromSkill(SpiritualProperty skillImp,BaseStates attacker)
    {
        switch(skillImp)
        {
            case SpiritualProperty.mvoid:
                MyImpression = attacker.MyImpression;
                break;
            case SpiritualProperty.Galvanize:
                MyImpression = attacker.MyImpression;
                break;
            case SpiritualProperty.air:
                //noneでも変化なし
                break;
            case SpiritualProperty.memento:
                //被害者には変化なし
                break;
            default:
                if(MyImpression == SpiritualProperty.none)
                {
                    MyImpression = skillImp;
                }
                break;
        }
    }
    /// <summary>
    /// クラスを通じて相手を攻撃する
    /// </summary>
    public virtual async UniTask<string> AttackChara(UnderActersEntryList Unders)
    {
        TenDayGrowthListByHIT = new();//ヒット分成長リストを初期化する。

        //素振り分のスキルの印象構造の十日能力が上昇する。
        if(NowUseSkill.HasType(SkillType.Attack))
        {
            GrowTenDayAbilityBySkill(0.3f,NowUseSkill.TenDayValues());
        }else{
            GrowTenDayAbilityBySkill(0.1f,NowUseSkill.TenDayValues());
        }

        SkillUseConsecutiveCountUp(NowUseSkill);//連続カウントアップ
        string txt = "";

       

        //対象者ボーナスの適用
        //if(Unders.Count == 1)//結果として一人だけを選び、
        //{
            //範囲意志で判定。
            var randomRangeSpecialBool //RandomRangeなら範囲意志が書き変わるので、RandomRangeがあるならば、Skill.HasZoneTraitで単体スキルかどうかの判定をする
            = SkillCalculatedRandomRange && NowUseSkill.HasAnySingleTargetTrait();
            if(HasAnySingleRangeWillTrait()|| randomRangeSpecialBool)//単体スキルの範囲意志を持ってるのなら
            {
                int index = -1;//判定関数でのFindIndexは見つからなかった場合-1を返す
                if((index = TargetBonusDatas.DoIHaveTargetBonusAny_ReturnListIndex(Unders.GetCharacterList())) != -1)//対象者ボーナスを持っていれば = 選んだ敵が対象者ボーナスならば。
                {
                    //適用
                    SetSpecialModifier("対象者ボーナス", whatModify.atk,TargetBonusDatas.GetAtPowerBonusPercentage(index));

                    //適用した対象者ボーナスの削除　該当インデックスのclear関数の制作
                    TargetBonusDatas.BonusClear(index);
                }
            }
        //}

        //「全体攻撃時」、被害者側全員の、「自分陣営が全体攻撃食らった時の自分のパッシブコールバック」
        if(HasRangeWill(SkillZoneTrait.AllTarget))
        {
            //undersに含まれる陣営を全て抽出し、その陣営グループのコールバックを呼び出す
            var EneFaction = Unders.charas.Any(x=>manager.GetCharacterFaction(x) == allyOrEnemy.Enemyiy);
            var AllyFaction = Unders.charas.Any(x=>manager.GetCharacterFaction(x) == allyOrEnemy.alliy);

            //それぞれの陣営のコールバック
            if(EneFaction)
            manager.FactionToGroup(allyOrEnemy.Enemyiy).PartyPassivesOnBeforeAllAlliesDamage(this,ref Unders);
            if(AllyFaction)
            manager.FactionToGroup(allyOrEnemy.alliy).PartyPassivesOnBeforeAllAlliesDamage(this,ref Unders);
        }

        //キャラクターに対して実行
        BeginSkillHitAggregation();
        for (var i = 0; i < Unders.Count; i++)
        {
            var ene = Unders.GetAtCharacter(i);
            ApplyCharaConditionalToSpecial(ene);//キャラ限定補正を通常の特別補正リストに追加　キャラが合ってればね
            schizoLog.AddLog($"{ene.CharacterName}のReactionSkillが始まった-Undersのカウント数:{Unders.Count}",true);
            txt += await ene.ReactionSkill(this, Unders.GetAtSpreadPer(i));//敵がスキルにリアクション
        }
        // 対象処理が完了したのでキャスト単位でポイント精算
        var overallHit = EndSkillHitAggregation();
        SettlePointsAfterSkillOutcome(NowUseSkill, overallHit);

        NowUseSkill.ConsecutiveFixedATKCountUP();//使用したスキルの攻撃回数をカウントアップ
        NowUseSkill.DoSkillCountUp();//使用したスキルの使用回数をカウントアップ
        RemoveUseThings();//特別な補正を消去
        PassivesOnAfterAttack();//攻撃後のパッシブ効果
        Debug.Log("AttackChara- 攻撃した人数:" + Unders.Count);

        //今回の攻撃で一回でもヒットしていれば  「攻撃者側の攻撃の単位 = 範囲攻撃でも一回だけ = 攻撃者の為の処理」で実行されてほしい
        if (IsAnyHitInRecentSkillData(NowUseSkill, Unders.Count))
        {
            //当たったので精神回復　行動が一応成功したからメンタルが安心する。
            MentalHealOnAttack();
            CalmDownSet(NowUseSkill.EvasionModifier,NowUseSkill.AttackModifier);//スキル回避率と落ち着きカウントをセット
        }
        //HIT分の十日能力の成長
        foreach(var growData in TenDayGrowthListByHIT)
        {
            GrowTenDayAbilityBySkill(growData.Factor,growData.growTenDay);
        }

        _tempUseSkill = NowUseSkill;//使ったスキルを一時保存

        //スキルの精神属性に染まる
        PullImpressionFromSkill();
        //思えの値を回復する
        ResonanceHealingOnBattle();
        //アクション単位での行動記録
        var isdivergence = GetIsSkillDivergence();
        DidActionSkillDatas.Add(new ActionSkillData(isdivergence, NowUseSkill));
        return txt;
    }
    /// <summary>
    /// スキルを実行した結果として精神属性に染まる
    /// </summary>
    void PullImpressionFromSkill()
    {
        var NextImpression =  NowUseSkill.SkillSpiritual;
        

        switch(NextImpression)//スキルの精神属性で特殊な分岐かそうでないかで
        {
            case SpiritualProperty.mvoid:
                MyImpression = SpiritualProperty.none;
                break;
            case SpiritualProperty.Galvanize:
                //変化なし
                break;
            case SpiritualProperty.air:
                //変化なし
                break;
            case SpiritualProperty.memento:
                MyImpression = DefaultImpression;
                break;
            default:
                 MyImpression = NextImpression;//基本的に実行した精神属性にそまる
                break;
        }
       
    }
    /// <summary>
    /// 現在のスキルが乖離してるかどうかを返す
    /// </summary>
    public bool GetIsSkillDivergence()
    {
        if(DefaultImpression == SpiritualProperty.none) 
        {
            Debug.Log($"{CharacterName}の{NowUseSkill.SkillName}-"
            +"「DefaultImpressionがnoneなら乖離判定は行われません。」none精神属性互換の十日能力とかないからね");
            return false;
        }

        //判定するスキル印象構造の種類数を取得  1クランプする。
        var NeedJudgementSkillTenDayCount = Mathf.Max(1, (int)(NowUseSkill.TenDayValues().Count * 0.8 -1));

        //判定するスキル印象構造を取得して
        var SuggestionJudgementSkillTenDay =new TenDayAbilityDictionary(NowUseSkill.TenDayValues());
        //キーリストを取得
        var SuggestionJudgementSkillTenDayKeys = SuggestionJudgementSkillTenDay.Keys.ToList();
        Debug.Log($"(使用スキルの乖離判定)判定するスキル印象構造の種類数 : {SuggestionJudgementSkillTenDayKeys.Count}");    
        if(SuggestionJudgementSkillTenDayKeys.Count <= 0)
        {
            Debug.Log("(使用スキルの乖離判定)判定するスキル印象構造の種類数が0以下、つまりスキルに印象構造がセットされてないので、GetIsSkillDivergenceはfalseを返し終了します。");
            return false;
        }
        //キーリストをシャッフルする
        SuggestionJudgementSkillTenDayKeys.Shuffle();

        //判定する種類分判定リストに代入
        var JudgementSkillTenDays =new HashSet<TenDayAbility>();
        for(var i = 0; i < NeedJudgementSkillTenDayCount; i++)
        {
            Debug.Log($"{i} : {SuggestionJudgementSkillTenDayKeys[i]} スキルが乖離してるかどうかを判定するリストに代入");
            var key = SuggestionJudgementSkillTenDayKeys[i];
            JudgementSkillTenDays.Add(key);
        }

        //判定印象構造とデフォルト精神属性互換の精神属性全て同士の距離の平均を出す。
        var AllAverageBetweenSkillTenAndDefaultImpressionDistance = 0f;//判定印象構造とデフォルト精神属性互換の精神属性全て同士の距離の平均の印象構造分全て
        foreach(var skillTen in JudgementSkillTenDays)//スキルの判定印象構造で回す
        {
            var AllBetweenSkillTenAndDefaultImpressionDistance = 0f;
            foreach(var defaultImpTen in SpritualTenDayAbilitysMap[DefaultImpression])//デフォルト精神属性互換の精神属性全てで回す
            {
                //距離を足す
                AllBetweenSkillTenAndDefaultImpressionDistance += GetDistance(skillTen, defaultImpTen);
            }
            //デフォルト精神属性互換の十日能力の数で割って、平均を出す
            AllAverageBetweenSkillTenAndDefaultImpressionDistance//その平均距離を総距離として加算する
             += AllBetweenSkillTenAndDefaultImpressionDistance / SpritualTenDayAbilitysMap[DefaultImpression].Count;
        }
        //平均の平均を出す　判定印象構造とデフォルト精神属性互換の精神属性全て同士の距離の平均の平均
        var AvarageAllAverageBetweenSkillTenAndDefaultImpressionDistance 
        = AllAverageBetweenSkillTenAndDefaultImpressionDistance / JudgementSkillTenDays.Count;

        //この平均の平均が特定の定数より多い、　離れているのなら、乖離したスキルとみなす。
        return AvarageAllAverageBetweenSkillTenAndDefaultImpressionDistance >= 8;
    }

    /// <summary>
    /// 攻撃対象の十日能力総量と被害者の十日能力総量の比率を計算し返す
    /// 攻撃者から呼び出す
    /// </summary>
    private float CalculateClampedStrengthRatio(float targetSum)
    {
        // 自分のTenDayValuesSumとの比率を計算（自分の値が0の場合は1とする）
        float strengthRatio = TenDayValuesSum(true) > 0 ? targetSum / TenDayValuesSum(true) : 1f;
        
        return strengthRatio;
    }

    /// <summary>
    /// 指定したスキルが最近のスキルデータでヒットしたかどうかを調べる
    /// </summary>
    private bool IsAnyHitInRecentSkillData(BaseSkill skill, int targetCount)
    {
        // 最新のtargetCount分のスキルデータを取得
        var recentSkillDatas = ActDoOneSkillDatas.Count >= targetCount 
            ? ActDoOneSkillDatas.GetRange(ActDoOneSkillDatas.Count - targetCount, targetCount) 
            : ActDoOneSkillDatas;

        // 最新のスキルデータでIsHitがtrueのものがあるか確認
        foreach (var data in recentSkillDatas)
        {
            if (data.IsHit && data.Skill == skill)
            {
                return true;
            }
        }
        
        return false;
    }

    //FreezeConsecutiveの処理------------------------------------------------------------------------------------FreezeConsecutiveの消去、フラグの処理など-----------------------------------
    /// <summary>
    /// 現在の自分自身の実行中のFreezeConsecutiveを削除するかどうかのフラグ
    /// </summary>
    public bool IsDeleteMyFreezeConsecutive = false;

    /// <summary>
    /// FreezeConsecutive、ターンをまたぐ連続実行スキルが実行中かどうか。
    /// </summary>
    /// <returns></returns>
    public bool IsNeedDeleteMyFreezeConsecutive()
    {
        if(NowUseSkill?.NowConsecutiveATKFromTheSecondTimeOnward() == true)//連続攻撃中で、
        {
            if(NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
            {
                return true;
            }
        }
        return false;
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
    /// consecutiveな連続攻撃の消去
    /// </summary>
    public void DeleteConsecutiveATK()
    {
        if(FreezeUseSkill != null)
        {
            FreezeUseSkill.ResetAtkCountUp();//強制実行中のスキルの攻撃カウントアップをリセット
        }
        Defrost();//解除
        IsDeleteMyFreezeConsecutive = false;

    }
    //---------------------------------------------------------------------------------------------FreezeConsecutiveのフラグ、後処理など終わり------------------------------------------------------------
    /// <summary>
    /// 死んだ瞬間を判断するためのフラグ
    /// </summary>
    bool hasDied =false;
    /// <summary>
    /// 完全死滅してるかどうか。
    /// </summary>
    public bool broken = false;
    [SerializeField]
    float _machineBrokenRate = 0.3f;//インスペクタで設定する際の初期デフォルト値
    const float _lifeBrokenRate = 0.1f;//生命の壊れる確率は共通の定数
    /// <summary>
    /// OverKillが発生した場合、壊れる確率
    /// </summary>
    float OverKillBrokenRate
    {
        get
        {
            if(MyType == CharacterType.Machine)
            {
                return _machineBrokenRate;
            }
            if(MyType == CharacterType.Life)
            {
                return _lifeBrokenRate;
            }
            // そのほかのタイプに対応していない場合は例外をスロー
            throw new NotImplementedException(
            $"OverKillBrokenRate is not implemented for CharacterType: {MyType}"
        );
        }
    }

    /// <summary>
    ///     死を判定するオーバライド可能な関数
    /// </summary>
    /// <returns></returns>
    public virtual bool Death()
    {
        if (HP <= 0) 
        {
            if(!hasDied)
            {
            hasDied =true;
            DeathCallBack();
            }
            return true;
        }
        return false;
    }
    /// <summary>
    /// 復活する際の関数
    /// </summary>
    public virtual void Angel()
    {
        if(!broken)//brokenしてないなら
        {
            hasDied =false;
            HP = float.Epsilon;//生きてるか生きてないか=Angel
            if(NowPower == ThePower.high)
            {
                HP = 30;//気力が高いと多少回復
            }
        }
    }
    /// <summary>
    /// 死亡時のコールバック　SkillsTmpResetでスキルの方からリセットできるような簡単じゃない奴をここで処理する。
    /// </summary>
    public virtual void DeathCallBack()
    {
        DeleteConsecutiveATK();//連続攻撃の消去
        ApplyConditionChangeOnDeath();//人間状況の変化

        //あるかわからないが続行中のスキルを消し、
        //以外のそれ以外のスキルの連続攻撃回数消去(基本的に一個しか増えないはずだが)は以下のforeachループで行う
        foreach (var skill in SkillList)
        {
            skill.OnDeath();
        }

        //対象者ボーナス全削除
        TargetBonusDatas.AllClear();

        //パッシブの死亡時処理
        UpdateDeathAllPassiveSurvival();

        //思えの値リセット
        ResetResonanceValue();

        //精神HPの死亡時分岐
        MentalHPOnDeath();

        //落ち着きをリセット　死んだらスキルの持続力無くなるしね
        CalmDown();

    }
    void HighNessChance(BaseStates deathEne)
    {
        var matchSkillCount = 0;
        foreach(var skill in deathEne.SkillList)//倒した敵のスキルで回す
        {
            if(skill.IsTLOA && skill.SkillSpiritual == MyImpression)//スキルがTLOAで自分の精神属性と一致するなら
            {
                matchSkillCount++;
            }
        }

        if(matchSkillCount > 0 && rollper(GetPowerUpChanceOnKillEnemy(matchSkillCount)))//合致数が一個以上あり、ハイネスチャンスの確率を通過すれば。
        {
            Power1Up();
        }
    }
    /// <summary>
    /// 敵を倒した時のパワー増加確率(%)を返す関数。 「ハイネスチャンスの確率」
    /// 精神属性ごとに分岐し、一致スキルの数 × 5% を加算する。
    /// </summary>
    float GetPowerUpChanceOnKillEnemy(int matchingSkillCount)
    {
        // 基礎確率を設定
        float baseChance = MyImpression switch
        {
            SpiritualProperty.kindergarden => 40f,
            SpiritualProperty.liminalwhitetile => 30f,
            _ => 20f
        };

        // 一致スキル数 × 5% を加算
        float totalChance = baseChance + matchingSkillCount * 5f;

        // 必要に応じて上限100%に丸めるなら下記をアンコメント
        // if (totalChance > 100f) totalChance = 100f;

        return totalChance;
    }
    /// <summary>
    /// ある程度の自信ブーストの相手の強さによって持続ターンを返す関数
    /// </summary>
    /// <returns></returns>
    int GetConfidenceBoostDuration(float ratio)
    {
        return ratio switch
        {
            < 1.77f => 2,
            < 1.85f => 3,
            < 2.0f => 4,
            < 2.2f => 5,
            < 2.4f => 6,
            < 2.7f => 9,
            < 3.0f => 12,
            < 3.3f => 14,
            < 3.4f => 16,
            < 3.6f => 17,
            < 3.8f => 20,
            < 3.9f => 21,
            < 4.0f => 23,
            _ => 23 + ((int)Math.Floor(ratio - 4.0f) * 2)  // 4以降は1増えるごとに2歩増える
        };
    }
    /// <summary>
    /// ある程度の自信ブーストを記録する。
    /// </summary>
    void RecordConfidenceBoost(BaseStates target,float allkilldmg)
    {
        // 相手との強さの比率を計算
        float opponentStrengthRatio = target.TenDayValuesSum(false) / TenDayValuesSum(true);
        //自分より1.7倍以上強い敵かどうか そうじゃないならreturn
        if(opponentStrengthRatio < 1.7f)return;
        //与えたダメージが敵の最大HPの半分以上与えてるかどうか、そうじゃないならreturn
        if(allkilldmg < target.MaxHP * 0.5f)return;

        //ブーストする十日能力を敵のデフォルト精神属性を構成する一番大きいの達から取得

        //デフォルト精神属性の十日能力たちを候補リストにする
        var candidateAbilitiesList = SpritualTenDayAbilitysMap[target.DefaultImpression];
        //倒したキャラの十日能力値と合わせたリストにする。
        var candidateAbilitiyValuesList = new List<(TenDayAbility ability , float value)>();
        foreach(var ability in candidateAbilitiesList)
        {
            candidateAbilitiyValuesList.Add((ability, TenDayValues(false).GetValueOrZero(ability)));//列挙体と能力値を持つタプルのリストに変換
        }

        //複数ある場合は最大から降順で　何個ブーストされるかはパッシブや何かしらで補正されます。
        var boostCount = 1;//基本
        var walkturn = GetConfidenceBoostDuration(opponentStrengthRatio);
        for(var i = 0; i< boostCount; i++)
        {
            var MaxTenDayValue = candidateAbilitiyValuesList.Max(x => x.value);//リスト内で一番大きい値
            var MaxTenDayAbilities = candidateAbilitiyValuesList.Where(x => x.value == MaxTenDayValue).ToList();//最大キーと等しい値の能力値を全て取得

            //最大の値を持つデフォルト精神属性を構成する十日能力の内、同じ最大の値でダブってるからランダムで
            var boostAbility = RandomEx.Shared.GetItem(MaxTenDayAbilities.ToArray());
            //ブーストを記録する
            ConfidenceBoosts.Add(boostAbility.ability,walkturn);//ブースト倍率は固定なので列挙体のみ記録すればok

            candidateAbilitiyValuesList.Remove(boostAbility);//今回取得した能力値と列挙体の候補セットリストを削除
        }
    }
    /// <summary>
    /// 歩行によって自信ブーストがフェードアウトする、やってることはただのデクリメント
    /// </summary>
    protected void FadeConfidenceBoostByWalking(int count)
    {
        //辞書のキーをリストにしておく (そのまま foreach で書き換えるとエラーになる可能性がある)
        var keys = ConfidenceBoosts.Keys.ToList();

        //キーを回して、値を取り出し -1 して戻す
        foreach (var key in keys)
        {
            ConfidenceBoosts[key]-= count;
            
            //もし歩行ターンが0以下になったら削除する
            if (ConfidenceBoosts[key] <= 0) { ConfidenceBoosts.Remove(key); }
        }
    }
    /// <summary>
    /// 攻撃した相手が死んだ場合のコールバック
    /// </summary>
    void OnKill(BaseStates target)
    {
        //まず殺すまでのダメージを取得する。
        var AllKillDmg = DamageDealtToEnemyUntilKill[target];
        DamageDealtToEnemyUntilKill.Remove(target);//殺したので消す(angelしたらもう一回最初から記録する)

        HighNessChance(target);//ハイネスチャンス(ThePowerの増加判定)
        ApplyConditionChangeOnKillEnemy(target);//人間状況の変化

        RecordConfidenceBoost(target,AllKillDmg);

        //ここの殺した瞬間のはみんな精神属性の分岐では　=> スキルの精神属性　を使えば、　実行した瞬間にそのスキルの印象に染まってその状態の精神属性で分岐するってのを表現できる
        //(スキル属性のキャラ代入のタイミングについて　を参照)
        
    }


    /// <summary>
    /// 持ってるスキルリストを初期化する
    /// 立場により持ってる実体スキルの扱い方が異なるので各派生クラスで実装する。
    /// </summary>
    public abstract void OnInitializeSkillsAndChara();
    /// <summary>
    /// BM終了時に全スキルの一時保存系プロパティをリセットする
    /// </summary>
    public void OnBattleEndSkills()
    {
        foreach (var skill in SkillList)
        {
            skill.OnBattleEnd();//プロパティをリセットする
        }
    }
    public void OnBattleStartSkills()
    {
        foreach (var skill in SkillList)
        {
            skill.OnBattleStart();
        }
    }

    /// <summary>
    ///追加HPを適用  passiveと違い適合条件がないからvoid
    /// </summary>
    public void ApplyVitalLayer(int id)
    {
        //リスト内に同一の物があるか判定する。
        var sameHP = _vitalLayerList.FirstOrDefault(lay => lay.id == id);
        if (sameHP != null)
        {
            sameHP.ReplenishHP();//同一の為リストにある側を再補充する。
        }
        else//初物の場合
        {
            var newLayer = VitalLayerManager.Instance.GetAtID(id).DeepCopy();//マネージャーから取得 当然ディープコピー
            newLayer.ReplenishHP();//maxにしないとね
            //優先順位にリスト内で並び替えつつ追加
            // _vitalLaerList 内で、新しいレイヤーの Priority より大きい最初の要素のインデックスを探す
            int insertIndex = _vitalLayerList.FindIndex(v => v.Priority > newLayer.Priority);

            if (insertIndex < 0)
            {
                // 該当する要素が見つからなかった場合（全ての要素が新しいレイヤー以下の Priority ）
                // リストの末尾に追加
                _vitalLayerList.Add(newLayer);
            }
            else
            {
                // 新しいレイヤーを適切な位置に挿入
                _vitalLayerList.Insert(insertIndex, newLayer);
            }
        }
    }
    /// <summary>
    /// 追加HPを消す
    /// </summary>
    public void RemoveVitalLayerByID(int id)
    {
        var layer = _vitalLayerList.FirstOrDefault(lay => lay.id == id);
        if (layer != null)//あったら消す
        {
            _vitalLayerList.Remove(layer);
        }
        else
        {
            Debug.Log("RemoveVitalLayer nothing. id:" + id);
        }
    }


    /// <summary>
    /// 戦闘中に次のターンに進む際のコールバック
    /// </summary>
    public void OnNextTurnNoArgument()
    {
        UpdateTurnAllPassiveSurvival();
        UpdateAllSkillPassiveSurvival();
        UpdateNotVanguardAllPassiveSurvival();
        PassivesOnNextTurn();//パッシブのターン進効果

        //生きている場合にのみする処理
        if(!Death())
        {
            ConditionInNextTurn();//人間状況ターン変化
            TryMentalPointRecovery();//精神HPが自動回復される前に精神HPによるポイント自然回復の判定

            if(IsMentalDiverGenceRefilCountDown() == false)//再充填とそのカウントダウンが終わってるのなら
            {
                MentalDiverGence();
                if(_mentalDivergenceRefilCount > 0)//乖離が発生した直後に回復が起こらないようにするif 再充填カウントダウンがセットされたら始まってるから
                {
                    MentalHPHealOnTurn();//精神HP自動回復
                }
               
            }

            CalmDownCountDec();//落ち着きカウントダウン
        }
       
        ApplyBufferApplyingPassive();//パッシブをここで付与。 =>詳細は豚のパッシブみとけ
        ApplySkillsBufferApplyingSkillPassive();//スキルパッシブも付与

        //記録系
        _tempLive = !Death();//死んでない = 生きてるからtrue
        BattleFirstSurpriseAttacker = false;//絶対にoff bm初回先手攻撃フラグは
        
        //フラグ系
        SelectedEscape = false;//選択を解除
        SkillCalculatedRandomRange = false;//ランダム範囲計算フラグを解除
    }

    /// <summary>
    /// 戦闘開始時に「パッシブ」の持続歩行ターンの場合により、持続戦闘ターン数をリセットする
    /// </summary>
    public void PassivesReSetDurationTurnOnBattleStart()
    {
        foreach (var pas in Passives)//全ての所持パッシブ
        {
            //歩行ターンのif文戦闘ターンの処理分け
            if(pas.DurationWalk - 2 >= pas.DurationWalkCounter)//現在値が設定値-2　以下なら
            {
                //歩行ターンがどの程度減っているかを割合で入手
                var rate = pas.DurationWalkCounter / pas.DurationWalk;
                //その割合を戦闘ターン設定値に掛けて、再代入する。
                pas.DurationTurnCounter = pas.DurationTurn * rate;
                Debug.Log("戦闘パッシブのターンが歩行ターンが2以下になったので再変更の機会	→変化: " + pas.DurationTurnCounter + " / " + pas.DurationTurn 
                + "× " + rate);
            }
        }
    }
    /// <summary>
    ///bm生成時に初期化される関数
    /// </summary>
    public virtual void OnBattleStartNoArgument()
    {
        TempDamageTurn = 0;
        SelectedEscape = false;//選択を解除
        SkillCalculatedRandomRange = false;//ランダム範囲計算フラグを解除
        CalmDownSet(AGI().Total * 1.3f,1f);//落ち着きカウントの初回生成　
        //最初のSkillACT前までは回避率補正は1.3倍 攻撃補正はなし。=100%\

        _tempVanguard = false;
        _tempLive = true;
        Rivahal = 0;//ライバハル値を初期化
        Target = 0;//どの辺りを狙うかの初期値
        RangeWill = 0;//範囲意志の初期値
        DecisionKinderAdaptToSkillGrouping();//慣れ補正の優先順位のグルーピング形式を決定するような関数とか
        DecisionSacriFaithAdaptToSkillGrouping();
        ActDoOneSkillDatas = new List<ACTSkillDataForOneTarget>();//スキルの行動記録はbm単位で記録する。
        DidActionSkillDatas = new();//スキルのアクション事の記録データを初期化
        damageDatas = new();
        FocusSkillImpressionList = new();//慣れ補正用スキル印象リストを初期化
        TargetBonusDatas = new();
        ConditionTransition();
        RecovelyWaitStart();//リカバリーターンのリセット
        _mentalDivergenceRefilCount = 0;//精神HP乖離の再充填カウントをゼロに戻す
        _mentalDivergenceCount = 0;//精神HP乖離のカウントをゼロに戻す
        _mentalPointRecoveryCountUp = 0;//精神HP自然回復のカウントをゼロに戻す
        DamageDealtToEnemyUntilKill = new();//戦闘開始時にキャラクターを殺すまでに与えたダメージを記録する辞書を初期化する
        battleGain = new();//バトルが開始するたびに勝利ブースト用の値を初期化
        PassivesReSetDurationTurnOnBattleStart();//パッシブの持続戦闘ターン数を場合により計算して再代入する。

        InitPByNowPower();//Pの初期値設定

        //初期精神HPは常に戦闘開始時に最大値
        MentalHP = MentalMaxHP;

        //スキルの戦闘開始時コールバック
        OnBattleStartSkills();
        
    }
    public virtual void OnBattleEndNoArgument()
    {
        DeleteConsecutiveATK();//連続攻撃を消す
        NowUseSkill = null;//現在何のスキルも使っていない。
        TempDamageTurn = 0;
        DeleteConsecutiveATK();
        DecayOfPersistentAdaptation();//恒常的な慣れ補正の減衰　　持ち越しの前に行われる　じゃないと記憶された瞬間に忘れてしまうし
        AdaptCarryOver();//慣れ補正持ち越しの処理
        battleGain.Clear();//勝利ブーストの値をクリアしてメモリをよくする
        foreach(var layer in _vitalLayerList.Where(lay => lay.IsBattleEndRemove))
        {
            RemoveVitalLayerByID(layer.id);//戦闘の終了で消える追加HPを持ってる追加HPリストから全部消す
        }
        foreach(var passive in _passiveList.Where(pas => pas.DurationWalkCounter < 0))
        {
            RemovePassive(passive);//歩行残存ターンが-1の場合戦闘終了時に消える。
        }
        foreach (var pas in _passiveList)
        {
            pas.DurationTurnCounter = -1;//歩行ターンがあれば残存するが、それには関わらず戦闘ターンは意味がなくなるので全て-1 = 戦闘ターンによる消えるのをなくす。
        }
        //スキルパッシブの戦闘終了時の歩行ターンによる処理は下のスキルコールバックでやってます。
    
        //スキルの戦闘終了時コールバック
        OnBattleEndSkills();
    }

    //慣れ補正ーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーAdaptToSkillsGroupingのための関数たちーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーー
    /// <summary>
    /// ベールドライヴァル用の慣れ補正の優先順位のグルーピング
    /// グルーピングっていうか　3 を境目に二つに分かれるだけ。
    /// </summary>
    int GetBaleAdaptToSkillGrouping(int number)
    {//序列が 0～2　つまり三位までなら
        if (number < 3) return 0;//グループ序列は0
        else return 1;//それ以降の順位なら、グループ序列は１
    }
    /// <summary>
    /// ゴッドティアー用の慣れ補正の優先順位のグルーピング
    /// 6ごとに区分けする
    /// </summary>
    int GetGodtierAdaptToSkillGrouping(int number)
    {
        return number / 6;
    }

    /// <summary>
    /// 支柱用の慣れ補正の優先順位のグルーピング
    /// 5ごとに区分けする
    /// </summary>
    int GetPillarAdaptToSkillGrouping(int number)
    {
        return number / 5;
    }
    /// <summary>
    /// ドレミスは用の慣れ補正の優先順位のグルーピング
    /// 最初の六つ(0～5)  そしてその後は七つ区切り
    /// </summary>
    int GetDoremisAdaptToSkillGrouping(int number)
    {
        if (number < 6)//最初の六つならそのまま
        {
            return number;
        }
        // 6以降は7つごと
        // 6～12 → グループ6
        // 13～19 → グループ7
        // 20～26 → グループ8 ...
        return 6 + (number - 6) / 7;  //6足してんのは最初の六つの固定グループ以降の順列であるから。　6引いてんのは最初の六つによるずれ修正
    }


    /// <summary>
    /// リーミナルホワイト用の素数による慣れ補正の"-スキル優先順位-"のグルーピング方式
    /// 引数の整数が何番目のグループに属するかを返す
    /// グループは0からはじまり、各素数を境界として区切る。
    /// グループnは [p_(n-1), p_n - 1] の範囲（p_0=0と仮定, p_1=2）
    /// 例: p_1=2の場合、グループ1は 0～1
    ///     p_2=3の場合、グループ2は 2～2
    ///     p_3=5の場合、グループ3は 3～4
    /// </summary>
    int GetLiminalAdaptToSkillGrouping(int number)
    {
        // number以上の素数を取得
        int primeAbove = GetPrimeAbove(number);

        // primeAboveがp_nだとして、p_(n-1)からp_n-1までがn番目のグループとなる
        // p_1=2, p_0=0とみなす
        int index = GetPrimeIndex(primeAbove); // primeAboveが何番目の素数か(2が1番目)
        int prevPrime = (index == 1) ? 0 : GetPrimeByIndex(index - 1); // 前の素数(なければ0)

        // prevPrime～(primeAbove-1)がindex番目のグループ
        if (number >= prevPrime && number <= primeAbove - 1)
        {
            return index;
        }

        return -1; // 理論上起こらないが安全策
    }
    /// <summary>
    /// シークイエスト用の十ごとに優先順位のグループ分けし、
    /// 渡されたindexが何番目に属するかを返す関数
    /// </summary>
    int GetCquiestAdaptToSkillGrouping(int number)
    {
        if (number < 0) return -1; // 負数はエラー扱い

        //0~9をグループ0、10~19をグループ1、20~29をグループ2…という風に10刻みで
        // グループ分けを行い、渡された整数がどのグループかを返します。
        // 
        // 例:
        //  - 0～9   → グループ0
        //  - 10～19 → グループ1
        //  - 20～29 → グループ2
        return number / 10;
    }
    /// <summary>
    /// 慣れ補正でintの精神属性ごとのグループ分け保持リストから優先順位のグループ序列を入手する関数
    /// 整数を受け取り、しきい値リストに基づいてその値が所属するグループ番号を返します。
    /// しきい値リストは昇順にソートされていることを想定。
    /// </summary>
    int GetAdaptToSkillGroupingFromList(int number, List<int> sequence)
    {
        //  例: thresholds = [10,20,30]
        // number <= 10 -> グループ0
        // 11 <= number <= 20 -> グループ1
        // 21 <= number <= 30 -> グループ2
        // 31 <= number     -> グループ3

        // 値がしきい値リストの最初の値以下の場合、0番目のグループに属するとする
        // ここは要件に合わせて調整可能。
        for (int i = 0; i < sequence.Count; i++)
        {
            if (number <= sequence[i])
            {
                return i;
            }
        }

        // 全てのしきい値を超えた場合は最後のグループ+1を返す
        return sequence.Count;
    }
    /// <summary>
    /// 自己犠牲用の慣れ補正のグルーピング方式の素数を交えた乱数を決定する。
    /// "bm生成時"に全キャラにこれを通じて決定される。
    /// 指定した数だけ素数を生成し、それらをリストに入れ、それらの素数の間に任意の数の乱数を挿入した数列を作成する
    /// </summary>
    void DecisionSacriFaithAdaptToSkillGrouping()
    {
        var primes = GetFirstNPrimes(countPrimes);
        if (primes == null || primes.Count == 0)
        {
            throw new ArgumentException("primesリストが空です");
        }
        if (insertProbability < 0.0 || insertProbability > 1.0)
        {
            throw new ArgumentException("insertProbabilityは0.0～1.0の間で指定してください");
        }

        List<int> result = new List<int>();

        for (int i = 0; i < primes.Count - 1; i++)
        {
            int currentPrime = primes[i];
            int nextPrime = primes[i + 1];

            // 現在の素数を追加
            result.Add(currentPrime);

            int gapStart = currentPrime + 1;
            int gapEnd = nextPrime - 1;

            // gap範囲を計算
            int gapRange = gapEnd - gapStart + 1;
            if (gapRange > 0)
            {
                // gapRange / 2.5回分の挿入判定を行う
                int tries = (int)Math.Floor(gapRange / 2.5);
                if (tries > 0)
                {
                    List<int> insertedNumbers = new List<int>();

                    for (int t = 0; t < tries; t++)
                    {
                        // 挿入確率判定
                        if (RandomEx.Shared.NextDouble() < insertProbability)
                        {
                            // gap内からランダムに1つ取得
                            int randomValue = RandomEx.Shared.NextInt(gapStart, gapEnd + 1);

                            if (!insertedNumbers.Contains(randomValue)) //重複してたら追加しない。
                                insertedNumbers.Add(randomValue);
                        }
                    }

                    // 取得した乱数をソートして追加（昇順順列にするため）
                    insertedNumbers.Sort();

                    foreach (var val in insertedNumbers)
                    {
                        result.Add(val);
                    }
                }
            }
        }

        // 最後の素数を追加
        result.Add(primes[primes.Count - 1]);

        SacrifaithAdaptToSkillGroupingIntegerList = result;//保持リストに入れる
    }
    //[Header("自己犠牲の慣れ補正用　HPの想定範囲 基本的に初期値からいじらない")]
    int countPrimes = 77;//生成する素数の数
    double insertProbability = 0.2;
    /// <summary>
    /// 自己犠牲用の慣れ補正グルーピングの数列を保持するリスト
    /// </summary>
    List<int> SacrifaithAdaptToSkillGroupingIntegerList;
    /// <summary>
    /// 指定した数の素数を小さい順に返す
    /// 簡易的な実装。多くの素数が欲しい場合は、高速なアルゴリズム(エラトステネスの篩など)に切り替え推奨
    /// </summary>
    private List<int> GetFirstNPrimes(int n)
    {
        List<int> primes = new List<int>();
        int num = 2;

        while (primes.Count < n)
        {
            if (IsPrime(num))
            {
                primes.Add(num);
            }
            num++;
        }

        return primes;
    }
    /// <summary>
    /// キンダーガーデン用の慣れ補正のグルーピング方式の乱数を決定する。
    /// "bm生成時"に全キャラにこれを通じて決定される。
    /// </summary>
    void DecisionKinderAdaptToSkillGrouping()
    {
        // decayRateを計算
        // completionFraction = exp(-decayRate*(maxHP-minHP))
        // decayRate = -ln(completionFraction)/(maxHP-minHP)
        if (completionFraction <= 0f || completionFraction >= 1f)
        {
            // completionFractionは0～1の間で設定してください
            completionFraction = 0.01f;
        }

        decayRate = -Mathf.Log(completionFraction) / (kinderGroupingMaxSimHP - kinderGroupingMinSimHP);

        var sum = 0;
        KinderAdaptToSkillGroupingIntegerList = new List<int>();//慣れ補正用のinteger保持リストを初期化
        //大体70個ほど決定する。hpの大きさに応じて最大間隔が狭まる
        for (var i = 0; i < 70; i++)
        {
            sum += RandomEx.Shared.NextInt(1, Mathf.RoundToInt(GetKinderGroupingIntervalRndMax()) + 1);
            KinderAdaptToSkillGroupingIntegerList.Add(sum);
        }

    }
    //[Header("キンダーガーデンの慣れ補正用　HPの想定範囲 基本的に初期値からいじらない")]
    float kinderGroupingMinSimHP = 1;    // ゲーム中でのHPの想定してる最小値
    float kinderGroupingMaxSimHP = 80;   // ゲーム中での想定してるHPの最大値(ここまでにキンダーガーデンの優先順位間隔が下がりきる。)

    //[Header("キンダーガーデンの慣れ補正用　出力値調整　基本的に初期値からいじらない")]
    float InitKinderGroupingInterval = 17;   // 最小HP時の出力値
    float limitKinderGroupingInterval = 2;    // 最大HP時に近づいていく限界値

    //[Tooltip("最大HP時点で、開始の値から限界の値までの差をどの割合まで縮めるか。\n0に近いほど限界値により近づく(下がりきる)。\n例えば0.01なら1%まで縮まる。")]
    float completionFraction = 0.01f;

    private float decayRate;
    /// <summary>
    /// キンダーガーデン用の慣れ補正グルーピングの数列を保持するリスト
    /// </summary>
    List<int> KinderAdaptToSkillGroupingIntegerList;
    /// <summary>
    /// キンダーガーデン用のグループ区切りでの乱数の最大値をゲットする。
    /// </summary>
    /// <returns></returns>
    float GetKinderGroupingIntervalRndMax()
    {
        // f(hp) = limitValue + (startValue - limitValue) * exp(-decayRate * (キャラの最大HP - minHP))
        float result = limitKinderGroupingInterval + (InitKinderGroupingInterval - limitKinderGroupingInterval) * Mathf.Exp(-decayRate * (_maxhp - kinderGroupingMinSimHP));
        return result;
    }
    /// <summary>
    /// n以上の素数のうち、最初に出てくる素数を返す
    /// nが素数ならnを返す
    /// </summary>
    int GetPrimeAbove(int n)
    {
        if (n <= 2) return 2;
        int candidate = n;
        while (!IsPrime(candidate))
        {
            candidate++;
        }
        return candidate;
    }

    /// <summary>
    /// 素数pが全素数列(2,3,5,7,...)の中で何番目かを返す(2が1番目)
    /// </summary>
    int GetPrimeIndex(int p)
    {
        int count = 0;
        int num = 2;
        while (num <= p)
        {
            if (IsPrime(num))
            {
                count++;
                if (num == p) return count;
            }
            num++;
        }
        return -1;
    }

    /// <summary>
    /// index番目(1-based)の素数を返す
    /// 1 -> 2, 2 -> 3, 3 -> 5, ...
    /// </summary>
    int GetPrimeByIndex(int index)
    {
        if (index < 1) throw new ArgumentException("indexは1以上である必要があります");
        int count = 0;
        int num = 2;
        while (true)
        {
            if (IsPrime(num))
            {
                count++;
                if (count == index)
                {
                    return num;
                }
            }
            num++;
        }
    }

    /// <summary>
    /// 素数判定(簡易)
    /// </summary>
    bool IsPrime(int x)
    {
        if (x < 2) return false;
        if (x == 2) return true;
        if (x % 2 == 0) return false;
        int limit = (int)Math.Sqrt(x);
        for (int i = 3; i <= limit; i += 2)
        {
            if (x % i == 0) return false;
        }
        return true;
    }


//ここから慣れ補正のメイン計算部分--------------------------------------------------------------------------------------------------------------------------------    
    /// <summary>
    /// 慣れ補正用　スキル印象の注目リスト
    /// </summary>
    public List<FocusedSkillImpressionAndUser> FocusSkillImpressionList = new List<FocusedSkillImpressionAndUser>();

    /// <summary>
    /// 注目リスト内でのスキルのy優先順位の序列を返す 0から数えるインデックス　0から数える
    /// </summary>
    int AdaptPriorityDamageToSkill(SkillImpression imp)
    {
        //ダメージの大きさで並び替えて
        FocusSkillImpressionList = FocusSkillImpressionList.OrderByDescending(skill => skill.TopDmg).ToList();

        return FocusSkillImpressionList.FindIndex(fo => fo.skillImpression == imp);
    }
    /// <summary>
    /// 注目リスト内でのスキルのy優先順位の序列を返す 0から数えるインデックス　0から数える
    /// </summary>
    int AdaptPriorityMemoryToSkill(SkillImpression imp)
    {
        //記憶回数のカウントで並び替えて
        FocusSkillImpressionList = FocusSkillImpressionList.OrderByDescending(skill => skill.MemoryCount(PersistentAdaptSkillImpressionMemories)).ToList();

        return FocusSkillImpressionList.FindIndex(fo => fo.skillImpression == imp);
    }

    /// <summary>
    /// 現在のスキルの優先序列がどのグループ序列に属してるか
    /// 各関数のツールチップにグループ分け方式の説明アリ
    /// </summary>
    int AdaptToSkillsGrouping(int index)
    {
        int groupIndex;
        if (index < 0) return -1;//負の値が優先序列として渡されたらエラー
        switch (MyImpression)//自分の印象によってスキルのグループ分けが変わる。
        {
            case SpiritualProperty.liminalwhitetile:
                groupIndex = GetLiminalAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.kindergarden:
                // キンダーガーデン用の素数による慣れ補正の"-スキル優先順位-"のグルーピング方式
                // 引数の整数が何番目のグループに属するかを返す
                // 最大HPが多ければ多いほど、乱数の間隔が狭まりやすい　= ダメージ格差による技への慣れの忘れやすさと慣れやすさが低段階化しやすい
                groupIndex = GetAdaptToSkillGroupingFromList(index, KinderAdaptToSkillGroupingIntegerList);
                break;
            case SpiritualProperty.sacrifaith:
                //自己犠牲は素数の間に　素数間隔 / 2.5　回　その間の数の乱数を入れる。
                //つまり素数と乱数の混じった優先順位のグループ分けがされる
                groupIndex = GetAdaptToSkillGroupingFromList(index, SacrifaithAdaptToSkillGroupingIntegerList);
                break;
            case SpiritualProperty.cquiest:
                //シークイエストは十ごとに区分けする。
                groupIndex = GetCquiestAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.baledrival:
                //ベールドライヴァルは三位以降以前に区分けする。
                groupIndex = GetBaleAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.godtier:
                //ゴッドティアは六つごとに区分けする
                groupIndex = GetGodtierAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.pillar:
                //支柱は六つごとに区分けする
                groupIndex = GetPillarAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.doremis:
                //ドレミスは六つ固定　以降七つ区切り
                groupIndex = GetDoremisAdaptToSkillGrouping(index);
                break;


            default:
                groupIndex = index;//デビルとサイコパスは省く
                break;
        }

        return groupIndex;
    }
    /// <summary>
    /// DEFによる基礎上昇値(慣れ補正)　記憶回数に加算されるものです。
    /// </summary>
    float GetBaseMemoryIncreaseValue()
    {
        var def = DEF().Total;
        if (def <= increaseThreshold)
        {
            // 第1段階: startIncreaseValueからmidLimitIncreaseValueへ収束
            return midLimitIncreaseValue + (startIncreaseValue - midLimitIncreaseValue) * Mathf.Exp(-increaseDecayRate1 * def);
        }
        else
        {
            // 第2段階: threshold超過後はmidLimitIncreaseValueからfinalLimitIncreaseValueへ超緩やかに減少
            float excess = def - increaseThreshold;
            return finalLimitIncreaseValue + (midLimitIncreaseValue - finalLimitIncreaseValue) * Mathf.Exp(-increaseDecayRate2 * excess);
        }
    }
    //[Header("慣れ補正のDEFによる基礎上昇値パラメータ（第1段階）")]
    float startIncreaseValue = 1.89f; // DEF=0での基礎上昇値 
    float midLimitIncreaseValue = 4.444f; // 中間で収束する上昇値 
    float increaseDecayRate1 = 0.0444f; // 第1段階でstart→midLimitへ近づく速度 
    float increaseThreshold = 100f; // 第2段階移行DEF値 

    //[Header("慣れ補正のDEFによる基礎上昇値パラメータ（第2段階）")]
    float finalLimitIncreaseValue = 8.9f; // 第2段階で最終的に近づく値 
    float increaseDecayRate2 = 0.0027f; // 第2段階でmid→finalLimitへ近づく速度 
    /// <summary>
    /// DEFによる基礎減少値を返す。　これは慣れ補正の記憶回数に加算される物。
    /// </summary>
    float GetBaseMemoryReducationValue()
    {
        var def = DEF().Total;//攻撃によって減少されないまっさらな防御力
        if (def <= thresholdDEF)
        {
            // 第1段階: StartValueからmidLimitValueへ収束
            // f(DEF) = midLimitValue + (StartValue - midLimitValue)*exp(-decayRate1*DEF)
            return midLimitValue + (startValue - midLimitValue) * Mathf.Exp(-decayRate1 * def);
        }
        else
        {
            // 第2段階: thresholdを超えたらmidLimitValueから0へ超ゆるやかな減衰
            // f(DEF) = finalLimitValue + midlimtValue * exp(-decayRate2*(DEF - threshold))
            float excess = def - thresholdDEF;
            return finalLimitValue + (midLimitValue - finalLimitValue) * Mathf.Exp(-decayRate2 * excess);
        }
    }
    //[Header("慣れ補正のDEFによる基礎減少値パラメータ（第1段階）")]
    float startValue = 0.7f;   // DEF=0での基礎減少値
    float midLimitValue = 0.2f; // 中間の下限値(比較的到達しやすい値)
    float decayRate1 = 0.04f;  // 第1段階で開始値から中間の下限値へ近づく速度
    float thresholdDEF = 88f;    // 第1段階から第2段階へ移行するDEF値

    //[Header("パラメータ（第2段階）")]
    // 第2段階：0.2から0への超低速な減衰
    float finalLimitValue = 0.0f;//基礎減少値がDEFによって下がりきる最終下限値　　基本的に0
    float decayRate2 = 0.007f; // 非常に小さい値にしてfinalLimitValueに収束するには莫大なDEFが必要になる

    /// <summary>
    /// 前にダメージを受けたターン
    /// </summary>
    int TempDamageTurn;
    /// <summary>
    /// 記憶回数の序列割合をゲット
    /// 指定されたインデックスがリスト内でどの程度の割合に位置しているかを計算します。
    /// 先頭が1.0、末尾が0.0の割合となります。 
    /// </summary>
    float GetMemoryCountRankRatio(int index)
    {
        if (FocusSkillImpressionList.Count == 1)
            return 1.0f; // リストに1つだけの場合、割合は1.0

        // 先頭が1.0、末尾が0.0となるように割合を計算
        return 1.0f - ((float)index / (FocusSkillImpressionList.Count - 1));
    }
    /// <summary>
    /// 自身の精神属性による記憶段階構造と範囲の取得
    /// </summary>
    List<MemoryDensity> MemoryStageStructure()
    {
        List<MemoryDensity> rl;
        switch (MyImpression)//左から降順に入ってくる　一番左が最初の、一番上の値ってこと
        {
            case SpiritualProperty.doremis:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.Medium, MemoryDensity.Medium };
                break;//しっかりと　普通　普通

            case SpiritualProperty.pillar:
                rl = new List<MemoryDensity> { MemoryDensity.Medium, MemoryDensity.Medium, MemoryDensity.Medium,
                MemoryDensity.Medium,MemoryDensity.Medium,MemoryDensity.Medium,};
                break;//普通　×6

            case SpiritualProperty.kindergarden:
                rl = new List<MemoryDensity> { MemoryDensity.Low };
                break;//薄い

            case SpiritualProperty.liminalwhitetile:
                rl = new List<MemoryDensity> { MemoryDensity.Medium,MemoryDensity.Medium,
                    MemoryDensity.Low,MemoryDensity.Low, MemoryDensity.Low};
                break;//普通×2 薄い×3

            case SpiritualProperty.sacrifaith:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.Low };
                break;//ハイアンドロー

            case SpiritualProperty.cquiest:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.High,MemoryDensity.High,MemoryDensity.High, MemoryDensity.High,
                MemoryDensity.Low};//しっかりと×5 //薄い1
                break;

            case SpiritualProperty.pysco:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.Low };
                break;//ハイアンドロー

            case SpiritualProperty.godtier:
                rl = new List<MemoryDensity> { MemoryDensity.High,MemoryDensity.High,MemoryDensity.Medium,
                MemoryDensity.Medium,MemoryDensity.Medium,MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low};
                break;//しっかりと×2 普通×3 薄く×3

            case SpiritualProperty.baledrival:
                rl = new List<MemoryDensity> { MemoryDensity.High,MemoryDensity.High,MemoryDensity.High,MemoryDensity.High,
                MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low,
                MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low};
                break;//しっかりと×4 薄く　×8

            case SpiritualProperty.devil:
                rl = new List<MemoryDensity> { MemoryDensity.Medium,MemoryDensity.Medium,
                MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low};
                break;//普通×2 薄く×3
            default:
                rl = new List<MemoryDensity> { MemoryDensity.Low };
                break;//適当

        }
        return rl;
    }
    /// <summary>
    /// 攻撃力を減衰する最終的な"慣れ"の基礎量
    /// </summary>
    float GetBaseAdaptValue()
    {
        const float bValue = 0.0004f; //ここの単位調節は1バトルの長さと密接に関係すると思う。

        return bValue * b_EYE.Total;//基礎命中率で補正。　「慣れは"元々"の視力と、記憶の精神由来の構成が物を言います。」
    }
    /// <summary>
    /// 慣れ補正割合値のデフォルトの下限しきい値
    /// </summary>
    const float initialAdaptThreshold = 0.85f;
    /// <summary>
    /// 慣れ補正割合値の下限しきい値がEYEが高いと下がる限界値
    /// </summary>
    const float maxEyeAdaptThreshold = 0.5566778f;

    /// <summary>
    /// EYE()を用いてAdaptModifyが下回らないようにする特定の下限しきい値を計算する関数
    /// - EYE()が0～30の間ではthresholdは0.85に固定
    /// - EYE()が30を超え、134まで増加するにつれてthresholdが非線形に0.85からmaxEyeThresholdへ減少
    /// - EYE()が134以上ではthresholdはmaxEyeThresholdに固定
    /// </summary>
    float CalculateEYEBasedAdaptThreshold()
    {
        // 定数の設定
        const float minEYE = 30f;//この値まではデフォルトのまま
        const float maxEYE = 134f;//EYEが補正される限界値

        // 現在のEYE()値を取得
        float eyeValue = EYE().Total;

        // EYE()の値に応じてthresholdを計算
        if (eyeValue <= minEYE)
        {
            // EYE()が30以下の場合
            return initialAdaptThreshold;
        }
        else if (eyeValue >= maxEYE)
        {
            // EYE()が134以上の場合
            return maxEyeAdaptThreshold;
        }
        else
        {
            // EYE()が30を超え134未満の場合

            // EYE()を0から1に正規化（30から134を0から1にマッピング）
            float normalizedEye = (eyeValue - minEYE) / (maxEYE - minEYE);

            // シグモイド関数のパラメータ設定
            // k: 勾配（大きいほど急激な変化）
            // x0: シグモイドの中心点（ここでは0.5に設定）
            float k = 10f;        // 調整可能なパラメータ
            float x0 = 0.5f;      // 中心点

            // シグモイド関数の計算
            float sigmoid = 1 / (1 + Mathf.Exp(-k * (normalizedEye - x0)));

            // thresholdを計算
            // thresholdは初期値からmaxEyeThresholdへの変化
            float threshold = initialAdaptThreshold - (initialAdaptThreshold - maxEyeAdaptThreshold) * sigmoid;

            // クランプ（安全策としてしきい値が範囲内に収まるように）
            threshold = Mathf.Clamp(threshold, maxEyeAdaptThreshold, initialAdaptThreshold);

            return threshold;
        }
    }

    /// <summary>
    /// 目の瞬きをするように、
    /// 慣れ補正がデフォルトの下限しきい値を下回っている場合、そこまで押し戻される関数
    /// </summary>
    /// <param name="adapt">現在の慣れ補正値 (例: 0.7 など)</param>
    /// <param name="largestMin">最大下限しきい値(例: 0.556677)</param>
    /// <param name="defaultMin">デフォルトの下限しきい値(例: 0.85)</param>
    /// <param name="kMax">バネ係数の最大値(適宜調整)</param>
    /// <returns>押し戻し後の値(一度きりで計算)</returns>    
    float EyeBlink(
    float adapt,
    float largestMin,
    float defaultMin,
    float kMax
)
    {
        // もし adapt が defaultMin 以上なら押し戻し不要なので、そのまま返す
        if (adapt >= defaultMin)
            return adapt;

        // 1) ratio = (adapt - largestMin) / (defaultMin - largestMin)
        //    → adapt が largestMin に近いほど ratio が小さくなり、結果として k が大きくなる
        //    → adapt が 0.85 に近いほど ratio が 1 に近くなり、k は 0 に近づく(押しが弱い)
        float ratio = (adapt - largestMin) / (defaultMin - largestMin);
        ratio = Mathf.Clamp01(ratio);

        // 2) k を計算：近いほど k 大きく
        //    ここでは単純に k = kMax * (1 - ratio)
        //    ratio=0(=adapt==largestMin付近) → k=kMax(最大)
        //    ratio=1(=adapt==defaultMin付近) → k=0(押しなし)
        float k = kMax * (1f - ratio);

        // 3) バネ式で一度だけ押し上げ
        float diff = defaultMin - adapt;  // 正の数(例: 0.85-0.7=0.15)
                                          // e^(-k) を掛ける
        float newDiff = diff * Mathf.Exp(-k);
        // 実際に adapt を上書き
        float newAdapt = defaultMin - newDiff; // 例: 0.85 - (0.15*exp(-k))

        // 4) もし何らかの理由で newAdapt が既に defaultMin 超えるならクランプ
        if (newAdapt > defaultMin) newAdapt = defaultMin;

        return newAdapt;
    }

    //[Header("慣れ補正のランダム性設定")]
    //[SerializeField, Range(0.0f, 0.2f)]//インスペクタ上で調節するスライダーの範囲
    private float randomVariationRange = 0.04f; // ±%の変動
    /// <summary>
    /// スキルに慣れる処理 慣れ補正を返す
    /// </summary>
    float AdaptToSkill(BaseStates enemy, BaseSkill skill, StatesPowerBreakdown dmg)
    {
        var donthaveskillImpression = true;//持ってないフラグ
        var IsFirstAttacker = false;//知っているスキル印象に食らったとき、その攻撃者が初見かどうか
        var IsConfused = false;//戸惑いフラグ
        float AdaptModify = -1;//デフォルト値
        var nowTurn = manager.BattleTurnCount;//現在のターン数
        FocusedSkillImpressionAndUser NowFocusSkillImpression = null;//今回食らった注目慣れスキル印象

        //今回食らうスキル印象が既に食らってるかどうかの判定ーーーーーーーーーーーーーーーーー
        foreach (var fo in FocusSkillImpressionList)
        {
            if (fo.skillImpression == skill.Impression)//スキル既にあるなら
            {
                fo.DamageMemory(dmg.Total);// ダメージ記録 慣れ補正用の優先順位などを決めるためのダメージ記録なのでそのままtotalでok
                donthaveskillImpression = false;//既にあるフラグ！
                if (IsFirstAttacker = !fo.User.Any(chara => chara == enemy))//攻撃者が人員リストにいない場合　true
                {
                    fo.User.Add(enemy);//敵をそのスキルのユーザーリストに登録
                }
                NowFocusSkillImpression = fo;//既にあるスキル印象を今回の慣れ注目スキル印象に
            }
        }
        //もし初めて食らうのならーーーーーーーーーーーーーーーーー
        if (donthaveskillImpression)
        {
            NowFocusSkillImpression = new FocusedSkillImpressionAndUser(enemy, skill.Impression, dmg.Total);//新しく慣れ注目印象に
            FocusSkillImpressionList.Add(NowFocusSkillImpression);//最初のキャラクターとスキルを記録
        }

        //前回"スキル問わず"攻撃を受けてから今回受けるまでの　"経過ターン"
        //(スキル性質がAttackのとき、必ず実行されるから　攻撃を受けた間隔　が経過ターンに入ります　スキルによる差はありません。)
        var DeltaDamageTurn = Math.Abs(nowTurn - TempDamageTurn);

        //今回食らった以外の全てのスキルの記憶回数をターン数経過によって減らすーーーーーーーーーーーーーーーーーーーーーーーーーーーーー
        var templist = FocusSkillImpressionList.Where(fo => fo != NowFocusSkillImpression).ToList();//元リストを破壊しないフィルタコピー
        foreach (var fo in templist)
        {
            //まず優先順位を取得し、グループ序列(スキルの最終優先ランク)を取得
            var finalSkillRank = AdaptToSkillsGrouping(AdaptPriorityDamageToSkill(fo.skillImpression));

            //DEFによる基礎減少値を取得
            var b_ReductionValue = GetBaseMemoryReducationValue();

            //DEFによる固定値と優先順位を計算して、どのくらい減るか　
            //優先順位が低ければ低いほど、つまりfinalSkillRankが多ければ多いほど、記憶回数が減りやすい(だからそのまま計算できる)
            var DeathMemoryFloat = 0f;//記憶忘却回数

            var rankNotTopModify = 0f;//二位以降での補正
            if (finalSkillRank > 0) rankNotTopModify = 0.08f;//優先順位が一軍でないのなら、序列補正に加算される固定値
            var PriorityModify = 1 + finalSkillRank / 8 + rankNotTopModify;//序列補正

            //計算　記憶忘却回数 = 序列補正×基礎減少値×経過ターン　
            //そのスキルの記憶回数の序列の割合/(3～2)により、 乱数判定成功したら、　　記憶忘却回数 /= 3　

            DeathMemoryFloat = PriorityModify * b_ReductionValue * DeltaDamageTurn;


            //記憶回数の序列割合を入手
            var MemoryRankRatio = GetMemoryCountRankRatio(AdaptPriorityMemoryToSkill(fo.skillImpression));
            var mod1 = RandomEx.Shared.NextFloat(2, 4);//2～3
            var rat1 = MemoryRankRatio / mod1;
            if (RandomEx.Shared.NextFloat(1f) < rat1)//乱数判定　成功したら。
            {
                DeathMemoryFloat /= 3;//3分の一に減衰される
            }


            fo.Forget(DeathMemoryFloat);//減る数だけ減る

        }


        //記憶回数による記憶範囲の判定と慣れ補正の計算☆ーーーーーーーーーーーーーーーーーーーーーーーーー

        //スキル印象の記憶回数での並べ替え
        //記憶回数が多い方から数えて、　　"今回のスキル印象"がそれに入ってるなら慣れ補正を返す
        //数える範囲は　記憶範囲
        FocusSkillImpressionList = FocusSkillImpressionList.OrderByDescending(skill => skill.MemoryCount(PersistentAdaptSkillImpressionMemories)).ToList();

        //記憶段階と範囲の取得　　
        var rl = MemoryStageStructure();

        //二回目以降で記憶範囲にあるのなら、補正計算して返す
        if (!donthaveskillImpression)
        {
            var loopMax = Mathf.Min(rl.Count, FocusSkillImpressionList.Count); // 範囲外防止
            for (var i = 0; i < loopMax; i++)//記憶段階と範囲のサイズ分ループ
            {
                var fo = FocusSkillImpressionList[i];
                if (fo.skillImpression == skill.Impression)//もし記憶範囲に今回のスキル印象があるならば
                {
                    //もしスキルを使う行使者が初見なら(二人目以降の使用者)
                    //精神属性によっては戸惑って補正はない　　戸惑いフラグが立つ
                    if (IsFirstAttacker)
                    {
                        switch (MyImpression)
                        {//ドレミス　ゴッドティア　キンダー　シークイエストは戸惑わない
                            case SpiritualProperty.doremis:
                                IsConfused = false; break;
                            case SpiritualProperty.godtier:
                                IsConfused = false; break;
                            case SpiritualProperty.kindergarden:
                                IsConfused = false; break;
                            case SpiritualProperty.cquiest:
                                IsConfused = false; break;
                            default:
                                IsConfused = true; break;//それ以外で初見人なら戸惑う
                        }
                    }

                    if (!IsConfused)//戸惑ってなければ、補正がかかる。(デフォルト値の-1でなくなる。)
                    {
                        var BaseValue = GetBaseAdaptValue();//基礎量
                        var MemoryValue = Mathf.Floor(fo.MemoryCount(PersistentAdaptSkillImpressionMemories));//記憶回数(小数点以下切り捨て)

                        float MemoryPriority = -1;//記憶段階による補正
                        switch (rl[i])
                        {
                            case MemoryDensity.Low:
                                MemoryPriority = 1.42f;
                                break;
                            case MemoryDensity.Medium:
                                MemoryPriority = 3.75f;
                                break;
                            case MemoryDensity.High:
                                MemoryPriority = 10f;
                                break;
                        }

                        //一回計算
                        AdaptModify = 1 - (BaseValue * MemoryValue * MemoryPriority);

                        // ランダムファクターの生成
                        float randomFactor = RandomEx.Shared.NextFloat(1.0f - randomVariationRange, 1.0f + randomVariationRange);
                        AdaptModify *= randomFactor;

                        //下限しきい値の設定
                        var Threshold = CalculateEYEBasedAdaptThreshold();

                        //もしデフォルトの下限しきい値を慣れ補正が下回っていたら
                        if (initialAdaptThreshold > AdaptModify)
                        {
                            var chance = (int)(777 - b_EYE.Total * 5);//b_eyeの0~150 0.1~3.7%推移　 以降は5.2%
                            chance = Mathf.Max(19, chance);

                            if (RandomEx.Shared.NextInt(chance) == 0)//瞬きが起きる機会
                            {
                                AdaptModify = EyeBlink(AdaptModify, maxEyeAdaptThreshold, initialAdaptThreshold, 2.111f);
                            }
                        }


                        //もし最終的な慣れの補正量がしきい値を下回っていた場合、しきい値に固定される
                        if (Threshold > AdaptModify)
                        {
                            AdaptModify = Threshold;
                        }
                    }

                    //"慣れ減衰"の計算に使用☆

                    //fo.MemoryCount  //記憶回数の数(切り下げ、小数点以下切り捨て)
                    //rl[i]  //精神属性による段階
                    //EYEによる基礎量

                    break;//スキルを見つけ処理を終えたので、記憶範囲ループから外れる
                }
            }

            TempDamageTurn = nowTurn;//今回の被害ターンを記録する。
        }


        //今回食らったスキルの記憶回数を増やす処理----------------------------------------------------------------------------------------------------------------
        if (!IsConfused)//戸惑いが立ってると記憶回数は増加しない
        {//FocuseSkillはコンストラクタでMemory()されないため、donthaveSkillに関わらず、実行されます。

            //今回食らったスキルの記憶回数を増やすーーーーーーーーーーーーーーーーーーーーーーーーーーー☆
            var finalSkillRank1 = AdaptToSkillsGrouping(AdaptPriorityDamageToSkill(NowFocusSkillImpression.skillImpression));//優先順位取得
                                                                                                         //基礎上昇値取得
                                                                                                         //DEFによる基礎上昇値を取得
            var b_IncreaseValue = GetBaseMemoryIncreaseValue();

            // 優先順位による補正　値は変更されます。
            // (例)：一軍は2.0倍、下位になるほど0.9倍ずつ減らす
            // rank=0で2.0, rank=1で1.8, rank=2で1.62 ...など
            float priorityBaseGain = 2.2f * Mathf.Pow(0.77f, finalSkillRank1);

            //一軍なら微々たる追加補正
            float rankTopIncreaseModify = finalSkillRank1 == 0 ? 0.05f : 0f;//一軍ならば、左の値が優先順位補正に加算
            float PriorityIncreaseModify = priorityBaseGain + rankTopIncreaseModify;

            // 攻撃を受けてからの経過ターンが少ないほどターンボーナス(掛け算)が増す（
            float TurnBonus = 1.0f;//デフォルト値
            if (DeltaDamageTurn < 5) TurnBonus += 0.1f;//4ターン以内
            if (DeltaDamageTurn < 4) TurnBonus += 0.45f;//3ターン以内
            if (DeltaDamageTurn < 3) TurnBonus += 0.7f;//2ターン以内

            //記憶回数による微加算　(これは掛けるのではなく最終計算結果に加算する¥)
            float MemoryAdjust = 0.08f * NowFocusSkillImpression.MemoryCount(PersistentAdaptSkillImpressionMemories);

            // 最終的な増加量計算
            // メモリ増加例: (基礎上昇値 * 優先順位補正 * 記憶割合補正 + ターン補正)
            float MemoryIncrease = b_IncreaseValue * PriorityIncreaseModify * TurnBonus + MemoryAdjust;

            //注目スキルとして記憶回数が増える。
            NowFocusSkillImpression.Memory(MemoryIncrease);
        }

        //慣れ補正がデフォルト値の-1のままだった場合、1.0として返す。
        if (AdaptModify < 0) AdaptModify = 1.0f;

        return AdaptModify;
    }

    /// <summary>
    /// 恒常的な慣れ補正のリスト
    /// </summary>
    Dictionary<SkillImpression,float>PersistentAdaptSkillImpressionMemories = new();
    const float MEMORY_COUNT_TOTAL_THRESHOLD = 47.7f;//持ち越し発生のの総記憶回数のしきい値
    const float MEMORY_COUNT_TOP_THRESHOLD = 22.45f;//持ち越し発生のトップ記憶された慣れスキル印象の記憶回数のしきい値
    const float NARE_TOP_PORTION_RATIO = 0.3f;//記憶回数でソートしたときの上位を何割取るか ％
    const float NARE_DOMINANCE_THRESHOLD = 0.66f;//特定上位のスキル印象が総記憶回数の何割以上を占めたら「突出」判定するか ％

    const float NARE_CARRYOVER_DECAY_RATIO = 0.3f;//恒常的な慣れ補正の戦闘終了時の減衰する際に使用する全員スキル印象の記憶量と掛ける割合

    const float NARE_CARRYOVER_PRESERVATION_THRESHOLD = 0.24f;//指定の印象が、記憶回数全体のこの割合(0.24 = 24%)以上を占めていれば、減算を免除される



    /// <summary>
    /// 特定の条件で慣れ補正の慣れ量を持ち越す処理
    /// </summary>
    void AdaptCarryOver()
    {
        if(b_EYE < 20) return;//眼力が20未満ならば、慣れ補正を持ち越さない

        //まずfocusSkillListの全てのスキル印象の記憶回数がしきい値を超えてるか計算する
        var AllMemoryCount = FocusSkillImpressionList.Sum(fo => fo.MemoryCount(PersistentAdaptSkillImpressionMemories));
        if (AllMemoryCount < MEMORY_COUNT_TOTAL_THRESHOLD) return;//総記憶回数のしきい値を超えていないなら、持ち越し処理は行わない

        //一番多い記憶回数を持つスキル印象を取得
        var TopMemoryCount = FocusSkillImpressionList.Max(fo => fo.MemoryCount(PersistentAdaptSkillImpressionMemories));
        //トップ記憶回数のしきい値を超えていないなら、持ち越し処理は行わない
        if(TopMemoryCount < MEMORY_COUNT_TOP_THRESHOLD) return;
        
        //念のため同じ記憶回数だった場合、その中から抽選するようにする。
        var TopMemoryCountSkillImpressions = FocusSkillImpressionList.Where(fo => fo.MemoryCount(PersistentAdaptSkillImpressionMemories) == TopMemoryCount).ToList();//トップ記憶回数と同じ物をリストに
        var TopMemorySkillImpression = RandomEx.Shared.GetItem(TopMemoryCountSkillImpressions.ToArray());//GetItemでランダムに一つ入手

        //もし記憶回数の分布が突出した形なら、倍率1.5倍
        var MemoryMountainBoost = 1.0f;
        int count = (int)Mathf.Ceil(FocusSkillImpressionList.Count * NARE_TOP_PORTION_RATIO);//上位何割を取るか
        FocusSkillImpressionList = FocusSkillImpressionList.OrderByDescending(fo => fo.MemoryCount(PersistentAdaptSkillImpressionMemories)).ToList();//記憶回数でソート
        var TopPortion = FocusSkillImpressionList.Take(count).ToList();//上位何割を取るか
        float TopPortionSum = TopPortion.Sum(fo => fo.MemoryCount(PersistentAdaptSkillImpressionMemories));//上位何割の合計
        if (TopPortionSum >= NARE_DOMINANCE_THRESHOLD * AllMemoryCount) MemoryMountainBoost = 1.5f;//上位何割の合計が総記憶回数の何割以上を占めたら倍率1.5倍

        var CarryOverMultplier = GetMemoryCarryOverMultiplierByDefaulSpiritualPropertyAndHumanCondition();//持ち越し倍率を取得

        //もし記憶回数の総量が持ち越し発生の総量しきい値の二倍より多いなら、持ち越し倍率が0.8倍の最低保証が付く。
        if (AllMemoryCount > MEMORY_COUNT_TOTAL_THRESHOLD * 2) CarryOverMultplier = Mathf.Max(0.8f, CarryOverMultplier);

        //恒常的な慣れ補正のリストに記録
        var PersistentMemoryValue = TopMemoryCount * CarryOverMultplier * MemoryMountainBoost;
        if(PersistentAdaptSkillImpressionMemories.ContainsKey(TopMemorySkillImpression.skillImpression))
        {
            PersistentAdaptSkillImpressionMemories[TopMemorySkillImpression.skillImpression] += PersistentMemoryValue;
        }
        else
        {
            PersistentAdaptSkillImpressionMemories.Add(TopMemorySkillImpression.skillImpression, PersistentMemoryValue);
        }
    }

    float GetMemoryCarryOverMultiplierByDefaulSpiritualPropertyAndHumanCondition()
    {
        switch(NowCondition)
        {
            case HumanConditionCircumstances.Painful:
                if(DefaultImpression == SpiritualProperty.devil) return 0.8f;
                return 0.75f;
            case HumanConditionCircumstances.Optimistic:
                return 0.95f;
            case HumanConditionCircumstances.Elated:
                if(DefaultImpression == SpiritualProperty.baledrival) return 0.91f;
                return 0.7f;
            case HumanConditionCircumstances.Resolved:
                return 1.0f;
            case HumanConditionCircumstances.Angry:
                if(DefaultImpression == SpiritualProperty.doremis) return 1.2f;
                return 0.6f;
            case HumanConditionCircumstances.Doubtful:
                if(DefaultImpression == SpiritualProperty.pillar) return 0.9f;
                if(DefaultImpression == SpiritualProperty.cquiest) return 1.1f;
                return 0.75f;
            case HumanConditionCircumstances.Confused:
                return 0.5f;
            case HumanConditionCircumstances.Normal:
                return 0.9f;
            default:
                return 0.7f;//念のためのデフォルト値
        }
    }
    /// <summary>
    /// 恒常的な慣れ補正の減衰処理
    /// </summary>
    void DecayOfPersistentAdaptation()
    {
        var AllMemoryCount = FocusSkillImpressionList.Sum(fo => fo.MemoryCount(PersistentAdaptSkillImpressionMemories));//総記憶回数を取得
        
        // いったん辞書のキーをまとめてリスト化
        var keys = PersistentAdaptSkillImpressionMemories.Keys.ToList();

        //全ての恒常的な慣れ補正のスキル印象で回す
        foreach(var key in keys)
        {
            //現在のスキル印象の記憶回数の値を取得
            float currentValue = PersistentAdaptSkillImpressionMemories[key];

            //記憶回数の総量の特定の割合以上を該当のスキル印象が占めている場合、減算を免除する
            if(currentValue >= AllMemoryCount * NARE_CARRYOVER_PRESERVATION_THRESHOLD) continue;
            
            // 新しい値を計算
            float newValue = currentValue - (AllMemoryCount * NARE_CARRYOVER_DECAY_RATIO);//減算処理

            // 上書き保存 (キーは同じ、値だけ更新)
            if(newValue <= 0)PersistentAdaptSkillImpressionMemories.Remove(key);//0以下なら削除
            else
            {
                PersistentAdaptSkillImpressionMemories[key] = newValue;
            }
        }

        
    }

    /// <summary>
    /// 慣れ補正（慣れ記憶）のデバッグ用サマリ文字列を返す。
    /// - 印象ごとの記憶回数（永続分込み/床関数値と実数値）
    /// - 最大被ダメージ
    /// - 使用者数
    /// - 合計記憶量と永続記憶の詳細
    /// - しきい値（初期・EYE基準・maxEye）
    /// </summary>
    public string GetAdaptationDebugText(int topN = 8)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            // しきい値
            float eyeThreshold = CalculateEYEBasedAdaptThreshold();
            sb.AppendLine($"慣れ下限: 初期={initialAdaptThreshold:0.###}, EYE={eyeThreshold:0.###}, 下限={maxEyeAdaptThreshold:0.###}");

            if (FocusSkillImpressionList == null || FocusSkillImpressionList.Count == 0)
            {
                sb.Append("記憶なし");
                return sb.ToString();
            }

            // 精神属性の段階配列と基礎量を取得（推定軽減率計算用）
            var rl = MemoryStageStructure();
            float baseV = GetBaseAdaptValue();

            // まず記憶回数で降順に並べて「段階インデックス」を決める（副作用回避）
            var preOrdered = FocusSkillImpressionList
                .Select(fo => new
                {
                    Impression = fo.skillImpression,
                    MemFloor = Mathf.Floor(fo.MemoryCount(PersistentAdaptSkillImpressionMemories)),
                    MemRaw = fo.MemoryCount(PersistentAdaptSkillImpressionMemories),
                    TopDmg = fo.TopDmg,
                    Users = (fo.User != null) ? fo.User.Count : 0
                })
                .OrderByDescending(x => x.MemFloor)
                .ThenByDescending(x => x.TopDmg)
                .ToList();

            // 段階・推定慣れ補正(Adapt)・推定軽減率を付与
            var enriched = preOrdered
                .Select((x, idx) =>
                {
                    bool inRange = idx < rl.Count;
                    string stageLabel = "範囲外";
                    float prio = 0f;
                    if (inRange)
                    {
                        switch (rl[idx])
                        {
                            case MemoryDensity.Low:
                                stageLabel = "Low"; prio = 1.42f; break;
                            case MemoryDensity.Medium:
                                stageLabel = "Medium"; prio = 3.75f; break;
                            case MemoryDensity.High:
                                stageLabel = "High"; prio = 10f; break;
                        }
                    }

                    float estAdapt = 1f;
                    if (prio > 0f)
                    {
                        float mem = x.MemFloor;
                        float tmp = 1f - (baseV * mem * prio);
                        if (tmp < eyeThreshold) tmp = eyeThreshold; // EYE下限でクランプ
                        estAdapt = tmp;
                    }
                    float reduction = Mathf.Clamp01(1f - estAdapt); // 軽減率（大きいほどダメ軽減が強い）

                    return new
                    {
                        x.Impression,
                        x.MemFloor,
                        x.MemRaw,
                        x.TopDmg,
                        x.Users,
                        Stage = stageLabel,
                        EstAdapt = estAdapt,
                        Reduction = reduction
                    };
                })
                .ToList();

            // 推定軽減率の大きい順に並び替え。タイブレークは記憶量→最大DMG
            var ordered = enriched
                .OrderByDescending(x => x.Reduction)
                .ThenByDescending(x => x.MemFloor)
                .ThenByDescending(x => x.TopDmg)
                .ToList();

            int limit = Mathf.Min(topN, ordered.Count);
            for (int i = 0; i < limit; i++)
            {
                var x = ordered[i];
                sb.AppendLine($"{i + 1}. {x.Impression} 記憶={x.MemFloor:0.##}({x.MemRaw:0.##}) 最大DMG={x.TopDmg:0.#} 使用者数={x.Users} 推定軽減={(x.Reduction * 100f):0.#}% 段階={x.Stage}");
            }

            // 合計記憶量
            float totalMem = FocusSkillImpressionList.Sum(fo => fo.MemoryCount(PersistentAdaptSkillImpressionMemories));
            sb.AppendLine($"総記憶={totalMem:0.##}");

            // 永続記憶の詳細
            if (PersistentAdaptSkillImpressionMemories != null && PersistentAdaptSkillImpressionMemories.Count > 0)
            {
                var pairs = PersistentAdaptSkillImpressionMemories
                    .Select(kv => $"{kv.Key}:{kv.Value:0.##}");
                sb.AppendLine("永続: " + string.Join(", ", pairs));
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Adaptation Debug Error: {ex.Message}";
        }
    }
    
    /// <summary>
    /// StatesBanner向けの簡易版：現在の慣れ（推定軽減率大→小）を
    /// "Impression:xx.x%" のコンマ区切りで返す。しきい値行や番号付与は行わない。
    /// GetAdaptationDebugText() と同じ決定論的ロジックを使用。
    /// </summary>
    public string GetAdaptationBannerText(int topN = 8)
    {
        try
        {
            if (FocusSkillImpressionList == null || FocusSkillImpressionList.Count == 0)
            {
                return "なし";
            }

            var rl = MemoryStageStructure();
            float baseV = GetBaseAdaptValue();
            float eyeThreshold = CalculateEYEBasedAdaptThreshold();

            var preOrdered = FocusSkillImpressionList
                .Select(fo => new
                {
                    Impression = fo.skillImpression,
                    MemFloor = Mathf.Floor(fo.MemoryCount(PersistentAdaptSkillImpressionMemories)),
                    MemRaw = fo.MemoryCount(PersistentAdaptSkillImpressionMemories),
                    TopDmg = fo.TopDmg,
                    Users = (fo.User != null) ? fo.User.Count : 0
                })
                .OrderByDescending(x => x.MemFloor)
                .ThenByDescending(x => x.TopDmg)
                .ToList();

            var enriched = preOrdered
                .Select((x, idx) =>
                {
                    float prio = 0f;
                    if (idx < rl.Count)
                    {
                        switch (rl[idx])
                        {
                            case MemoryDensity.Low:    prio = 1.42f; break;
                            case MemoryDensity.Medium: prio = 3.75f; break;
                            case MemoryDensity.High:   prio = 10f;   break;
                        }
                    }

                    float estAdapt = 1f;
                    if (prio > 0f)
                    {
                        float mem = x.MemFloor;
                        float tmp = 1f - (baseV * mem * prio);
                        if (tmp < eyeThreshold) tmp = eyeThreshold;
                        estAdapt = tmp;
                    }
                    float reduction = Mathf.Clamp01(1f - estAdapt);

                    return new { x.Impression, x.MemFloor, x.TopDmg, Reduction = reduction };
                })
                .OrderByDescending(y => y.Reduction)
                .ThenByDescending(y => y.MemFloor)
                .ThenByDescending(y => y.TopDmg)
                .ToList();

            int limit = Mathf.Min(topN, enriched.Count);
            var partsList = enriched
                .Take(limit)
                .Select(y => $"{y.Impression}:{(y.Reduction * 100f):0.#}%")
                .ToList();

            if (enriched.Count > limit && partsList.Count > 0)
            {
                int lastIndex = partsList.Count - 1;
                partsList[lastIndex] = partsList[lastIndex] + "・・・・";
            }
            return string.Join(", ", partsList);
        }
        catch (Exception ex)
        {
            return $"Adapt Banner Error: {ex.Message}";
        }
    }

    //static 静的なメゾット(戦いに関する辞書データなど)---------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// 精神属性と十日能力の互換表
    /// デフォルト精神属性
    /// </summary>
    public static readonly Dictionary<SpiritualProperty, List<TenDayAbility>> SpritualTenDayAbilitysMap = 
    new()
    {
        {SpiritualProperty.doremis, new List<TenDayAbility>(){TenDayAbility.Enokunagi, TenDayAbility.PersonaDivergence, TenDayAbility.KereKere, TenDayAbility.Rain, TenDayAbility.BlazingFire}},
        {SpiritualProperty.pillar, new List<TenDayAbility>(){TenDayAbility.JoeTeeth, TenDayAbility.Sort, TenDayAbility.SilentTraining, TenDayAbility.Leisure, TenDayAbility.Vond}},
        {SpiritualProperty.kindergarden, new List<TenDayAbility>(){TenDayAbility.dokumamusi, TenDayAbility.Baka, TenDayAbility.TentVoid, TenDayAbility.SpringNap, TenDayAbility.WaterThunderNerve}},
        {SpiritualProperty.liminalwhitetile, new List<TenDayAbility>(){TenDayAbility.FlameBreathingWife, TenDayAbility.NightDarkness, TenDayAbility.StarTersi, TenDayAbility.FaceToHand, TenDayAbility.Pilmagreatifull}},
        {SpiritualProperty.sacrifaith, new List<TenDayAbility>(){TenDayAbility.UnextinguishedPath, TenDayAbility.Miza, TenDayAbility.JoeTeeth, TenDayAbility.SpringNap}},
        {SpiritualProperty.cquiest, new List<TenDayAbility>(){TenDayAbility.ColdHeartedCalm, TenDayAbility.NightDarkness, TenDayAbility.NightInkKnight, TenDayAbility.Glory, TenDayAbility.SpringNap}},
        {SpiritualProperty.pysco, new List<TenDayAbility>(){TenDayAbility.Raincoat, TenDayAbility.TentVoid, TenDayAbility.Blades, TenDayAbility.Smiler, TenDayAbility.StarTersi}},
        {SpiritualProperty.godtier, new List<TenDayAbility>(){TenDayAbility.HeavenAndEndWar, TenDayAbility.Vail, TenDayAbility.BlazingFire, TenDayAbility.FlameBreathingWife, TenDayAbility.SpringWater}},
        {SpiritualProperty.baledrival, new List<TenDayAbility>(){TenDayAbility.Smiler, TenDayAbility.Miza, TenDayAbility.HeatHaze, TenDayAbility.Vail}},
        {SpiritualProperty.devil, new List<TenDayAbility>(){TenDayAbility.CryoniteQuality, TenDayAbility.HumanKiller, TenDayAbility.HeatHaze, TenDayAbility.FaceToHand}},
    };

    /// <summary>
    /// 戦闘規格ごとのデフォルトa,bの狙い流れ
    /// </summary>
    public static readonly Dictionary<BattleProtocol, (AimStyle aStyle,float a,AimStyle bStyle)> DefaultDefensePatternPerProtocol =
        new ()
        {
            {BattleProtocol.LowKey,(AimStyle.AcrobatMinor,0.6f,AimStyle.Doublet)},
            {BattleProtocol.Tricky,(AimStyle.Duster,0.9f,AimStyle.QuadStrike)},
            {BattleProtocol.Showey,(AimStyle.CentralHeavenStrike,0.8f,AimStyle.Doublet)}
        };


    /// <summary>
    /// 指定したAimStyleを除いた中からランダムに1つ選択する
    /// </summary>
    private AimStyle GetRandomAimStyleExcept(AimStyle excludeStyle)
    {
        // 全てのAimStyleの値を配列として取得
        var allStyles = Enum.GetValues(typeof(AimStyle))
                        .Cast<AimStyle>()
                        .Where(style => style != excludeStyle)
                        .ToArray();
        
        // ランダムに1つ選択して返す
        return RandomEx.Shared.GetItem(allStyles);
    }
    /// <summary>
    /// 攻撃者の狙い流れ(Aimstyle)、受け手の"現在の変更前の"防ぎ方(Aimstyle)の組み合わせによって受け手の防ぎ方変更までの最大ターン数を算出する辞書。
    /// 【その狙い流れを受けて、現在の防ぎ方がそのAimStyleに対応するまでのカウント】であって、決して前の防ぎ方から今回の防ぎ方への変化ではない。(複雑なニュアンスの違い)
    /// </summary>
    public static readonly Dictionary<(AimStyle attackerAIM, AimStyle nowDefenderAIM), int> DefenseTransformationThresholds =  
    new Dictionary<(AimStyle attackerAIM, AimStyle defenderAIM), int>()
    {
    { (AimStyle.AcrobatMinor, AimStyle.Doublet), 2 },         // アクロバマイナ体術1 ← ダブレット
    { (AimStyle.AcrobatMinor, AimStyle.QuadStrike), 6 },      // アクロバマイナ体術1 ← 四弾差し込み
    { (AimStyle.AcrobatMinor, AimStyle.Duster), 4 },          // アクロバマイナ体術1 ← ダスター
    { (AimStyle.AcrobatMinor, AimStyle.PotanuVolf), 7 },      // アクロバマイナ体術1 ← ポタヌヴォルフのほうき術系
    { (AimStyle.AcrobatMinor, AimStyle.CentralHeavenStrike), 4 }, // アクロバマイナ体術1 ← 中天一弾
    { (AimStyle.Doublet, AimStyle.AcrobatMinor), 3 },         // ダブレット ← アクロバマイナ体術1
    { (AimStyle.Doublet, AimStyle.QuadStrike), 6 },           // ダブレット ← 四弾差し込み
    { (AimStyle.Doublet, AimStyle.Duster), 8 },               // ダブレット ← ダスター
    { (AimStyle.Doublet, AimStyle.PotanuVolf), 4 },           // ダブレット ← ポタヌヴォルフのほうき術系
    { (AimStyle.Doublet, AimStyle.CentralHeavenStrike), 7 },  // ダブレット ← 中天一弾
    { (AimStyle.QuadStrike, AimStyle.AcrobatMinor), 4 },      // 四弾差し込み ← アクロバマイナ体術1
    { (AimStyle.QuadStrike, AimStyle.Doublet), 2 },           // 四弾差し込み ← ダブレット
    { (AimStyle.QuadStrike, AimStyle.Duster), 5 },            // 四弾差し込み ← ダスター
    { (AimStyle.QuadStrike, AimStyle.PotanuVolf), 6 },        // 四弾差し込み ← ポタヌヴォルフのほうき術系
    { (AimStyle.QuadStrike, AimStyle.CentralHeavenStrike), 4 }, // 四弾差し込み ← 中天一弾
    { (AimStyle.Duster, AimStyle.AcrobatMinor), 3 },          // ダスター ← アクロバマイナ体術1
    { (AimStyle.Duster, AimStyle.Doublet), 8 },               // ダスター ← ダブレット
    { (AimStyle.Duster, AimStyle.QuadStrike), 4 },            // ダスター ← 四弾差し込み
    { (AimStyle.Duster, AimStyle.PotanuVolf), 7 },            // ダスター ← ポタヌヴォルフのほうき術系
    { (AimStyle.Duster, AimStyle.CentralHeavenStrike), 5 },   // ダスター ← 中天一弾
    { (AimStyle.PotanuVolf, AimStyle.AcrobatMinor), 2 },      // ポタヌヴォルフのほうき術系 ← アクロバマイナ体術1
    { (AimStyle.PotanuVolf, AimStyle.Doublet), 3 },           // ポタヌヴォルフのほうき術系 ← ダブレット
    { (AimStyle.PotanuVolf, AimStyle.QuadStrike), 5 },        // ポタヌヴォルフのほうき術系 ← 四弾差し込み
    { (AimStyle.PotanuVolf, AimStyle.Duster), 4 },            // ポタヌヴォルフのほうき術系 ← ダスター
    { (AimStyle.PotanuVolf, AimStyle.CentralHeavenStrike), 5 }, // ポタヌヴォルフのほうき術系 ← 中天一弾
    { (AimStyle.CentralHeavenStrike, AimStyle.AcrobatMinor), 4 }, // 中天一弾 ← アクロバマイナ体術1
    { (AimStyle.CentralHeavenStrike, AimStyle.Doublet), 3 },      // 中天一弾 ← ダブレット
    { (AimStyle.CentralHeavenStrike, AimStyle.QuadStrike), 6 },   // 中天一弾 ← 四弾差し込み
    { (AimStyle.CentralHeavenStrike, AimStyle.Duster), 8 },       // 中天一弾 ← ダスター
    { (AimStyle.CentralHeavenStrike, AimStyle.PotanuVolf), 2 }    // 中天一弾 ← ポタヌヴォルフのほうき術系
    };

    /// <summary>
    /// 精神属性でのスキルの補正値　攻撃者の精神属性→キャラクター属性 csvDataからロード
    /// </summary>
    protected static Dictionary<(SpiritualProperty atk, SpiritualProperty def), FixedOrRandomValue> SpiritualModifier;
    /// <summary>
    /// スキル以外のキャラクター同士の攻撃的なニュアンスの精神補正
    /// </summary>
    public static FixedOrRandomValue GetOffensiveSpiritualModifier(BaseStates attacker,BaseStates Defer)
    {

        if(attacker.MyImpression == SpiritualProperty.none) return new FixedOrRandomValue(100);//noneなら補正なし(100%なので無変動)
        
        var resultModifier = SpiritualModifier[(attacker.MyImpression, Defer.MyImpression)];//攻撃的な人の放つ精神属性と被害者の精神属性による補正
        
        if(attacker.MyImpression == attacker.MyImpression)
        {
            resultModifier.RandomMaxPlus(RandomEx.Shared.NextInt(2,8));//スキルの計算でないのでほんのちょっとだけ。
        }

        return resultModifier;
    }
    /// <summary>
    /// セルの文字列を整数にパースする。空または無効な場合はデフォルト値を返す。
    /// </summary>
    /// <param name="cell">セルの文字列</param>
    /// <returns>パースされた整数値またはデフォルト値</returns>
    private static int ParseCell(string cell)
    {
        if (int.TryParse(cell, out int result))
        {
            return result;
        }//空セルの場合は整数変換に失敗してelseが入る　splitで,,みたいに区切り文字が二連続すると""空文字列が入る
        else return -1;  //空セルには-1が入る　空セルが入るのはrndMaxが入る所のみになってるはずなので、最大値が無効になる-1が入る
    }
    /// <summary>
    /// BaseStatus内で使われるデータ用のcsvファイルをロード
    /// </summary>
    public async static void CsvLoad()
    {
        SpiritualModifier = new Dictionary<(SpiritualProperty, SpiritualProperty), FixedOrRandomValue>();//初期化
        var csvFile = "Assets/csvData/SpiritualMatchData.csv";

        var textHandle = await Addressables.LoadAssetAsync<TextAsset>(csvFile);


        var rows = textHandle.text //そのままテキストを渡す
            .Split("\n")//改行ごとに分割
                        //.Select(line => line.Trim())//行の先頭と末尾の空白や改行を削除する
            .Select(line => line.Split(',').Select(ParseCell).ToArray()) //それをさらにカンマで分割してint型に変換して配列に格納する。
            .ToArray(); //配列になった行をさらに配列に格納する。
        /*
         * new List<List<int>> {  実際はarrayだけどこういうイメージ
            new List<int> { 50, 20, 44, 53, 42, 37, 90, 100, 90, 50 },
            new List<int> { 60, 77, 160, 50, 80, 23, 32, 50, 51, 56 }}
         */

        var SpiritualCsvArrayRows = new[]
        {
            //精神攻撃の相性の　行の属性並び順
            SpiritualProperty.liminalwhitetile,
            SpiritualProperty.kindergarden,
            SpiritualProperty.sacrifaith,
            SpiritualProperty.cquiest,
            SpiritualProperty.devil,
            SpiritualProperty.devil,//乱数のmax
            SpiritualProperty.doremis,
            SpiritualProperty.pillar,
            SpiritualProperty.godtier,
            SpiritualProperty.baledrival,
            SpiritualProperty.pysco
        };
        var SpiritualCsvArrayColumn = new[]
        {
            //精神攻撃の相性の　列の属性並び順
            SpiritualProperty.liminalwhitetile,
            SpiritualProperty.kindergarden,
            SpiritualProperty.sacrifaith,
            SpiritualProperty.cquiest,
            SpiritualProperty.devil,
            SpiritualProperty.doremis,
            SpiritualProperty.pillar,
            SpiritualProperty.godtier,
            SpiritualProperty.baledrival,
            SpiritualProperty.baledrival,//乱数のmax
            SpiritualProperty.pysco
        };


        for (var i = 0; i < rows.Length; i++) //行ごとに回していく oneは行たちを格納した配列
        {
            //4行目と5行目はdevilへの乱数min,max

            //min 部分でクラスを生成　max部分で既にあるクラスにmaxをセット　空なら-1
            //つまり乱数のmaxにあたる行でのみSetmaxが既にある辞書に実行されるという仕組みにすればいいのだ楽だラクダ
            for (var j = 0; j < rows[i].Length; j++) //数字ごとに回す　one[j]は行の中の数字を格納した配列
            {
                //8,9列目はbaleが相手に対する乱数min,max

                var key = (SpiritualCsvArrayColumn[j], SpiritualCsvArrayRows[i]);
                var value = rows[i][j];
                if (i == 5 || j == 9)//もし五行目、または九列目の場合
                {
                    if (SpiritualModifier.ContainsKey(key))//キーが既にあれば
                    {
                        SpiritualModifier[key].SetMax(value);//乱数最大値を設定
                        //Debug.Log($"乱数セット{value}");
                    }
                    else
                    {
                        Debug.LogError($"キー {key} が存在しません。SetMax を実行できません。");
                    }
                    //既にある辞書データの乱数単一の値のクラスに最大値をセット
                }
                else
                {
                    //固定値としてクラスを生成 (生成時にrndMaxに初期値-1が入るよ)
                    if (!SpiritualModifier.ContainsKey(key))//キーが存在していなければ
                    {
                        SpiritualModifier.Add(key, new FixedOrRandomValue(value));//キーを追加
                    }
                    else
                    {
                        Debug.LogWarning($"キー {key} は既に存在しています。追加をスキップします。");
                    }

                }


            }
        }


        /*Debug.Log("読み込まれたキャラクター精神スキル補正値\n" +
              string.Join(", ",
                  SkillSpiritualModifier.Select(kvp => $"[{kvp.Key}: {kvp.Value.GetValue()} rndMax({kvp.Value.rndMax})]" + "\n")); //デバックで全内容羅列。*/
    }
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

        // 属性ポイント（混合上限 + DropNew）のディープコピー
        // - _attrPMap: 属性別の現在量
        // - _attrPTotal: 合計量
        // - _attrAddHistory: 追加履歴（最新から削るために保持）
        dst._attrPMap.Clear();
        foreach (var kv in _attrPMap)
        {
            dst._attrPMap[kv.Key] = kv.Value;
        }
        dst._attrPTotal = _attrPTotal;
        dst._attrAddHistory.Clear();
        dst._attrAddHistory.AddRange(_attrAddHistory);

        if(dst.DefaultImpression == 0)
        {
            //Debug.LogError("DefaultImpressionが0です、敵はディープコピー時デフォルトの精神属性が入ります。");
        }
        //Debug.Log($"{CharacterName}のDefaultImpression:{dst.DefaultImpression}");

        //Debug.Log(CharacterName + "のBaseStatesディープコピー完了");
        //パワーは初期値　medium allyは歩行で変化　enemyは再遭遇時コールバックで一回だけ歩行変化で判別
        
    }

    
}

/// <summary>
/// 固定値か最大値、最小値に応じた乱数のどっちかを返すクラス
/// 精神補正用の割合を出すためのクラスなので　割合はintで0~100の百分率で指定します
/// </summary>
public class FixedOrRandomValue
{

    private int rndMax;//乱数の最大値 乱数かどうかはrndMaxに-1を入れればいい
    private int rndMinOrFixed;//単一の値または乱数としての最小値

    /// <summary>
    /// 乱数の上振れを増やす
    /// </summary>
    public void RandomMaxPlus(int plusAmount)
    {
        if(rndMax == -1)//乱数最大範囲がないなら
        {
            rndMax = rndMinOrFixed + plusAmount;//上振れを付けた状態で増やす
            return;
        }

        rndMax += plusAmount;//既にあるならそのまま最大値を増やす
    }

    /// <summary>
    /// クラス生成
    /// </summary>
    /// <param name="isRnd">乱数として保持するかどうか</param>
    /// <param name="minOrFixed">最小値または単一の値として</param>
    /// <param name="max">省略可能、乱数なら最大値</param>
    public FixedOrRandomValue(int minOrFixed)
    {
        rndMinOrFixed = minOrFixed;//まず最小値またはデフォルトありきでクラスを作成
        rndMax = -1;//予め無を表す-1で初期化
    }

    /// <summary>
    /// -1を指定すると乱数ではないってことになる。
    /// </summary>
    public void SetMax(int value)
    {
        rndMax = value;//-1を指定するとないってこと
    }
    public float GetValue(float percentage = 1.0f)
    {
        float value;
        if (rndMax == -1) value = rndMinOrFixed / 100.0f;//乱数じゃないなら単一の値が返る
        else value = RandomEx.Shared.NextInt(rndMinOrFixed, rndMax + 1)  / 100.0f;//ランダムなら

        return value * percentage;//割合を掛ける

    }
    public FixedOrRandomValue DeepCopy()
    {
        var newData = new FixedOrRandomValue(rndMinOrFixed);
        newData.SetMax(rndMax);
        return newData;
    }
}
/// <summary>
/// 慣れ補正で使用するスキルとその使用者
/// </summary>
public class FocusedSkillImpressionAndUser
{
    public FocusedSkillImpressionAndUser(BaseStates InitUser, SkillImpression askil, float InitDmg)
    {
        User = new List<BaseStates>();
        User.Add(InitUser);
        skillImpression = askil;

        //Memory();//この記憶回数の処理の後に補正するので、作った瞬間はゼロから始めた方がいい
        DamageMemory(InitDmg);
    }

    /// <summary>
    /// そのスキルのユーザー
    /// </summary>
    public List<BaseStates> User;

    /// <summary>
    /// 保存スキル
    /// </summary>
    public SkillImpression skillImpression;

    float _memoryCount;
    /// <summary>
    /// 慣れの記憶回数
    /// </summary>
    public float MemoryCount(Dictionary<SkillImpression, float> PersistentMemories)
    {
        if (PersistentMemories.ContainsKey(skillImpression))
        {
            return _memoryCount + PersistentMemories[skillImpression];
        }
        else
        {
            return _memoryCount;
        }
    }
    public void Memory(float value)
    {
        _memoryCount += value;
    }
    public void Forget(float value)
    {
        _memoryCount -= value;
    }


    float _topDmg;
    /// <summary>
    /// このスキルが自らに施した最大限のダメージ
    /// </summary>
    public float TopDmg => _topDmg;
    public void DamageMemory(float dmg)
    {
        if (dmg > _topDmg) _topDmg = dmg;//越してたら記録
    }
}

/// <summary>
/// 狙い流れ(AimStyle)に対する短期記憶・対応進行度をまとめた構造体
/// </summary>
public struct AimStyleMemory
{
    /// <summary>いま対応しようとしている相手の AimStyle==そのまま自分のNowDeffenceStyleに代入されます。</summary>
    public AimStyle? TargetAimStyle;

    /// <summary>現在の変革カウント(対応がどこまで進んでいるか)</summary>
    public int TransformCount;

    /// <summary>変革カウントの最大値。ここに達したら対応完了</summary>
    public int TransformCountMax;

    


}
/// <summary>
/// 対象者ボーナスのデータ
/// 相性値用の仕組みで汎用性は低い　　威力の1.〇倍ボーナス　を対象者限定で、尚且つ持続ターンありきのもの。
/// 割り込みカウンターのシングル単体攻撃とも違うぞ！
/// </summary>
public class TargetBonusDatas
{
    /// <summary>
    /// 持続ターン
    /// </summary>
    List<int> DurationTurns { get; set; }
    /// <summary>
    /// スキルのパワーボーナス倍率
    /// </summary>
    List<float> PowerBonusPercentages { get; set; }
    /// <summary>
    /// 対象者
    /// </summary>
    List<BaseStates> Targets { get; set; }
    /// <summary>
    /// 対象者がボーナスに含まれているか
    /// </summary>
    public bool DoIHaveTargetBonus(BaseStates target)
    {
        return Targets.Contains(target);
    }
    /// <summary>
    /// 渡されたリストの中に対象者が含まれているかどうか。
    /// 含まれていたらその対象者のリストのインデックスを返す。
    /// </summary>
    public int DoIHaveTargetBonusAny_ReturnListIndex(List<BaseStates> targets)
    {
        return Targets.FindIndex(x => targets.Contains(x));
    }
    
    /// <summary>
    /// 対象者のインデックスを取得
    /// </summary>
    public int GetTargetIndex(BaseStates target)
    {
        return Targets.FindIndex(x => x == target);
    }
    /// <summary>
    /// 対象者ボーナスが発動しているか
    /// </summary>
    //public List<bool> IsTriggered { get; set; }     ーーーーーーーーーーーー一回自動で発動するようにするから消す、明確に対象者ボーナスの適用を手動にするなら解除
    /// <summary>
    /// 発動してるかどうかを取得
    /// </summary>
    /*public bool GetAtIsTriggered(int index)
    {
        return IsTriggered[index];
    }*/
    /// <summary>
    /// 対象者ボーナスの持続ターンを取得
    /// </summary>
    public int GetAtDurationTurns(int index)
    {
        return DurationTurns[index];
    }
    /// <summary>
    /// 全てのボーナスをデクリメントと自動削除の処理
    /// </summary>
    public void AllDecrementDurationTurn()
    {
        for (int i = 0; i < DurationTurns.Count; i++)
        {
            DecrementDurationTurn(i);
        }
    }
    /// <summary>
    /// 持続ターンをデクリメントし、0以下になったら削除する。全ての対象者ボーナスを削除する。
    /// </summary>
    void DecrementDurationTurn(int index)
    {
        DurationTurns[index]--;
        if (DurationTurns[index] <= 0)
        {
            DurationTurns.RemoveAt(index);
            PowerBonusPercentages.RemoveAt(index);
            Targets.RemoveAt(index);
        }
    }
    /// <summary>
    /// 対象者ボーナスのパワーボーナス倍率を取得
    /// </summary>
    public float GetAtPowerBonusPercentage(int index)
    {
        return PowerBonusPercentages[index];
    }
    /// <summary>
    /// 対象者ボーナスの対象者を取得
    /// </summary>
    public BaseStates GetAtTargets(int index)
    {
        return Targets[index];
    }

    public TargetBonusDatas()
    {
        DurationTurns =  new();
        PowerBonusPercentages = new();
        Targets = new();
        //IsTriggered = new();
    }

    public void Add(int duration, float powerBonusPercentage, BaseStates target)
    {
        //targetの重複確認
        if (Targets.Contains(target))
        {
            int index = Targets.IndexOf(target);//同じインデックスの物をすべて消す
            DurationTurns.RemoveAt(index);
            PowerBonusPercentages.RemoveAt(index);
            Targets.RemoveAt(index);
            //IsTriggered.RemoveAt(index);
            return;
        }

        //追加
        DurationTurns.Add(duration);
        PowerBonusPercentages.Add(powerBonusPercentage);
        Targets.Add(target);
        //IsTriggered.Add(false);
    }
    /// <summary>
    /// 全削除
    /// </summary>
    public void AllClear()
    {
        DurationTurns.Clear();
        PowerBonusPercentages.Clear();
        Targets.Clear();
        //IsTriggered.Clear();
    }
    /// <summary>
    /// 該当のインデックスのボーナスを削除
    /// </summary>
    public void BonusClear(int index)
    {
        DurationTurns.RemoveAt(index);
        PowerBonusPercentages.RemoveAt(index);
        Targets.RemoveAt(index);
        //IsTriggered.RemoveAt(index);
    }
}
/// <summary>
///     キャラクター達の種別
/// </summary>
[Flags]
public enum CharacterType
{
    TLOA = 1 << 0,
    Machine = 1 << 1,
    Life = 1 << 2 //TLOAそのもの、機械、生命
}
/// <summary>
/// スキルの行動記録　リストで記録する
/// 一人一人に対するものってニュアンス
/// </summary>
public class ACTSkillDataForOneTarget
{
    public bool IsDone;
    /// <summary>
    /// 攻撃が乱れたかどうか
    /// </summary>
    public bool IsDisturbed;
    public bool IsHit;
    public BaseSkill Skill;
    public BaseStates Target;   
    public ACTSkillDataForOneTarget(bool isdone, bool isdisturbed, BaseSkill skill, BaseStates target, bool ishit)
    {
        IsDone = isdone;
        Skill = skill;
        Target = target;
        IsHit = ishit;
        IsDisturbed = isdisturbed;
    }
}
public class ActionSkillData
{
    /// <summary>
    /// 実行したスキルが乖離しているかどうか
    /// </summary>
    public bool IsDivergence;
    public BaseSkill Skill;
    public ActionSkillData(bool isdivergence, BaseSkill skill)
    {
        IsDivergence = isdivergence;
        Skill = skill;
    }
}
/// <summary>
/// 被害記録
/// </summary>
public class DamageData
{
    public BaseStates Attacker;
    /// <summary>
    /// 攻撃自体がヒットしたかどうかで、atktypeなら攻撃で全部ひっくるめてあたるから
    /// atktypeじゃないなら、falseで
    /// </summary>
    public bool IsAtkHit;
    public bool IsBadPassiveHit;
    public bool IsBadPassiveRemove;
    public bool IsGoodPassiveHit;
    public bool IsGoodPassiveRemove;


    public bool IsGoodVitalLayerHit;
    public bool IsGoodVitalLayerRemove;
    public bool IsBadVitalLayerHit;
    public bool IsBadVitalLayerRemove;

    public bool IsGoodSkillPassiveHit;
    public bool IsGoodSkillPassiveRemove;
    public bool IsBadSkillPassiveHit;
    public bool IsBadSkillPassiveRemove;


    /// <summary>
    /// 死回復も含める
    /// </summary>
    public bool IsHeal;
    //public bool IsConsecutive;　これは必要なし、なぜなら相性値の判断は毎ターン行われるから、連続ならちゃんと連続毎ターンで結果的に多く相性値関連の処理は加算される。
    public float Damage;
    public float Heal;
    //public BasePassive whatPassive;  多いしまだ必要ないから一旦コメントアウト
    //public int DamagePercent　最大HPはBaseStates側にあるからそっちから取得する
    public BaseSkill Skill;
    public DamageData(bool isAtkHit,bool isBadPassiveHit,bool isBadPassiveRemove,bool isGoodPassiveHit,bool isGoodPassiveRemove,
    bool isGoodVitalLayerHit,bool isGoodVitalLayerRemove,bool isBadVitalLayerHit,bool isBadVitalLayerRemove,
    bool isGoodSkillPassiveHit,bool isGoodSkillPassiveRemove,bool isBadSkillPassiveHit,bool isBadSkillPassiveRemove,
    bool isHeal,BaseSkill skill,float damage,float heal,BaseStates attacker)
    {
        IsAtkHit = isAtkHit;
        IsBadPassiveHit = isBadPassiveHit;
        IsBadPassiveRemove = isBadPassiveRemove;
        IsGoodPassiveHit = isGoodPassiveHit;
        IsGoodPassiveRemove = isGoodPassiveRemove;
        IsGoodVitalLayerHit = isGoodVitalLayerHit;
        IsGoodVitalLayerRemove = isGoodVitalLayerRemove;
        IsBadVitalLayerHit = isBadVitalLayerHit;
        IsBadVitalLayerRemove = isBadVitalLayerRemove;
        IsGoodSkillPassiveHit = isGoodSkillPassiveHit;
        IsGoodSkillPassiveRemove = isGoodSkillPassiveRemove;
        IsBadSkillPassiveHit = isBadSkillPassiveHit;
        IsBadSkillPassiveRemove = isBadSkillPassiveRemove;
        IsHeal = isHeal;

        Skill = skill;
        Damage = damage;
        Heal = heal;
        Attacker = attacker;
    }
}

/// <summary>
///物理属性、スキルに依存し、キャラクター達の種別や個人との相性で攻撃の通りが変わる
/// </summary>
public enum PhysicalProperty
{
    heavy,
    volten,
    dishSmack //床ずれ、ヴぉ流転、暴断
    ,none
}
/// <summary>
/// 人間状況　全員持つけど例えばLife以外なんかは固定されてたりしたりする。
/// </summary>
public enum HumanConditionCircumstances
{
    /// <summary>
    /// 辛い状態を表します。
    /// </summary>
    Painful,
    /// <summary>
    /// 楽観的な状態を表します。
    /// </summary>
    Optimistic,
    /// <summary>
    /// 高揚した状態を表します。
    /// </summary>
    Elated,
    /// <summary>
    /// 覚悟を決めた状態を表します。
    /// </summary>
    Resolved,
    /// <summary>
    /// 怒りの状態を表します。
    /// </summary>
    Angry,
    /// <summary>
    /// 状況への疑念を抱いている状態を表します。
    /// </summary>
    Doubtful,
    /// <summary>
    /// 混乱した状態を表します。
    /// </summary>
    Confused,
    /// <summary>
    /// 普段の状態を表します。
    /// </summary>
    Normal
    }
/// <summary>
/// パワー、元気、気力値　歩行やその他イベントなどで短期的に上げ下げし、
/// 狙い流れ等の防ぎ方切り替え処理などで、さらに上下する値として導入されたりする。
/// ThePowerExtensionsで日本語に変更可能
/// </summary>
public enum ThePower
{
        /// <summary>たるい</summary>
    lowlow,
        /// <summary>低い</summary>
    low,
    /// <summary>普通</summary>
    medium,
    /// <summary>高い</summary>
    high
}
/// <summary>
/// 武器依存の戦闘規格
/// </summary>
public enum BattleProtocol
{
    /// <summary>地味</summary>
    LowKey,
    /// <summary>トライキー</summary>
    Tricky,
    /// <summary>派手</summary>
    Showey,
    /// <summary>
    /// この戦闘規格には狙い流れ(AimStyle)がないため、には防ぎ方(AimStyleごとに対応される防御排他ステ)もなく、追加攻撃力(戦闘規格による排他ステ)もない
    /// </summary>
    none
}
/// <summary>
/// 防ぎ方 狙い流れとも言う　戦闘規格とスキルにセットアップされる順番や、b_defの対応に使用される。
/// </summary>
public enum AimStyle
{

     /// <summary>
    /// アクロバマイナ体術1 - Acrobat Minor Technique 1
    /// </summary>
    AcrobatMinor,       // アクロバマイナ体術1

    /// <summary>
    /// ダブレット - Doublet
    /// </summary>
    Doublet,            // ダブレット

    /// <summary>
    /// 四弾差し込み - Quad Strike Insertion
    /// </summary>
    QuadStrike,         // 四弾差し込み

    /// <summary>
    /// ダスター - Duster
    /// </summary>
    Duster,             // ダスター

    /// <summary>
    /// ポタヌヴォルフのほうき術系 - Potanu Volf's Broom Technique
    /// </summary>
    PotanuVolf,         // ポタヌヴォルフのほうき術系

    /// <summary>
    /// 中天一弾 - Central Heaven Strike
    /// </summary>
    CentralHeavenStrike, // 中天一弾

    /// <summary>
    /// 戦闘規格のnoneに対して変化する防ぎ方
    /// </summary>
    none
}
/// <summary>
/// あるキャラクターにのみ効く一時補正
/// </summary>
public class CharacterConditionalModifier
{
    public BaseStates Target { get; }
    public ModifierPart Part  { get; }

    public CharacterConditionalModifier(BaseStates target, ModifierPart part)
    {
        Target = target;
        Part   = part;
    }
}
/// <summary>
/// 命中率、攻撃力、回避力、防御力への補正
/// </summary>
public class ModifierPart
{
    /// <summary>
    /// どういう補正かを保存する　攻撃時にunderに出てくる
    /// </summary>
    public string memo;

    public whatModify whatStates;

    /// <summary>
    /// trueならfixed、falseならpercent
    /// </summary>
    public bool IsFixedOrPercent;

    /// <summary>
    /// 補正率 
    /// </summary>
    public float Modifier;

    /// <summary>
    /// 固定値補正値　十日能力の内訳を含む
    /// </summary>
    public StatesPowerBreakdown FixedModifier;


    public ModifierPart(string memo, whatModify whatStates, float value, StatesPowerBreakdown fixedModifier = null, bool isFixedOrPercent = false)
    {
        this.memo = memo;
        Modifier = value;
        this.whatStates = whatStates;
        IsFixedOrPercent = isFixedOrPercent;
        FixedModifier = fixedModifier;
    }
}
/// <summary>
/// 命中結果の種類を表す列挙型
/// </summary>
public enum HitResult
{
    /// <summary>完全回避（ノーダメージ）</summary>
    CompleteEvade,
    
    /// <summary>かすり（割合少ないダメージ）</summary>
    Graze,
    
    /// <summary>通常ヒット</summary>
    Hit,
    
    /// <summary>クリティカルヒット（大ダメージ）</summary>
    Critical,
    /// <summary>結果ナシ</summary>
    none
}
/// <summary>
///     精神属性、スキル、キャラクターに依存し、キャラクターは直前に使った物が適用される
    ///     だから精神属性同士で攻撃の通りは設定される。
    /// </summary>
[Flags]
public enum SpiritualProperty
{
    doremis = 1 << 0,   // ビットパターン: 0000 0001  (1)
    pillar = 1 << 1,   // ビットパターン: 0000 0010  (2)
    kindergarden = 1 << 2,   // ビットパターン: 0000 0100  (4)
    liminalwhitetile = 1 << 3,   // ビットパターン: 0000 1000  (8)
    sacrifaith = 1 << 4,   // ビットパターン: 0001 0000  (16)
    cquiest = 1 << 5,   // ビットパターン: 0010 0000  (32)
    pysco = 1 << 6,   // ビットパターン: 0100 0000  (64)
    godtier = 1 << 7,   // ビットパターン: 1000 0000  (128)
    baledrival = 1 << 8,   // ビットパターン: 0001 0000 0000  (256)
    devil = 1 << 9,    // ビットパターン: 0010 0000 0000  (512)
    none = 1 << 10,    // ビットパターン: 0100 0000 0000  (1024)
    mvoid = 1 << 11,
    Galvanize = 1 << 12,
    air = 1 << 13,
    memento = 1 << 14
}

public enum MemoryDensity
{
    /// <summary>
    /// 薄い
    /// </summary>
    Low,
    /// <summary>
    /// 普通
    /// </summary>
    Medium,
    /// <summary>
    /// しっかりと
    /// </summary>
    High,
}

/// <summary>
/// 分解に対応した十日能力と非十日能力の内訳を持つ四大ステ保持クラス
/// </summary>
public class StatesPowerBreakdown
{
    /// <summary>
    /// 「TenDayAbilityからくる四大ステの内訳」
    /// </summary>
    public TenDayAbilityDictionary TenDayBreakdown { get; set; }

    /// <summary>
    /// 「非TenDayAbility要素 (固定値 等)」
    /// </summary>
    public float NonTenDayPart { get; set; }

    /// <summary>
    /// コンストラクタ
    /// 十日能力と非十日能力の初期値
    /// </summary>
    public StatesPowerBreakdown(TenDayAbilityDictionary tenDayBreakdown, float nonTenDayPart)
    {
        TenDayBreakdown = tenDayBreakdown;
        NonTenDayPart = nonTenDayPart;
    }

    /// <summary>
    /// 合計値を出すプロパティ
    /// </summary>
    public float Total
        => TenDayBreakdown.Values.Sum() + NonTenDayPart;
    public float TenDayValuesSum => TenDayBreakdown.Values.Sum();
    /// <summary>
    /// 十日能力追加
    /// </summary>
    public void TenDayAdd(TenDayAbility tenDayAbility, float value)
    {
        if (!TenDayBreakdown.ContainsKey(tenDayAbility))
        {
            TenDayBreakdown.Add(tenDayAbility, 0);
        }
        TenDayBreakdown[tenDayAbility] += value;
    }
    /// <summary>
    /// 非十日能力追加
    /// </summary>
    public void NonTenDayAdd(float value)
    {
        NonTenDayPart += value;
    }
    /// <summary>
    /// 該当の十日能力ステータス値を入手
    /// </summary>
    public float GetTenDayValue(TenDayAbility tenDayAbility)
    {
        return TenDayBreakdown.GetValueOrZero(tenDayAbility);
    }

    /// <summary>
    /// StatesPowerBreakdown同士の除算演算子
    /// 結果の合計値が left.Total / right.Total と同じになるように各値をスケーリングします
    /// </summary>
    public static StatesPowerBreakdown operator /(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        // ゼロ除算対策
        if (right.Total == 0)
        {
            // 右辺の合計が0の場合は左辺をそのまま返す
            return new StatesPowerBreakdown(
                new TenDayAbilityDictionary(left.TenDayBreakdown),
                left.NonTenDayPart);
        }
        
        // スケーリング係数を計算 (left.Total / right.Total)
        float scaleFactor = left.Total / right.Total;
        
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary(left.TenDayBreakdown);
        
        // 全ての値をスケーリング係数で乗算
        foreach (var key in newTenDayBreakdown.Keys.ToList())
        {
            newTenDayBreakdown[key] *= scaleFactor;
        }
        
        // 非十日能力部分も同様にスケーリング
        float newNonTenDayPart = left.NonTenDayPart * scaleFactor;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }

    /// <summary>
    /// 乗算演算子のオーバーロード - スカラー値による乗算
    /// 乗算補正で十日能力などが使われてたとしてもすべてに対するブーストなので、
    /// どんな十日能力に補正され乗算補正されたかの情報は持たないし、全ての値に乗算する。
    /// </summary>
    public static StatesPowerBreakdown operator *(StatesPowerBreakdown breakdown, float multiplier)
    {
        // 新しいDictionaryを作成（元のを変更しないため）
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        // すべての十日能力の寄与値に乗算を適用
        foreach (var entry in breakdown.TenDayBreakdown)
        {
            newTenDayBreakdown[entry.Key] = entry.Value * multiplier;
        }
        
        // 非十日能力部分にも乗算を適用
        float newNonTenDayPart = breakdown.NonTenDayPart * multiplier;
        
        // 新しいStatesPowerBreakdownオブジェクトを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }
    /// <summary>
    /// float値とStatesPowerBreakdownの乗算演算子（交換法則対応）
    /// 各十日能力値と非十日能力値に同じfloat値を乗算します
    /// </summary>
    public static StatesPowerBreakdown operator *(float multiplier, StatesPowerBreakdown breakdown)
    {
        // 既存の StatesPowerBreakdown * float 演算子を利用（交換法則）
        return breakdown * multiplier;
    }
    /// <summary>
    /// 除算補正で十日能力などが使われてたとしてもすべてに対するブーストなので、
    /// どんな十日能力に補正され除算補正されたかの情報は持たないし、全ての値に除算する。
    /// </summary>
    public static StatesPowerBreakdown operator /(StatesPowerBreakdown breakdown, float divisor)
    {
        if (divisor == 0)
        {
            // ゼロ除算の場合はそのまま返す
            return breakdown;
        }
        
        // 新しい十日能力値の内訳を作成
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        // 各十日能力値を割る
        foreach (var entry in breakdown.TenDayBreakdown)
        {
            newTenDayBreakdown[entry.Key] = entry.Value / divisor;
        }
        
        // 非十日能力値も割る
        float newNonTenDayPart = breakdown.NonTenDayPart / divisor;
        
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }

    /// <summary>
    /// float値でStatesPowerBreakdownを除算する演算子
    /// 結果の合計値が float / breakdown.Total と同じになるように各値をスケーリングします
    /// </summary>
    public static StatesPowerBreakdown operator /(float left, StatesPowerBreakdown right)
    {
        // ゼロ除算対策
        if (right.Total == 0)
        {
            // 右辺の合計が0の場合は、ゼロに近い小さな値を使用して除算を続行
            return new StatesPowerBreakdown(new TenDayAbilityDictionary(),left);
        }
        
        // スケーリング係数を計算 (left / right.Total)
        float scaleFactor = left / right.Total;
        
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        // 右辺の全ての十日能力値に対して、その逆数にスケーリング係数を掛ける
        foreach (var entry in right.TenDayBreakdown)
        {
            // 元の値との比率を反転させつつスケーリング
            newTenDayBreakdown[entry.Key] = entry.Value / right.Total * left;
        }
        
        // 非十日能力部分も同様にスケーリング
        float newNonTenDayPart = right.NonTenDayPart / right.Total * left;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }





    /// <summary>
    /// StatesPowerBreakdown同士の加算演算子
    /// 十日能力ごとに対応する値を加算します
    /// </summary>
    public static StatesPowerBreakdown operator +(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary(left.TenDayBreakdown);
        
        // 右辺の十日能力値を加算
        foreach (var entry in right.TenDayBreakdown)
        {
            if (newTenDayBreakdown.ContainsKey(entry.Key))
            {
                newTenDayBreakdown[entry.Key] += entry.Value;
            }
            else
            {
                newTenDayBreakdown[entry.Key] = entry.Value;
            }
        }
        
        // 非十日能力部分も加算
        float newNonTenDayPart = left.NonTenDayPart + right.NonTenDayPart;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }
    /// <summary>
    /// StatesPowerBreakdown同士の減算演算子
    /// 十日能力ごとに対応する値を減算します
    /// </summary>
    public static StatesPowerBreakdown operator -(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary(left.TenDayBreakdown);
        
        // 右辺の十日能力値を減算
        foreach (var entry in right.TenDayBreakdown)
        {
            if (newTenDayBreakdown.ContainsKey(entry.Key))
            {
                newTenDayBreakdown[entry.Key] -= entry.Value;
            }
            else
            {
                newTenDayBreakdown[entry.Key] = -entry.Value;
            }
        }
        
        // 非十日能力部分も減算
        float newNonTenDayPart = left.NonTenDayPart - right.NonTenDayPart;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }

    /// <summary>
    /// スカラー値による加算演算子
    /// 非十日能力部分に加算されます
    /// </summary>
    public static StatesPowerBreakdown operator +(StatesPowerBreakdown breakdown, float value)
    {
        // 新しいオブジェクトを作成（十日能力部分はコピー）
        var newBreakdown = new StatesPowerBreakdown(
            new TenDayAbilityDictionary(breakdown.TenDayBreakdown), 
            breakdown.NonTenDayPart);
        
        // 加算は非十日能力部分に適用
        newBreakdown.NonTenDayPart += value;
        
        return newBreakdown;
    }
    /// <summary>
    /// スカラー値による減算演算子
    /// 非十日能力部分に減算されます
    /// </summary>
    public static StatesPowerBreakdown operator -(StatesPowerBreakdown breakdown, float value)
    {
        // 新しいオブジェクトを作成（十日能力部分はコピー）
        var newBreakdown = new StatesPowerBreakdown(
            new TenDayAbilityDictionary(breakdown.TenDayBreakdown), 
            breakdown.NonTenDayPart);
        
        // 減算は非十日能力部分に適用
        newBreakdown.NonTenDayPart -= value;
        
        return newBreakdown;
    }
    /// <summary>
    /// float値からStatesPowerBreakdownを引くための演算子
    /// 非十日能力部分に適用されます
    /// </summary>
    public static StatesPowerBreakdown operator -(float left, StatesPowerBreakdown right)
    {
        // 新しいStatesPowerBreakdownを作成
        // 十日能力の内訳は反転して保持（マイナス値にする）
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        foreach (var entry in right.TenDayBreakdown)
        {
            newTenDayBreakdown[entry.Key] = -entry.Value;
        }
        
        // 非十日能力部分はfloat値から引く
        float newNonTenDayPart = left - right.NonTenDayPart;
        
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }
    /// <summary>
    /// 合計値が0未満になる場合は、非十日能力値を調整して0以上になるようにする
    /// 十日能力値は変更しない
    /// </summary>
    public void ClampToZero()
    {
        // 合計値を計算
        float total = Total;
        
        if (total < 0)
        {
            // 十日能力値の合計を計算
            float tenDayTotal = TenDayBreakdown.Values.Sum();
            
            // 非十日能力値を調整して、全体が0になるようにする
            // 非十日能力値が負の場合はそのまま0にせず、全体の合計が0になるよう調整
            if (tenDayTotal > 0)
            {
                // 十日能力値が正なら、合計が0になるよう非十日能力値を調整
                NonTenDayPart = -tenDayTotal;
            }
            else
            {
                // 十日能力値も負または0なら、非十日能力値を0にする
                NonTenDayPart = 0;
            }
        }
    }
    /// <summary>
    /// 大なり演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator >(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return left.Total > right.Total;
    }

    /// <summary>
    /// 小なり演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator <(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return left.Total < right.Total;
    }

    /// <summary>
    /// 以上演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator >=(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return left.Total >= right.Total;
    }

    /// <summary>
    /// 以下演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator <=(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return left.Total <= right.Total;
    }

    /// <summary>
    /// 等価演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator ==(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        if (ReferenceEquals(left, null))
            return ReferenceEquals(right, null);
        return left.Equals(right);
    }

    /// <summary>
    /// 非等価演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator !=(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return !(left == right);
    }
    /// <summary>
    /// 単項マイナス演算子 - 全ての値の符号を反転
    /// </summary>
    public static StatesPowerBreakdown operator -(StatesPowerBreakdown value)
    {
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        // 各十日能力値の符号を反転
        foreach (var entry in value.TenDayBreakdown)
        {
            newTenDayBreakdown[entry.Key] = -entry.Value;
        }
        
        // 非十日能力部分も符号を反転
        float newNonTenDayPart = -value.NonTenDayPart;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }

    /// <summary>
    /// オブジェクトの等価性を判定する - 全ての十日能力値と非十日能力値を比較
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is StatesPowerBreakdown other)
        {
            // 1. 非十日能力値の比較
            if (this.NonTenDayPart != other.NonTenDayPart)
                return false;
                
            // 2. 十日能力値の数が同じか確認
            if (this.TenDayBreakdown.Count != other.TenDayBreakdown.Count)
                return false;
                
            // 3. すべての十日能力値を比較
            foreach (var entry in this.TenDayBreakdown)
            {
                // キーが存在するか確認
                if (!other.TenDayBreakdown.TryGetValue(entry.Key, out float otherValue))
                    return false;
                    
                // 値が一致するか確認
                if (entry.Value != otherValue)
                    return false;
            }
            
            // すべての比較に合格したら等価
            return true;
        }
        return false;
    }

    /// <summary>
    /// ハッシュコードを取得する - 全ての値を考慮
    /// </summary>
    public override int GetHashCode()
    {
        unchecked // オーバーフローを許可
        {
            int hash = 17;
            hash = hash * 23 + NonTenDayPart.GetHashCode();
            
            foreach (var entry in TenDayBreakdown)
            {
                hash = hash * 23 + entry.Key.GetHashCode();
                hash = hash * 23 + entry.Value.GetHashCode();
            }
            
            return hash;
        }
    }
    /// <summary>
    /// StatesPowerBreakdownとfloatの大小比較（小なり）
    /// </summary>
    public static bool operator <=(StatesPowerBreakdown left, float right)
    {
        return left.Total <= right;
    }

    /// <summary>
    /// StatesPowerBreakdownとfloatの大小比較（大なり）
    /// </summary>
    public static bool operator >=(StatesPowerBreakdown left, float right)
    {
        return left.Total >= right;
    }

    /// <summary>
    /// StatesPowerBreakdownとfloatの大小比較（小なり）
    /// </summary>
    public static bool operator <(StatesPowerBreakdown left, float right)
    {
        return left.Total < right;
    }

    /// <summary>
    /// StatesPowerBreakdownとfloatの大小比較（大なり）
    /// </summary>
    public static bool operator >(StatesPowerBreakdown left, float right)
    {
        return left.Total > right;
    }
}
/// <summary>
/// 敵対的行動のヒット結果を返す構造体
/// </summary>
public struct HostileEffectResult
{
    public bool BadPassiveHit;
    public bool BadVitalLayerHit;
    public bool GoodPassiveRemove;
    public bool GoodVitalLayerRemove;
    public bool BadSkillPassiveHit;
    public bool GoodSkillPassiveRemove;

    public HostileEffectResult(
        bool badPassiveHit,
        bool badVitalLayerHit,
        bool goodPassiveRemove,
        bool goodVitalLayerRemove,
        bool badSkillPassiveHit,
        bool goodSkillPassiveRemove)
    {
        BadPassiveHit        = badPassiveHit;
        BadVitalLayerHit     = badVitalLayerHit;
        GoodPassiveRemove    = goodPassiveRemove;
        GoodVitalLayerRemove = goodVitalLayerRemove;
        BadSkillPassiveHit   = badSkillPassiveHit;
        GoodSkillPassiveRemove = goodSkillPassiveRemove;
    }
}
