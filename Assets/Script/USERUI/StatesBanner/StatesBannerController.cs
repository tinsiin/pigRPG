using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller class for managing StatesBanner UI elements.
/// Provides centralized control over gauge and text components within the status banner.
/// </summary>
public class StatesBannerController : MonoBehaviour
{
    [Header("General Components")]
    [Header("0=Geino, 1=Noramlia, 2=Sites")]
    [SerializeField] private Sprite[] m_BackGroungSprites;
    [SerializeField] private Image m_InkBackGround;
    [Header("0Page Components")]

    [Header("Gauge Components")]
    [SerializeField] private StatesBannerGauge m_HPGauge;
    [SerializeField] private StatesBannerGauge m_MentalHPGauge;
    [SerializeField] private StatesBannerGauge m_Pgauge;
    [SerializeField] private StatesBannerGauge m_ThinkingGauge;
    [SerializeField] private StatesBannerGauge m_AttrPBar;

    [Header("Pages (Switchable Views)")]
    [SerializeField] private GameObject[] m_Pages; // インスペクタで設定（長さは3推奨だが可変対応）
    /// <summary>
    /// 現在のページインデックス（0..n-1）。キャラ切替時にも維持します。
    /// </summary>
    private int m_PageIndex = 0;

    /// <summary>
    /// CharaconfigController から流れてくるキャラインデックスで背景を切替
    /// </summary>
    private int m_CurrentCharacterIndex = 0;

    [Header("Tap Input")]
    [SerializeField, Tooltip("バナー全体のButton。未割当なら同一GameObjectから自動取得")]
    private Button m_BannerButton;

    [Header("Text Components")]
    [SerializeField] private TextMeshProUGUI m_HPText;
    [SerializeField] private TextMeshProUGUI m_PText;    
    [SerializeField] private TextMeshProUGUI m_AttrPText;
    [SerializeField] private TextMeshProUGUI m_ImpressionText;

    [SerializeField] private TextMeshProUGUI m_ThinkingText;
    [SerializeField] private TextMeshProUGUI m_MentalHPText;

    [Header("AttrP Text List (Segments)")]
    [SerializeField] private StatesBannerAttrPointsText m_AttrPTextSegments;

    [Header("1Page Components")]
    [SerializeField] private TextMeshProUGUI m_PowerText;
    [SerializeField] private TextMeshProUGUI m_WeaponText;
    [SerializeField, Header("慣れ補正、カウントの指定は表示される慣れスキル印象の数です。")] 
    private TextMeshProUGUI m_AdaptationText;
    [SerializeField] int m_AdaptationDisplayCount = 7;

    [Header("2Page Components")]

    //[SerializeField, Tooltip("攻撃排他ステのグラフ")] 
    //private StatesBannerAttackColumnsView m_AttackColumnsView;
    //[SerializeField, Tooltip("防御の排他ステのグラフ")] 
    //private StatesBannerDefenseColumnsView m_DefenseColumnsView;
    [SerializeField]
    private TextMeshProUGUI m_atkText;
    [SerializeField]
    private TextMeshProUGUI m_defText;
    [SerializeField, Tooltip("右側カラムに表示する防御テキスト（任意）。未割当なら単一カラム表示。")]
    private TextMeshProUGUI m_defRightText;
    [SerializeField]
    private TextMeshProUGUI m_eyeText;
    [SerializeField]
    private TextMeshProUGUI m_agiText;


    [Header("Optimization Cache")]
    private bool _hasLast;
    private float _lastHPPercent;
    private float _lastMentalHPPercent;
    private float _lastPPercent;
    private float _lastThinkingPercent;
    private float _lastAttrPPercent;

