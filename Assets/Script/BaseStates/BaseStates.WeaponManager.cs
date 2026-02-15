using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;
using static TenDayAbilityPosition;

//武器管理
public abstract partial class BaseStates
{
    /// <summary>
    /// 装備中の武器
    /// </summary>
    [NonSerialized]
    public BaseWeapon NowUseWeapon;
    /// <summary>
    /// 初期所持してる武器のID
    /// </summary>
    [Space]
    [Header("武器/戦闘規格")]
    [Tooltip("初期所持している武器のID（シーン開始時に適用）")]
    public int InitWeaponID;

    // ── 適応ラグ ──
    /// <summary>
    /// 排他ATK係数に掛かる適応率（0.77〜1.0）
    /// </summary>
    [NonSerialized] private float _adaptationRate = 1.0f;
    /// <summary>
    /// ランダムで決まった適応率のスタート値
    /// </summary>
    [NonSerialized] private float _adaptationStartRate = 1.0f;
    /// <summary>
    /// 規格/武器切替後に経過したBM戦闘回数
    /// </summary>
    [NonSerialized] private int _battlesSinceProtocolSwitch;

    /// <summary>
    /// 現在の適応率（外部参照用）
    /// </summary>
    public float AdaptationRate => _adaptationRate;

    /// <summary>
    /// 指定した武器をこのキャラクターが装備できるか判定する。
    /// BaseTenDayValues が weapon.TenDayValues の全要求を満たしていれば true。
    /// </summary>
    public bool CanEquipWeapon(BaseWeapon weapon)
    {
        if (weapon == null) return false;
        if (weapon.TenDayValues == null || weapon.TenDayValues.Count == 0) return true;

        var abilities = BaseTenDayValues;
        if (abilities == null) return false;

        foreach (var req in weapon.TenDayValues)
        {
            abilities.TryGetValue(req.Key, out float charVal);
            if (charVal < req.Value) return false;
        }
        return true;
    }

    /// <summary>
    /// 武器装備、武器から移る戦闘規格の変化。
    /// 能力値不足の場合はフリーハンドにフォールバックする。
    /// </summary>
    /// <returns>指定した武器の装備に成功した場合 true。フォールバックまたは失敗時 false。</returns>
    public bool ApplyWeapon(int ID)
    {
        if (WeaponManager.Instance == null)
        {
            Debug.LogError("WeaponManager.Instance is null");
            return false;
        }
        var weapon = WeaponManager.Instance.GetAtID(ID);
        if (weapon == null)
        {
            weapon = WeaponManager.Instance.GetFreehandWeapon();
            if (weapon == null)
            {
                Debug.LogError($"ApplyWeapon: ID={ID} の武器もフリーハンド武器も見つかりません");
                return false;
            }
            Debug.LogWarning($"ApplyWeapon: ID={ID} の武器が見つからないため、フリーハンドを装備します");
        }

        // 能力値チェック: 不足ならフリーハンドにフォールバック
        if (!CanEquipWeapon(weapon))
        {
            var freehand = WeaponManager.Instance.GetFreehandWeapon();
            Debug.LogWarning($"ApplyWeapon: {CharacterName} は武器「{weapon.name}」(ID={ID}) の能力値要件を満たしていません。フリーハンドを装備します");
            if (freehand == null)
            {
                Debug.LogError("ApplyWeapon: フリーハンド武器が見つかりません");
                return false;
            }
            weapon = freehand;
        }

        // 武器変更時の名目ラグ（99%固定、実質影響なし）
        bool weaponChanged = NowUseWeapon != null && NowUseWeapon != weapon;
        if (weaponChanged)
        {
            _adaptationStartRate = 0.99f;
            _adaptationRate = 0.99f;
            _battlesSinceProtocolSwitch = 0;
        }

        NowUseWeapon = weapon;
        NowBattleProtocol = weapon.protocol;

        // 武器スキルの初期化（武器スキルはSkillListに含まれないため個別に初期化）
        if (weapon.WeaponSkill != null)
        {
            if (!weapon.WeaponSkill.IsInitialized)
                weapon.WeaponSkill.OnInitialize(this);

            if (weapon.WeaponSkill.ZoneTrait == 0)
                Debug.LogError($"武器「{weapon.name}」の武器スキル「{weapon.WeaponSkill.SkillName}」のZoneTraitが未設定(0)です。Inspectorで設定してください");
        }

        // 掛け合わせスキルの初期化
        if (weapon.CombinationEntries != null)
        {
            foreach (var entry in weapon.CombinationEntries)
            {
                if (entry?.combinedSkill != null && !entry.combinedSkill.IsInitialized)
                    entry.combinedSkill.OnInitialize(this);
            }
        }

        return weapon.id == ID;
    }

    /// <summary>
    /// 複数規格武器で戦闘規格を切り替える
    /// </summary>
    public void SwitchProtocol(BattleProtocol newProtocol)
    {
        if (NowBattleProtocol == newProtocol) return;
        if (NowUseWeapon == null || !NowUseWeapon.protocols.Contains(newProtocol)) return;

        // 適応率スタート値をランダム決定
        var freehand = WeaponManager.Instance?.GetFreehandWeapon();
        bool isFromFreehand = (NowUseWeapon == freehand);
        float minRate = isFromFreehand ? 0.90f : 0.77f;
        _adaptationStartRate = UnityEngine.Random.Range(minRate, 1.0f);
        _adaptationRate = _adaptationStartRate;
        _battlesSinceProtocolSwitch = 0;

        NowBattleProtocol = newProtocol;
    }

    /// <summary>
    /// 戦闘終了時に適応率を回復する（線形6戦で100%）
    /// </summary>
    public void RecoverAdaptation()
    {
        if (_adaptationRate >= 1.0f) return;
        _battlesSinceProtocolSwitch++;
        _adaptationRate = Mathf.Min(1.0f,
            _adaptationStartRate + _battlesSinceProtocolSwitch * (1.0f - _adaptationStartRate) / 6f);
    }

    /// <summary>
    /// 今のキャラの戦闘規格
    /// </summary>
    [NonSerialized]
    public BattleProtocol NowBattleProtocol;

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
