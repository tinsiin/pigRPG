using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RandomExtensions;

public sealed class AttrPointModule
{
    private readonly BaseStates _host;

    // 内部ストレージ
    private readonly Dictionary<SpiritualProperty, int> _attrPMap = new();
    private int _attrPTotal;
    private readonly List<AttrChunk> _attrAddHistory = new(); // DropNew 用LIFO

    // 通知バッチ
    private int _attrPBatchDepth;
    private readonly HashSet<SpiritualProperty> _attrPDirty = new();

    public AttrPointModule(BaseStates host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    // ========= 歩行時の属性ポイント減衰 =========
    // p(n) = 2 + 6*(n-1) [%], 上限は GetWalkingDecayProbabilityCap()。
    // 基準量 base = floor(0.6 * Total + 0.4 * Max)。
    // 消費量 consume = min(floor(base * p/100), Total)。
    // ランダム20%: カテゴリをランダム選択し続け、複数カテゴリに跨って目標消費量まで減らす。
    // 古い順80%: "種類"の最古登場順（_attrAddHistory の最小インデックス）でカテゴリ単位にまとめて消費。

    private float GetWalkingDecayProbabilityCap()
    {
        // TODO: 将来的に TenDay 等で 38 → 13 へ引き下げ可能にする
        return 38f;
    }

    public int ComputeWalkingDecayAmount(int stepIndex)
    {
        int total = CombinedAttrPTotal;
        if (total <= 0) return 0;
        float pCap = GetWalkingDecayProbabilityCap();
        float p = Mathf.Min(2f + 6f * (Mathf.Max(1, stepIndex) - 1), pCap);
        int baseAmount = Mathf.FloorToInt(0.6f * total + 0.4f * CombinedAttrPMax);
        int consume = Mathf.Min(Mathf.FloorToInt(baseAmount * (p / 100f)), total);
        return Mathf.Max(0, consume);
    }

    public int ApplyWalkingDecayStep(int stepIndex, float? randomRatioOverride = null)
    {
        int total = CombinedAttrPTotal;
        if (total <= 0) return 0;
        int consume = ComputeWalkingDecayAmount(stepIndex);
        if (consume <= 0) return 0;

        float randRatio = randomRatioOverride ?? 0.2f; // 5分の1
        bool randomMode = RandomEx.Shared.NextFloat(0f, 1f) < randRatio;
        using (BeginAttrPBatch())
        {
            int remain = consume;
            var changed = new HashSet<SpiritualProperty>();

            if (randomMode)
            {
                // 複数カテゴリ横断でランダムに削る
                while (remain > 0)
                {
                    var candidates = _attrPMap.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToArray();
                    if (candidates.Length == 0) break;
                    var choice = candidates[RandomEx.Shared.NextInt(0, candidates.Length)];
                    int have = GetAttrP(choice);
                    if (have <= 0) continue;
                    int take = Mathf.Min(have, remain);
                    DecreaseAttrForSingle(choice, take);
                    remain -= take;
                    changed.Add(choice);
                }
            }
            else
            {
                // 種類の最古登場順でカテゴリ単位に消費
                var earliestIndex = new Dictionary<SpiritualProperty, int>();
                for (int i = 0; i < _attrAddHistory.Count; i++)
                {
                    var a = _attrAddHistory[i].Attr;
                    if (!_attrPMap.TryGetValue(a, out var v) || v <= 0) continue;
                    if (!earliestIndex.ContainsKey(a)) earliestIndex[a] = i;
                }
                var order = earliestIndex.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
                foreach (var a in order)
                {
                    if (remain <= 0) break;
                    int have = GetAttrP(a);
                    if (have <= 0) continue;
                    int take = Mathf.Min(have, remain);
                    DecreaseAttrForSingle(a, take);
                    remain -= take;
                    changed.Add(a);
                }
            }

            foreach (var a in changed) NotifyAttrPChanged(a);
            ValidateAttrPInvariants();
            return consume - remain;
        }
    }


    // ========= 公開イベント =========
    public event Action<SpiritualProperty, int> OnAttrPChanged;

    // ========= 状態のエクスポート/インポート（DeepCopy 用） =========
    public sealed class AttrPointModuleState
    {
        public Dictionary<SpiritualProperty, int> Map = new();
        public List<KeyValuePair<SpiritualProperty, int>> History = new();
    }

    public AttrPointModuleState ExportState()
    {
        var st = new AttrPointModuleState();
        foreach (var kv in _attrPMap)
        {
            st.Map[kv.Key] = kv.Value;
        }
        // 最新→古い の順序を保ったまま数量>0のみ出力
        for (int i = 0; i < _attrAddHistory.Count; i++)
        {
            var ch = _attrAddHistory[i];
            if (ch.Amount > 0)
            {
                st.History.Add(new KeyValuePair<SpiritualProperty, int>(ch.Attr, ch.Amount));
            }
        }
        return st;
    }

    public void ImportState(AttrPointModuleState st, bool suppressNotify = true)
    {
        if (st == null) return;
        var changed = suppressNotify ? null : new HashSet<SpiritualProperty>();
        _attrPMap.Clear();
        _attrAddHistory.Clear();
        _attrPTotal = 0;
        foreach (var kv in st.Map)
        {
            _attrPMap[kv.Key] = kv.Value;
            _attrPTotal += Mathf.Max(0, kv.Value);
            if (!suppressNotify) changed?.Add(kv.Key);
        }
        foreach (var kv in st.History)
        {
            if (kv.Value > 0) _attrAddHistory.Add(new AttrChunk(kv.Key, kv.Value));
        }
        if (!suppressNotify && changed != null && changed.Count > 0)
        {
            foreach (var a in changed) OnAttrPChanged?.Invoke(a, GetAttrP(a));
        }
    }

    // ========= 上限/合計 =========
    public int CombinedAttrPTotal => _attrPTotal;
    public int CombinedAttrPMax => Mathf.RoundToInt(_host.MAXP * GetAttrCombinedCapMultiplier(_host.NowPower));
    public int CombinedAttrPRemaining => Mathf.Max(0, CombinedAttrPMax - _attrPTotal);

    // ========= 参照系 =========
    public int GetAttrP(SpiritualProperty attr)
    {
        return _attrPMap.TryGetValue(attr, out var v) ? v : 0;
    }

    // ========= 追加/消費 =========
    public int TryAddAttrP(SpiritualProperty attr, int amount)
    {
        if (amount <= 0) return 0;
        var space = CombinedAttrPMax - _attrPTotal;
        if (space <= 0) return 0; // DropNew: 満杯なら追加不可
        var added = amount > space ? space : amount;
        if (added <= 0) return 0;
        if (_attrPMap.ContainsKey(attr)) _attrPMap[attr] += added; else _attrPMap[attr] = added;
        _attrPTotal += added;
        _attrAddHistory.Add(new AttrChunk(attr, added));
        NotifyAttrPChanged(attr);
        ValidateAttrPInvariants();
        return added;
    }

    public bool TrySpendAttrP(SpiritualProperty attr, int cost)
    {
        if (cost <= 0) return true;
        var have = GetAttrP(attr);
        if (have < cost) return false;
        DecreaseAttrForSingle(attr, cost);
        NotifyAttrPChanged(attr);
        ValidateAttrPInvariants();
        return true;
    }

    // ========= 一括バッチ =========
    public IDisposable BeginAttrPBatch()
    {
        _attrPBatchDepth++;
        return new AttrPBatchToken(this);
    }

    private struct AttrPBatchToken : IDisposable
    {
        private readonly AttrPointModule _owner;
        public AttrPBatchToken(AttrPointModule owner) { _owner = owner; }
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

    // ========= 全消去/上限再クランプ =========
    public void ClearAllAttrP()
    {
        var keys = _attrPMap.Keys.ToArray();
        _attrPMap.Clear();
        _attrPTotal = 0;
        _attrAddHistory.Clear();
        using (BeginAttrPBatch())
        {
            foreach (var k in keys) NotifyAttrPChanged(k);
        }
        ValidateAttrPInvariants();
    }

    public void ReclampAttrPToCapDropNew()
    {
        var cap = CombinedAttrPMax;
        if (_attrPTotal <= cap) return;
        var overflow = _attrPTotal - cap;
        var changed = new HashSet<SpiritualProperty>();
        using (BeginAttrPBatch())
        {
            ReduceLatestAcrossAllAttrs(overflow, changed);
            if (_attrPTotal < 0) _attrPTotal = 0;
            foreach (var a in changed) NotifyAttrPChanged(a);
        }
        ValidateAttrPInvariants();
    }

    // ========= スナップショット(UI用) =========
    public readonly struct AttrPSnapshotEntry
    {
        public readonly SpiritualProperty Attr;
        public readonly int Amount;
        public readonly float RatioOfTotal;
        public readonly float RatioOfMax;
        public AttrPSnapshotEntry(SpiritualProperty a, int amt, float rTot, float rMax)
        { Attr = a; Amount = amt; RatioOfTotal = rTot; RatioOfMax = rMax; }
    }

    public List<AttrPSnapshotEntry> GetSnapshotByAmount(bool sortDesc = true)
    {
        var total = (float)CombinedAttrPTotal;
        var max = (float)CombinedAttrPMax;
        IEnumerable<KeyValuePair<SpiritualProperty, int>> seq = _attrPMap;
        if (sortDesc) seq = seq.OrderByDescending(kv => kv.Value);
        var list = new List<AttrPSnapshotEntry>();
        foreach (var kv in seq)
        {
            list.Add(new AttrPSnapshotEntry(kv.Key, kv.Value,
                total > 0 ? kv.Value / total : 0f,
                max > 0 ? kv.Value / max : 0f));
        }
        return list;
    }

    public List<AttrPSnapshotEntry> GetSnapshotRecentFirst()
    {
        var total = (float)CombinedAttrPTotal;
        var max = (float)CombinedAttrPMax;
        var lastIndex = new Dictionary<SpiritualProperty, int>();
        for (int i = 0; i < _attrAddHistory.Count; i++) lastIndex[_attrAddHistory[i].Attr] = i;

        var ordered = _attrPMap
            .Where(kv => kv.Value > 0)
            .Select(kv => new { Attr = kv.Key, Amount = kv.Value, Index = lastIndex.TryGetValue(kv.Key, out var idx) ? idx : -1 })
            .OrderByDescending(x => x.Index)
            .ThenByDescending(x => x.Amount);

        var list = new List<AttrPSnapshotEntry>();
        foreach (var x in ordered)
        {
            list.Add(new AttrPSnapshotEntry(x.Attr, x.Amount,
                total > 0 ? x.Amount / total : 0f,
                max > 0 ? x.Amount / max : 0f));
        }
        return list;
    }

    // ========= スキル起点の変換/清算 =========

    private const float NORMAL_TO_ATTR_MIN = 1.48f;
    private const float NORMAL_TO_ATTR_MAX = 1.66f;

    public int ConvertAndAddAttrPOnSkillUse(SpiritualProperty skillAttr, int spentNormalP, Dictionary<SpiritualProperty, int> spentAttrP)
    {
        if (spentAttrP == null) spentAttrP = new Dictionary<SpiritualProperty, int>();
        if (!IsAttrDefinedForConversion(skillAttr))
            throw new ArgumentOutOfRangeException(nameof(skillAttr), $"Unsupported SpiritualProperty for conversion: {skillAttr}");
        foreach (var key in spentAttrP.Keys)
        {
            if (!IsAttrDefinedForConversion(key))
                throw new ArgumentOutOfRangeException(nameof(spentAttrP), $"Unsupported SpiritualProperty for conversion: {key}");
        }

        int totalAdded = 0;
        if (spentNormalP > 0)
        {
            var sumAll = spentNormalP + spentAttrP.Values.Sum();
            var m = GetNormalToAttrMultiplier();
            var minted = Mathf.FloorToInt(sumAll * m);
            totalAdded += TryAddAttrP(skillAttr, minted);
            return totalAdded;
        }

        int mintedTotal = 0;
        if (spentAttrP.TryGetValue(skillAttr, out var sameSpent) && sameSpent > 0)
        {
            var sameCap = GetSameAttrCap(skillAttr);
            var sameEff = sameCap * GetTenDayConvFactor(true, skillAttr, skillAttr);
            var add = Mathf.FloorToInt(sameSpent * Mathf.Max(0f, sameEff));
            mintedTotal += add;
        }
        foreach (var kv in spentAttrP)
        {
            var fromAttr = kv.Key;
            if (fromAttr == skillAttr) continue;
            var amount = kv.Value; if (amount <= 0) continue;
            var otherCap = GetOtherAttrCap(skillAttr, fromAttr);
            var otherEff = otherCap * GetTenDayConvFactor(false, skillAttr, fromAttr);
            var add = Mathf.FloorToInt(amount * Mathf.Max(0f, otherEff));
            mintedTotal += add;
        }
        if (mintedTotal > 0) totalAdded += TryAddAttrP(skillAttr, mintedTotal);
        return totalAdded;
    }

    public int ComputeAttrMintedAmount(SpiritualProperty skillAttr, int spentNormalP, Dictionary<SpiritualProperty, int> spentAttrP)
    {
        if (spentAttrP == null) spentAttrP = new Dictionary<SpiritualProperty, int>();
        if (!IsAttrDefinedForConversion(skillAttr))
            throw new ArgumentOutOfRangeException(nameof(skillAttr), $"Unsupported SpiritualProperty for conversion: {skillAttr}");
        foreach (var key in spentAttrP.Keys)
        {
            if (!IsAttrDefinedForConversion(key))
                throw new ArgumentOutOfRangeException(nameof(spentAttrP), $"Unsupported SpiritualProperty for conversion: {key}");
        }
        if (spentNormalP > 0)
        {
            var sumAll = spentNormalP + spentAttrP.Values.Sum();
            var m = GetNormalToAttrMultiplier();
            return Mathf.FloorToInt(sumAll * m);
        }
        int mintedTotal = 0;
        if (spentAttrP.TryGetValue(skillAttr, out var sameSpent) && sameSpent > 0)
        {
            var sameCap = GetSameAttrCap(skillAttr);
            var sameEff = sameCap * GetTenDayConvFactor(true, skillAttr, skillAttr);
            var add = Mathf.FloorToInt(sameSpent * Mathf.Max(0f, sameEff));
            mintedTotal += add;
        }
        foreach (var kv in spentAttrP)
        {
            var fromAttr = kv.Key; if (fromAttr == skillAttr) continue;
            var amount = kv.Value; if (amount <= 0) continue;
            var otherCap = GetOtherAttrCap(skillAttr, fromAttr);
            var otherEff = otherCap * GetTenDayConvFactor(false, skillAttr, fromAttr);
            var add = Mathf.FloorToInt(amount * Mathf.Max(0f, otherEff));
            mintedTotal += add;
        }
        return mintedTotal;
    }

    private const float CRIT_OUTPUT_SCALE = 1.60f;
    private const float GRAZE_OUTPUT_SCALE = 0.30f;
    private const float EVADE_REFUND_SCALE = 0.40f;

    public int SettlePointsAfterSkillOutcome(SpiritualProperty skillAttr, int spentNormalP, Dictionary<SpiritualProperty, int> spentAttrP, HitResult outcome)
    {
        if (spentAttrP == null) spentAttrP = new Dictionary<SpiritualProperty, int>();
        bool canConvert = IsAttrDefinedForConversion(skillAttr);
        switch (outcome)
        {
            case HitResult.Hit:
                if (!canConvert) { Debug.LogWarning($"[AttrPointModule] undefined skillAttr={skillAttr}. Skip convert."); return 0; }
                return ConvertAndAddAttrPOnSkillUse(skillAttr, spentNormalP, spentAttrP);
            case HitResult.Critical:
                if (!canConvert) { Debug.LogWarning($"[AttrPointModule] undefined skillAttr={skillAttr}. Skip convert(critical)."); return 0; }
                {
                    int minted = ComputeAttrMintedAmount(skillAttr, spentNormalP, spentAttrP);
                    int scaled = Mathf.FloorToInt(minted * Mathf.Max(0f, CRIT_OUTPUT_SCALE));
                    return scaled > 0 ? TryAddAttrP(skillAttr, scaled) : 0;
                }
            case HitResult.Graze:
                if (!canConvert) { Debug.LogWarning($"[AttrPointModule] undefined skillAttr={skillAttr}. Skip convert(graze)."); return 0; }
                {
                    int minted = ComputeAttrMintedAmount(skillAttr, spentNormalP, spentAttrP);
                    int scaled = Mathf.FloorToInt(minted * Mathf.Max(0f, GRAZE_OUTPUT_SCALE));
                    return scaled > 0 ? TryAddAttrP(skillAttr, scaled) : 0;
                }
            case HitResult.CompleteEvade:
                {
                    int refunded = 0;
                    int refundN = Mathf.FloorToInt(spentNormalP * Mathf.Max(0f, EVADE_REFUND_SCALE));
                    if (refundN > 0) _host.P += refundN;
                    foreach (var kv in spentAttrP)
                    {
                        int refundA = Mathf.FloorToInt(kv.Value * Mathf.Max(0f, EVADE_REFUND_SCALE));
                        if (refundA > 0) refunded += TryAddAttrP(kv.Key, refundA);
                    }
                    return 0; // 返り値は「変換で増えた量」
                }
            default:
                return 0;
        }
    }

    /// <summary>
    /// ノーマルPと属性Pをアトミックに消費。途中失敗時は属性Pをロールバック。
    /// SkillResourceFlow.CanConsumeOnCast は事前チェック層として継続利用する前提。
    /// </summary>
    public bool TryConsumeForSkillAtomic(BaseSkill skill)
    {
        if (skill == null) { Debug.LogError("[AttrPointModule] TryConsumeForSkillAtomic: skill is null"); return false; }
        int reqNormal = Mathf.Max(0, skill.RequiredNormalP);
        var reqAttr = skill.RequiredAttrP;
        if (_host.P < reqNormal) { Debug.LogError($"[AttrPointModule] NormalP不足 need={reqNormal} have={_host.P}"); return false; }
        if (reqAttr != null)
        {
            foreach (var kv in reqAttr)
            {
                int need = Mathf.Max(0, kv.Value);
                if (need == 0) continue;
                if (GetAttrP(kv.Key) < need)
                {
                    Debug.LogError($"[AttrPointModule] 属性P不足 attr={kv.Key} need={need} have={GetAttrP(kv.Key)}");
                    return false;
                }
            }
        }

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
                        foreach (var back in consumedAttr)
                        {
                            var restored = TryAddAttrP(back.Key, back.Value);
                            if (restored != back.Value)
                            {
                                Debug.LogWarning($"[AttrPointModule] ロールバック不足 attr={back.Key} expected={back.Value} restored={restored}");
                            }
                        }
                        Debug.LogError($"[AttrPointModule] 属性P消費失敗 attr={kv.Key} need={need}");
                        return false;
                    }
                    consumedAttr.Add(new KeyValuePair<SpiritualProperty, int>(kv.Key, need));
                }
            }