    private void SetStatesBanner(BaseStates actor)
    {
        // 参照値をローカルに展開
        float HP = actor.HP;
        float MaxHP = actor.MaxHP;
        float MentalHP = actor.MentalHP;
        float P = actor.P;
        float MaxP = actor.MAXP;
        float Thinking = actor.NowResonanceValue;
        float MaxThinking = actor.ResonanceValue;
        float attrP = actor.CombinedAttrPTotal;
        float attrMaxP = actor.CombinedAttrPMax;
        SpiritualProperty impression = actor.MyImpression;
        SpiritualProperty DefaultImpression = actor.DefaultImpression;
        ThePower power = actor.NowPower;
        BaseWeapon weapon = actor.NowUseWeapon;

        // 四大ステ・排他/防御
        float atk = actor.b_ATK.Total;
        float base_def = actor.b_DEF(AimStyle.none).Total;
        float atk_Ex = actor.ATKProtocolExclusiveTotal(actor.NowBattleProtocol);
        float def_AcrobatMinor = actor.b_DEF(AimStyle.AcrobatMinor).Total;
        float def_Doublet = actor.b_DEF(AimStyle.Doublet).Total;
        float def_QuadStrike = actor.b_DEF(AimStyle.QuadStrike).Total;
        float def_Duster = actor.b_DEF(AimStyle.Duster).Total;
        float def_PotanuVolf = actor.b_DEF(AimStyle.PotanuVolf).Total;
        float def_CentralHeavenStrike = actor.b_DEF(AimStyle.CentralHeavenStrike).Total;

        float eye = actor.b_EYE.Total;
        float agi = actor.b_AGI.Total;

        // パーセンテージ計算
        float hpPercent = (MaxHP != 0f) ? (HP / MaxHP * 100f) : 0f;
        float mentalPercent = (MaxHP != 0f) ? (MentalHP / MaxHP * 100f) : 0f; // 精神HPは最大HP基準
        float pPercent = (MaxP != 0f) ? (P / MaxP * 100f) : 0f;
        float thinkingPercent = (MaxThinking != 0f) ? (Thinking / MaxThinking * 100f) : 0f;
        float attrPPercent = (attrMaxP != 0f) ? (attrP / attrMaxP * 100f) : 0f;

        // ゲージ: 変化がなければSetPercentを呼ばない
        if (!_hasLast || !Mathf.Approximately(_lastHPPercent, hpPercent))
        {
            m_HPGauge.SetPercent(hpPercent);
            _lastHPPercent = hpPercent;
        }
        if (!_hasLast || !Mathf.Approximately(_lastMentalHPPercent, mentalPercent))
        {
            m_MentalHPGauge.SetPercent(mentalPercent);
            _lastMentalHPPercent = mentalPercent;
        }
        if (!_hasLast || !Mathf.Approximately(_lastPPercent, pPercent))
        {
            m_Pgauge.SetPercent(pPercent);
            _lastPPercent = pPercent;
        }
        if (!_hasLast || !Mathf.Approximately(_lastThinkingPercent, thinkingPercent))
        {
            m_ThinkingGauge.SetPercent(thinkingPercent);
            _lastThinkingPercent = thinkingPercent;
        }
        if (!_hasLast || !Mathf.Approximately(_lastAttrPPercent, attrPPercent))
        {
            m_AttrPBar.SetPercent(attrPPercent);
            _lastAttrPPercent = attrPPercent;
        }

        // テキスト: 同値代入スキップ（レイアウト抑制）: 表示はすべて小数点以下切り捨て
        int hpInt = Mathf.FloorToInt(HP);
        int maxHPInt = Mathf.FloorToInt(MaxHP);
        string hpText = $"{hpInt}/{maxHPInt}";
        if (m_HPText != null && m_HPText.text != hpText) m_HPText.text = hpText;

        int mentalPctInt = (MaxHP != 0f) ? Mathf.FloorToInt(MentalHP / MaxHP * 100f) : 0;
        string mentalText = $"{mentalPctInt} %";
        if (m_MentalHPText != null && m_MentalHPText.text != mentalText) m_MentalHPText.text = mentalText;

        int pInt = Mathf.FloorToInt(P);
        int maxPInt = Mathf.FloorToInt(MaxP);
        string pText = $"{pInt}/{maxPInt}";
        if (m_PText != null && m_PText.text != pText) m_PText.text = pText;

        int thinkingInt = Mathf.FloorToInt(Thinking);
        int maxThinkingInt = Mathf.FloorToInt(MaxThinking);
        string thinkingText = $"{thinkingInt}/{maxThinkingInt}";
        if (m_ThinkingText != null && m_ThinkingText.text != thinkingText) m_ThinkingText.text = thinkingText;

        int attrPInt = Mathf.FloorToInt(attrP);
        int attrMaxPInt = Mathf.FloorToInt(attrMaxP);
        string attrPText = $"{attrPInt}/{attrMaxPInt}";
        if (m_AttrPText != null && m_AttrPText.text != attrPText) m_AttrPText.text = attrPText;

        string impressionText = $"{impression}\n[{DefaultImpression}]";
        if (m_ImpressionText != null && m_ImpressionText.text != impressionText) m_ImpressionText.text = impressionText;

        string powerText = $"パワー:{power.ToDisplayText()}";
        if (m_PowerText != null && m_PowerText.text != powerText) m_PowerText.text = powerText;

        string weaponText = $"武器:{weapon.name} -戦闘規格:{actor.NowBattleProtocol.ToDisplayShortText()}";
        if (m_WeaponText != null && m_WeaponText.text != weaponText) m_WeaponText.text = weaponText;
        
        int atkInt = Mathf.FloorToInt(atk);
        int atkExInt = Mathf.FloorToInt(atk_Ex);
        string atkText = $"攻撃の力:{atkInt}(+{atkExInt.ToString("+0;-0;0")})";//攻撃力に()表示で排他ステータス表記（整数・符号付）
        if (m_atkText != null && m_atkText.text != atkText) m_atkText.text = atkText;
        int baseDefInt = Mathf.FloorToInt(base_def);
        int defAcrobatMinorInt = Mathf.FloorToInt(def_AcrobatMinor);
        int defDoubletInt = Mathf.FloorToInt(def_Doublet);
        int defQuadStrikeInt = Mathf.FloorToInt(def_QuadStrike);
        int defDusterInt = Mathf.FloorToInt(def_Duster);
        int defPotanuVolfInt = Mathf.FloorToInt(def_PotanuVolf);
        int defCentralHeavenStrikeInt = Mathf.FloorToInt(def_CentralHeavenStrike);

        // 防御テキストは左（基礎〜QuadStrike）と右（Duster以降）に分割
        string defLeftText =
            $"基礎/無:{baseDefInt}\n" +
            $"{AimStyle.AcrobatMinor.ToDisplayShortText()}:{defAcrobatMinorInt}\n" +
            $"{AimStyle.Doublet.ToDisplayShortText()}:{defDoubletInt}\n" +
            $"{AimStyle.QuadStrike.ToDisplayShortText()}:{defQuadStrikeInt}";

        string defRightText =
            $"{AimStyle.Duster.ToDisplayShortText()}:{defDusterInt}\n" +
            $"{AimStyle.PotanuVolf.ToDisplayShortText()}:{defPotanuVolfInt}\n" +
            $"{AimStyle.CentralHeavenStrike.ToDisplayShortText()}:{defCentralHeavenStrikeInt}";

        if (m_defRightText != null)
        {
            if (m_defText != null && m_defText.text != defLeftText) m_defText.text = defLeftText;
            if (m_defRightText.text != defRightText) m_defRightText.text = defRightText;
        }
        else
        {
            // 右カラム未使用時は従来通り1つに結合して表示
            string defCombined = defLeftText + "\n" + defRightText;
            if (m_defText != null && m_defText.text != defCombined) m_defText.text = defCombined;
        }
        int eyeInt = Mathf.FloorToInt(eye);
        string eyeText = $"視力:{eyeInt}";
        if (m_eyeText != null && m_eyeText.text != eyeText) m_eyeText.text = eyeText;
        int agiInt = Mathf.FloorToInt(agi);
        string agiText = $"機動:{agiInt}";
        if (m_agiText != null && m_agiText.text != agiText) m_agiText.text = agiText;

        // AttrP のセグメント（下線付きカラー）も数値と同タイミングで再構築
        // 低頻度想定だが、StatesBannerAttrPointsText 側に差分スキップがあるため安全
        if (m_AttrPTextSegments == null) m_AttrPTextSegments = GetComponentInChildren<StatesBannerAttrPointsText>(true);
        if (m_AttrPTextSegments != null) m_AttrPTextSegments.Bind(actor);

        _hasLast = true;
    }

