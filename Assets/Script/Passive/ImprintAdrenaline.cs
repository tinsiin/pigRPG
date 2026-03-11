using System;
using UnityEngine;

/// <summary>
/// Imprint Adrenaline パッシブ
/// 同じスキル印象のスキルを連続行動で使い続けるとコンボが蓄積し、
/// 汎用クリティカル率が段階的に上昇する。
/// カウンタが上限(5)に達した次の使用でバースト効果が発動し、カウンタがリセットされる。
///
/// プレイヤー版: DurationTurn を設定した時限パッシブ（スキルで付与）
/// 敵版: DurationTurn = -1 の永続パッシブ
/// 同じクラスの別インスタンスをPassiveManagerに登録して使い分ける。
/// </summary>
[Serializable]
public class ImprintAdrenaline : BasePassive
{
    //  ═══════════════════════════════════════════════════════
    //  コンボ設定（定数）
    //  ═══════════════════════════════════════════════════════

    const int ComboMax = 5;

    /// <summary> コンボ数ごとの汎用クリティカル率（%）。インデックス = コンボ数 </summary>
    static readonly float[] ComboRateTable = { 0f, 0f, 3f, 6f, 8f, 12f, 14f };

    /// <summary> バースト時の確定威力倍率 </summary>
    const float BurstDamageMultiplier = 1.4f;

    /// <summary> バースト1回ごとの恒常クリティカル率UP（%） </summary>
    const float PermanentCritPerBurst = 2f;

    /// <summary> 汎用クリティカル発動時のダメージ倍率（BaseStatesの定数を参照） </summary>
    public const float GenericCriticalMultiplier = BaseStates.GenericCriticalMultiplier;

    //  ═══════════════════════════════════════════════════════
    //  ランタイム状態（DeepCopyで引き継がない）
    //  ═══════════════════════════════════════════════════════

    [NonSerialized] int _comboCount;
    [NonSerialized] SkillImpression _lastImpression;
    [NonSerialized] int _lastActedTurnCount = -1;
    [NonSerialized] bool _isBurstAction;
    [NonSerialized] float _currentComboRate;
    [NonSerialized] bool _hasLastImpression;

    //  ═══════════════════════════════════════════════════════
    //  公開プロパティ
    //  ═══════════════════════════════════════════════════════

    /// <summary> 現在のコンボ数 </summary>
    public int ComboCount => _comboCount;

    /// <summary> 今回のアクションがバーストかどうか </summary>
    public bool IsBurstAction => _isBurstAction;

    /// <summary> 現在のコンボによる汎用クリティカル率（%） </summary>
    public float CurrentComboRate => _currentComboRate;

    //  ═══════════════════════════════════════════════════════
    //  ライフサイクル
    //  ═══════════════════════════════════════════════════════

    public override void OnApply(BaseStates user, BaseStates grantor)
    {
        _comboCount = 0;
        _lastActedTurnCount = -1;
        _isBurstAction = false;
        _currentComboRate = 0f;
        _hasLastImpression = false;
        base.OnApply(user, grantor);
    }

    //  ═══════════════════════════════════════════════════════
    //  コンボ判定（スキル使用前に呼ばれる）
    //  ═══════════════════════════════════════════════════════

    /// <summary>
    /// 攻撃者のスキル実行前に呼ばれる。
    /// コンボカウンタを更新し、汎用クリティカル率とバーストフラグをセットする。
    /// </summary>
    public override void OnBeforeSkillAction()
    {
        if (_owner == null || _owner.NowUseSkill == null) return;

        var skill = _owner.NowUseSkill;
        var currentImpression = skill.Impression;
        var currentTurnCount = manager != null ? manager.BattleTurnCount : 0;

        // 連続行動判定: TurnCount差分が1であること
        bool isConsecutiveTurn = _lastActedTurnCount >= 0
            && (currentTurnCount - _lastActedTurnCount == 1);

        // 同じスキル印象かどうか
        bool isSameImpression = _hasLastImpression
            && _lastImpression == currentImpression;

        // コンボ更新
        if (isConsecutiveTurn && isSameImpression)
        {
            _comboCount++;
        }
        else
        {
            // 途切れた or 初回 → 1から開始
            _comboCount = 1;
        }

        // バースト判定（コンボ6回目 = ComboMax + 1）
        _isBurstAction = _comboCount > ComboMax;

        // クリティカル率をテーブルから取得
        int rateIndex = Mathf.Min(_comboCount, ComboRateTable.Length - 1);
        _currentComboRate = ComboRateTable[rateIndex];

        // 記録更新
        _lastImpression = currentImpression;
        _lastActedTurnCount = currentTurnCount;
        _hasLastImpression = true;

        // バースト処理
        if (_isBurstAction)
        {
            // 恒常クリティカル率を加算（BaseStatesに永続保存）
            _owner.GenericCriticalRate += PermanentCritPerBurst;

            Debug.Log($"[ImprintAdrenaline] バースト発動！ 恒常クリ率 +{PermanentCritPerBurst}% → 合計 {_owner.GenericCriticalRate}%");

            // コンボリセット
            _comboCount = 0;
            _hasLastImpression = false;
        }
    }

    //  ═══════════════════════════════════════════════════════
    //  汎用クリティカル率の提供
    //  ═══════════════════════════════════════════════════════

    /// <summary>
    /// このパッシブが提供する汎用クリティカル率（%）を返す。
    /// BaseStates.GenericCriticalRate（恒常分）とは別にコンボ分を加算する。
    /// </summary>
    public override float GetGenericCriticalContribution()
    {
        return _currentComboRate;
    }

    /// <summary>
    /// バースト時の確定威力倍率を返す。バーストでなければ1.0f。
    /// </summary>
    public override float GetBurstMultiplier()
    {
        return _isBurstAction ? BurstDamageMultiplier : 1.0f;
    }

    //  ═══════════════════════════════════════════════════════
    //  攻撃後のクリーンアップ
    //  ═══════════════════════════════════════════════════════

    public override void OnAfterAttack()
    {
        // バーストフラグはアクション単位なのでリセット
        _isBurstAction = false;
        base.OnAfterAttack();
    }
}
