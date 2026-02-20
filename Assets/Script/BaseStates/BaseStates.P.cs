using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;


//ポインントは巨大なので全てここに
public abstract partial class BaseStates    
{

    /// <summary>
    /// 最大ポイントは実HPの最大値を定数で割ったもの。　この定数はHPのスケールの変更などに応じて、適宜調整する
    /// </summary>
    public int MAXP
    {
        get
        {
            var factor = Tuning?.HpToMaxPConversionFactor ?? 80;
            if (factor <= 0) factor = 80;
            return (int)_maxhp / factor;
        }
    }

    [Header("ポイント(P)")]
    [SerializeField]
    [Tooltip("現在のポイント(P)の実体。プロパティPで0〜MAXPにクランプされる")] 
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
        var VeryLowMinus = 3;
        if (rollper(37))VeryLowMinus = 0;
        P = NowPower switch
        {
            PowerLevel.VeryLow => 3 - VeryLowMinus,
            PowerLevel.Low => (int)(MAXP * 0.15),
            PowerLevel.Medium => (int)(MAXP * 0.5),
            PowerLevel.High => (int)(MAXP * 0.7),
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
    // 属性ポイントモジュール（遅延初期化）。BaseStates の公開APIから内部的に委譲する。
    private AttrPointModule _attrPoints;
    private AttrPointModule AttrP
    {
        get
        {
            if (_attrPoints == null)
            {
                _attrPoints = new AttrPointModule(this);
            }
            return _attrPoints;
        }
    }

    /// <summary>
    /// 属性ポインント
    /// </summary>
    public AttrPointModule AttrPoints => AttrP;
    
    /// <summary>
    /// 属性ポイント総上限（混合最大値）。MAXP にパワー倍率を乗じて決定。
    /// </summary>
    public int CombinedAttrPMax
    {
        get
        {
            return AttrP.CombinedAttrPMax;
        }
    }
    
    /// <summary>
    /// 現在の属性ポイント総量（混合合計）。0..CombinedAttrPMax。
    /// </summary>
    public int CombinedAttrPTotal => AttrP.CombinedAttrPTotal;
    public event Action<SpiritualProperty, int> OnAttrPChanged
    {
        add { AttrP.OnAttrPChanged += value; }
        remove { AttrP.OnAttrPChanged -= value; }
    }
    

    // バッチ期間の開始/終了 (入れ子可)
    public IDisposable BeginAttrPBatch()
    {
        // 互換APIは残しつつ、実体は AttrPointModule のバッチ管理へ移譲
        return AttrP.BeginAttrPBatch();
    }

    /// <summary>
    /// 追加可能な残容量（DropNew ポリシーで使用）。
    /// </summary>
    public int CombinedAttrPRemaining => AttrP.CombinedAttrPRemaining;
    
    // 旧内部ストレージ・履歴は AttrPointModule 側へ完全移行済み
    
    /// <summary>
    /// 現在の属性ポイント（該当属性が未登録なら0）。
    /// </summary>
    public int GetAttrP(SpiritualProperty attr)
    {
        return AttrP.GetAttrP(attr);
    }
    
    /// <summary>
    /// 属性ポイントの追加（DropNew）：
    /// CombinedAttrPMax に達している場合、または残容量が0の場合は追加できない。
    /// 実際に追加できた量を返す。
    /// </summary>
    public int TryAddAttrP(SpiritualProperty attr, int amount)
    {
        return AttrP.TryAddAttrP(attr, amount);
    }

    // 旧 ReduceLatest* ヘルパは不要になったため削除（AttrPointModule 側に移行）
    
    /// <summary>
    /// 属性ポイントの消費。該当属性のプールからのみ消費（他属性の補填はしない）。
    /// 成功時 true、残量不足で失敗時 false。
    /// 履歴は「同属性の最新から（LIFO）」で減らし、上限縮小時の DropNew ポリシー
    /// （最新から削る）と整合が取れるようにする。
    /// </summary>
    public bool TrySpendAttrP(SpiritualProperty attr, int cost)
    {
        return AttrP.TrySpendAttrP(attr, cost);
    }
    
    /// <summary>
    /// スキルに設定されたノーマルPと属性Pを「一括でアトミックに」消費する。
    /// 事前に全コストを再チェックし、次に属性→最後にノーマルの順で清算する。
    /// 途中で失敗した場合は、既に消費した属性Pをロールバックして false を返す。
    /// </summary>
    public bool TryConsumeForSkillAtomic(BaseSkill skill)
    {
        return AttrP.TryConsumeForSkillAtomic(skill);
    }
    
    /// <summary>
    /// 属性ポイントを全消去。
    /// </summary>
    public void ClearAllAttrP()
    {
        AttrP.ClearAllAttrP();
    }
    
    /// <summary>
    /// 上限が縮小した場合などに、総量が CombinedAttrPMax を超えているとき余剰を削る。
    /// DropNew ポリシーに従い、モジュール内部の追加履歴を基準に
    /// 「直近に追加された順（最新から）」で横断的（全属性）に減らす。
    /// 履歴・属性マップ・合計値の整合は AttrPointModule 側で保証される。
    /// </summary>
    public void ReclampAttrPToCapDropNew()
    {
        AttrP.ReclampAttrPToCapDropNew();
    }

    // Invariantチェックは AttrPointModule 側で実施

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
        var src = AttrP.GetSnapshotByAmount(sortDesc);
        var list = new List<AttrPSnapshotEntry>();
        foreach (var s in src)
        {
            list.Add(new AttrPSnapshotEntry
            {
                Attr = s.Attr,
                Amount = s.Amount,
                RatioOfTotal = s.RatioOfTotal,
                RatioOfMax = s.RatioOfMax
            });
        }
        return list;
    }
    