    // --- Page switching (tap to cycle) ---
    private void Awake()
    {
        // Button未割当なら自身から自動取得
        if (m_BannerButton == null) m_BannerButton = GetComponent<Button>();
        if (m_BannerButton != null)
        {
            m_BannerButton.onClick.AddListener(NextPage);
        }

        m_InkBackGround.gameObject.SetActive(true);//デザイン時邪魔なので消してるから表示
    }

    private void OnEnable()
    {
        // 有効化時に現在のページインデックスで表示反映
        ApplyPageIndex();
    }

    /// <summary>
    /// 外部API: 現在ページインデックスを取得
    /// </summary>
    public int GetPageIndex()
    {
        return m_PageIndex;
    }

    /// <summary>
    /// 外部API: ページインデックスを設定（Clamp + 反映）。通常はバナータップでのみ変更。
    /// </summary>
    public void SetPageIndex(int index)
    {
        int count = (m_Pages != null) ? m_Pages.Length : 0;
        if (count <= 0) return;
        int clamped = Mathf.Clamp(index, 0, count - 1);
        if (clamped == m_PageIndex)
        {
            ApplyPageIndex();
            return;
        }
        m_PageIndex = clamped;
        ApplyPageIndex();
    }

    /// <summary>
    /// バナータップで次のページへ（末尾→先頭へループ）
    /// </summary>
    public void NextPage()
    {
        int count = (m_Pages != null) ? m_Pages.Length : 0;
        if (count <= 0) return;
        m_PageIndex = (m_PageIndex + 1) % count;
        ApplyPageIndex();
    }

