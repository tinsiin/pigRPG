using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using NRandom.Linq;
using Cysharp.Threading.Tasks;
using System.Linq;


//キャラクターのパッシブ管理
public abstract partial class BaseStates    
{
    //  ==============================================================================================================================
    //                                              基本のフィールド
    //  ==============================================================================================================================

    /// <summary>
    /// インスペクタからいじれないように、パッシブのmanagerから来たものがbaseStatesに保存されるpassive保存用
    /// </summary>
    List<BasePassive> _passiveList = new();
    public List<BasePassive> Passives => _passiveList;
    [Header("初期所持のパッシブのIDリスト")]
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

    //  ==============================================================================================================================
    //                                              パッシブ汎用所持確認
    //  ==============================================================================================================================


    /// <summary>
    /// unityのインスペクタ上で設定したPassiveのIDからキャラが持ってるか調べる。
    /// </summary>
    public bool HasPassive(int id)
    {
        return _passiveList.Any(pas => pas.ID == id);
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


    //  ==============================================================================================================================
    //                                              バッファ追加関数(戦闘中)
    //  ==============================================================================================================================

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
    //  ==============================================================================================================================
    //                                              パッシブ適用関数
    //  ==============================================================================================================================
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
    /* ---------------------------------
     * パッシブ由来のスキルにパッシブを宿らせる
     * --------------------------------- 
     */

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


    //  ==============================================================================================================================
    //                                              パッシブ除去関数
    //  ==============================================================================================================================
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

    //  ==============================================================================================================================
    //                                              パッシブ処理関数群
    //  ==============================================================================================================================

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



}