    /// <summary>
    /// UI表示用に属性ポイント内訳を「最近追加された属性が左（新しい→古い）」の順序で取得する。
    /// 並び順は、モジュール内部の直近追加履歴における「最後に登場したインデックス」が大きいほど新しいとみなす。
    /// 履歴に存在しない属性は最も古い（末尾）に配置し、同順位は量の多い順で安定化する。
    /// </summary>
    public List<AttrPSnapshotEntry> GetAttrPSnapshotRecentFirst()
    {
        var src = AttrP.GetSnapshotRecentFirst();
        var list = new List<AttrPSnapshotEntry>();
        foreach (var s in src)
        {
            list.Add(new AttrPSnapshotEntry
            {
                Attr = s.Attr,
                Amount = s.Amount,
                RatioOfTotal = s.RatioOfTotal,
                RatioOfMax = s.RatioOfMax
            });
        }
        return list;
    }
    
    // ================================
    // 歩行コールバック向けAPI（A案: 呼び出し側で歩数管理）
    // ================================
    /// <summary>
    /// 歩行時に属性ポイントを減衰させる。stepIndex は 1 始まり（1歩目=2%、以降+6%で最大38%）。
    /// ランダム分岐はデフォルト20%（override可）。
    /// </summary>
    public int ApplyWalkingAttrPDecayStep(int stepIndex, float? randomRatioOverride = null)
    {
        return AttrP.ApplyWalkingDecayStep(stepIndex, randomRatioOverride);
    }

    /// <summary>
    /// 歩行時の消費量を事前計算して返す（実際には減らさない）。
    /// </summary>
    public int ComputeWalkingAttrPDecayAmount(int stepIndex)
    {
        return AttrP.ComputeWalkingDecayAmount(stepIndex);
    }



    // ================================
    // スキル使用時: ポイント→属性ポイント変換（実装は AttrPointModule へ移行済み）
    // ================================
    
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
        return AttrP.ConvertAndAddAttrPOnSkillUse(skillAttr, spentNormalP, spentAttrP);
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

        return AttrP.SettlePointsAfterSkillOutcome(skillAttr, spentNormalP, spentAttrP, outcome);
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
        return AttrP.SettlePointsAfterSkillOutcome(skillAttr, spentNormalP, spentAttrP, outcome);
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
        return AttrP.ComputeAttrMintedAmount(skillAttr, spentNormalP, spentAttrP);
    }
    // 変換ヘルパ/上限倍率などのロジックは AttrPointModule 側の実装を使用
}