    /// <summary>
    /// 現在のページインデックスに応じて子ページのActiveを切替
    /// </summary>
    private void ApplyPageIndex()
    {
        int count = (m_Pages != null) ? m_Pages.Length : 0;
        if (count <= 0) return;
        if (m_PageIndex < 0 || m_PageIndex >= count)
        {
            m_PageIndex = Mathf.Clamp(m_PageIndex, 0, count - 1);
        }
        for (int i = 0; i < count; i++)
        {
            var go = m_Pages[i];
            if (go != null) go.SetActive(i == m_PageIndex);
        }
    }

    /// <summary>
    /// アクターを受け取り、属性ポイントバーの購読設定と数値UIの即時反映を一括で行う。
    /// </summary>
    public void Bind(BaseStates actor)
    {

        if (actor == null) return;

        // 数値UIの即時反映
        SetStatesBanner(actor);

        // 低頻度更新: 属性ポイントの文字UIを再構築（オーブ色と同期した下線）
        if (m_AttrPTextSegments == null) m_AttrPTextSegments = GetComponentInChildren<StatesBannerAttrPointsText>(true);
        if (m_AttrPTextSegments != null) m_AttrPTextSegments.Bind(actor);

        // 慣れ補正デバッグ表示（存在時のみ）
        if (m_AdaptationText != null)
        {
            var text = actor.GetAdaptationBannerText(m_AdaptationDisplayCount);
            if (m_AdaptationText.text != text) m_AdaptationText.text = text;
        }


        // 攻撃/防御のカラムビュー（存在時のみ）
        //if (m_AttackColumnsView == null) m_AttackColumnsView = GetComponentInChildren<StatesBannerAttackColumnsView>(true);
        //if (m_AttackColumnsView != null) m_AttackColumnsView.Bind(actor);
        //if (m_DefenseColumnsView == null) m_DefenseColumnsView = GetComponentInChildren<StatesBannerDefenseColumnsView>(true);
        //if (m_DefenseColumnsView != null) m_DefenseColumnsView.Bind(actor);


    }


    /// <summary>
    /// 外部API: キャラインデックスを設定（背景を該当キャラのスプライトに切替）
    /// </summary>
    public void SetCharacterIndex(int index)
    {
        int length = (m_BackGroungSprites != null) ? m_BackGroungSprites.Length : 0;
        if (length <= 0)
        {
            m_CurrentCharacterIndex = 0;
            UpdateBannerBackground();
            return;
        }
        m_CurrentCharacterIndex = Mathf.Clamp(index, 0, length - 1);
        UpdateBannerBackground();
    }

    /// <summary>
    /// キャラインデックスに対応した背景スプライトを m_InkBackGround に設定する。
    /// </summary>
    private void UpdateBannerBackground()
    {
        if (m_InkBackGround == null || m_BackGroungSprites == null || m_BackGroungSprites.Length == 0)
        {
            return;
        }

        int idx = Mathf.Clamp(m_CurrentCharacterIndex, 0, m_BackGroungSprites.Length - 1);
        var sprite = m_BackGroungSprites[idx];

        // スプライト適用（null の場合は表示を外す）
        m_InkBackGround.sprite = sprite;
        if (sprite == null)
        {
            return;
        }


    }
}