            if (_host.P < reqNormal) // 念のため再確認
            {
                foreach (var back in consumedAttr)
                {
                    var restored = TryAddAttrP(back.Key, back.Value);
                    if (restored != back.Value)
                    {
                        Debug.LogWarning($"[AttrPointModule] ロールバック不足 attr={back.Key} expected={back.Value} restored={restored}");
                    }
                }
                Debug.LogError($"[AttrPointModule] ノーマルP消費に失敗 need={reqNormal} have={_host.P}");
                return false;
            }

            _host.P -= reqNormal;
            return true;
        }
    }

    // ========= 内部実装 =========

    private void NotifyAttrPChanged(SpiritualProperty a)
    {
        if (_attrPBatchDepth > 0) _attrPDirty.Add(a);
        else OnAttrPChanged?.Invoke(a, GetAttrP(a));
    }

    private struct AttrChunk
    {
        public SpiritualProperty Attr;
        public int Amount;
        public AttrChunk(SpiritualProperty attr, int amount)
        { Attr = attr; Amount = amount; }
    }

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
            if (chunk.Amount > 0) _attrAddHistory[i] = chunk; else _attrAddHistory.RemoveAt(i);
        }
    }

    // 単一属性を amount 減算する共通処理（Map/History/Total を一貫更新）
    private void DecreaseAttrForSingle(SpiritualProperty attr, int amount)
    {
        if (amount <= 0) return;
        if (!_attrPMap.TryGetValue(attr, out var cur) || cur <= 0) return;
        int take = Mathf.Min(cur, amount);
        ReduceLatestForAttr(attr, take);
        int left = cur - take;
        if (left > 0) _attrPMap[attr] = left; else _attrPMap.Remove(attr);
        _attrPTotal -= take; if (_attrPTotal < 0) _attrPTotal = 0;
    }

    private void ReduceLatestAcrossAllAttrs(int amount, HashSet<SpiritualProperty> changed)
    {
        var overflow = amount;
        for (int i = _attrAddHistory.Count - 1; i >= 0 && overflow > 0; i--)
        {
            var chunk = _attrAddHistory[i];
            var reduce = Math.Min(chunk.Amount, overflow);
            if (_attrPMap.TryGetValue(chunk.Attr, out var cur))
            {
                var left = cur - reduce;
                if (left > 0) _attrPMap[chunk.Attr] = left; else _attrPMap.Remove(chunk.Attr);
                changed?.Add(chunk.Attr);
            }
            _attrPTotal -= reduce;
            overflow -= reduce;
            chunk.Amount -= reduce;
            if (chunk.Amount > 0) _attrAddHistory[i] = chunk; else _attrAddHistory.RemoveAt(i);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void ValidateAttrPInvariants()
    {
        try
        {
            int sumMap = 0; foreach (var v in _attrPMap.Values) sumMap += v;
            if (_attrPTotal != sumMap)
            {
                Debug.LogError($"[AttrPointModule] Invariant violation: _attrPTotal({_attrPTotal}) != sumMap({sumMap}) host={_host.CharacterName}");
            }
            for (int i = 0; i < _attrAddHistory.Count; i++)
            {
                var amt = _attrAddHistory[i].Amount;
                if (amt < 0) Debug.LogError($"[AttrPointModule] History negative amount at {i} host={_host.CharacterName}");
            }
            if (_attrPTotal < 0) Debug.LogError($"[AttrPointModule] _attrPTotal negative: {_attrPTotal} host={_host.CharacterName}");

            // 属性別: History の合計と Map の値が一致するか検証
            var histSum = new Dictionary<SpiritualProperty, int>();
            for (int i = 0; i < _attrAddHistory.Count; i++)
            {
                var ch = _attrAddHistory[i];
                if (!histSum.ContainsKey(ch.Attr)) histSum[ch.Attr] = 0;
                histSum[ch.Attr] += Mathf.Max(0, ch.Amount);
            }
            foreach (var kv in _attrPMap)
            {
                int s = histSum.TryGetValue(kv.Key, out var val) ? val : 0;
                if (s != kv.Value)
                {
                    Debug.LogError($"[AttrPointModule] Per-attr mismatch attr={kv.Key} map={kv.Value} histSum={s} host={_host.CharacterName}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AttrPointModule] ValidateAttrPInvariants exception: {ex}");
        }
    }

    private float GetNormalToAttrMultiplier()
    {
        return RandomEx.Shared.NextFloat(NORMAL_TO_ATTR_MIN, NORMAL_TO_ATTR_MAX);
    }

    private float GetTenDayConvFactor(bool isSameAttr, SpiritualProperty skillAttr, SpiritualProperty fromAttr)
    {
        // 将来 TenDay の値により調整。現状は1.0固定。
        return 1f;
    }

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

    private float GetOtherAttrCap(SpiritualProperty skillAttr, SpiritualProperty fromAttr)
    {
        if (!IsAttrDefinedForConversion(skillAttr))
            throw new ArgumentOutOfRangeException(nameof(skillAttr), $"Unsupported SpiritualProperty for conversion: {skillAttr}");
        if (!IsAttrDefinedForConversion(fromAttr))
            throw new ArgumentOutOfRangeException(nameof(fromAttr), $"Unsupported SpiritualProperty for conversion: {fromAttr}");

        float baseCap = skillAttr switch
        {
            SpiritualProperty.liminalwhitetile => 0f,
            SpiritualProperty.kindergarden => 1.3f,
            SpiritualProperty.sacrifaith => 0f,
            SpiritualProperty.cquiest => 0.3f,
            SpiritualProperty.devil => 0.2f,
            SpiritualProperty.doremis => 0.8f,
            SpiritualProperty.pillar => 0.1f,
            SpiritualProperty.godtier => 0.4f,
            SpiritualProperty.baledrival => 0.0889f,
            SpiritualProperty.pysco => 0f,
            _ => throw new ArgumentOutOfRangeException(nameof(skillAttr), $"Unsupported SpiritualProperty for conversion: {skillAttr}")
        };

        if (skillAttr == SpiritualProperty.pysco && fromAttr == SpiritualProperty.kindergarden) return 2.0f;
        if (skillAttr == SpiritualProperty.cquiest && fromAttr == SpiritualProperty.devil) return 0.0f;
        if (skillAttr == SpiritualProperty.devil && fromAttr == SpiritualProperty.cquiest) return 0.6f;
        if (skillAttr == SpiritualProperty.devil && fromAttr == SpiritualProperty.pysco) return 0.0f;
        return baseCap;
    }

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
}
