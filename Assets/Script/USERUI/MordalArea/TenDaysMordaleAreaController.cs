using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

/// <summary>
/// TenDays系モーダル用のページコントローラ。
/// 要件: 3ページ想定のページ配列と、左右ボタンでページインデックスを切替えるだけを実装。
/// </summary>
public class TenDaysMordaleAreaController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private Button m_LeftButton;
    [SerializeField] private Button m_RightButton;
    [Header("Close Gesture (Double-Tap)")]
    [SerializeField, Tooltip("この範囲内でダブルタップ（一本指）するとモーダルを閉じる")] private RectTransform m_CloseGestureArea;
    [SerializeField, Min(0.05f), Tooltip("ダブルタップ成立の最大遅延(秒)。この時間以内に2回目が終わったら成立")]
    private float m_DoubleTapMaxDelay = 0.35f;
    [SerializeField, Min(0f), Tooltip("ダブルタップ成立の許容距離(px)。この距離以内なら同じ場所とみなす")]
    private float m_DoubleTapMaxPixel = 64f;
    [Header("Close Gesture Filtering")]
    [SerializeField, Tooltip("これらのボタン群上のタップでは閉じない（無視する）")] private Button[] m_IgnoreTapButtons;

    [Header("Pages (Switchable Views)")]
    [SerializeField] private GameObject[] m_Pages; // インスペクタで3要素を推奨（可変対応）

    [Header("0page Components")]
    [SerializeField] private TextMeshProUGUI m_TenDaysText;
    
    [Header("Graph (Horizontal Bars)")]
    [SerializeField] private TenDayAbilityHorizontalBarsView m_TenDayBarsView;
    [SerializeField] private Slider m_ScaleSlider;
    [SerializeField, Min(0.01f)] private float m_SliderMinScale = 0.25f;
    [SerializeField, Min(0.01f)] private float m_SliderMaxScale = 3f;

    [Header("1page Components")]
    [SerializeField] private TextMeshProUGUI m_AttackText;
    [SerializeField] private AttackColumnsView m_AttackColumnsView;
    [SerializeField] private Slider m_AttackScaleSlider;
    [SerializeField, Min(0.01f)] private float m_AttackSliderMinScale = 0.25f;
    [SerializeField, Min(0.01f)] private float m_AttackSliderMaxScale = 3f;

    [SerializeField, Tooltip("ATK係数の換算表（存在時のみ Render() 実行）")]
    private AttackPowerCoefficientsTableView m_ATKCoefficientsView;

    [Header("Test (Attack)")]
    [Tooltip("攻撃グラフをテスト値で描画する（本番データの代わり）。true のときだけ有効")] 
    [SerializeField] private bool m_AttackUseTestValues = false;
    [Tooltip("テスト時に全列へ入れる一定値（例:40）")] 
    [SerializeField, Min(0f)] private float m_AttackTestConstantValue = 40f;
    [Tooltip("テスト時のハイライト戦闘規格（none を選ぶとエラーを出してハイライト無し）")] 
    [SerializeField] private BattleProtocol m_AttackTestHighlight = BattleProtocol.none;

    /// <summary>
    /// 現在のページインデックス（0..n-1）
    /// </summary>
    private int m_PageIndex = 0;

    [Header("Debug")]
    [SerializeField] private bool m_DebugLogs = true;

    [Header("Options")]
    [Tooltip("テスト用: true にするとゼロ値を含む全 TenDayAbility を一覧表示します。false なら従来の非ゼロ行のみの詳細表示。")]
    [SerializeField] private bool m_ShowAllAbilities = false;

    [Header("Test (Temporary)")]
    [Tooltip("モーダルを開いたときに、全ての十日能力=100のテストデータを表示します（TMP行数=列挙数）。不要になったらOFFにしてください。")]
    [SerializeField] private bool m_UseTestAll100OnOpen = true;

    [Header("2page Components")]
    [SerializeField] private TextMeshProUGUI m_DefenseText;
    [SerializeField] private DefenseColumnsView m_DefenseColumnsView;
    [SerializeField] private Slider m_DefenseScaleSlider;
    [SerializeField, Min(0.01f)] private float m_DefenseSliderMinScale = 0.25f;
    [SerializeField, Min(0.01f)] private float m_DefenseSliderMaxScale = 3f;

    [SerializeField, Tooltip("DEF係数の換算表（存在時のみ Render() 実行）")]
    private DefensePowerCoefficientsTableView m_DEFCoefficientsView;
    [Header("3page Components (AGI/EYE)")]
    [SerializeField] private TextMeshProUGUI m_AgiText;
    [SerializeField] private TextMeshProUGUI m_EyeText;
    [SerializeField, Tooltip("AGI係数の換算表（存在時のみ Render() 実行）")]
    private AgiPowerCoefficientsTableView m_AGICoefficientsView;
    [SerializeField, Tooltip("EYE係数の換算表（存在時のみ Render() 実行）")]
    private EyePowerCoefficientsTableView m_EYECoefficientsView;


    private BaseStates m_LastActor;
    private Canvas m_ParentCanvas;
    // ダブルタップ検出（モバイル）用の内部状態
    private float _lastTapTime = -1f;
    private Vector2 _lastTapPos;

    private void D(string msg)
    {
        if (m_DebugLogs)
        {
            Debug.Log("[TenDaysModal] " + msg);
        }
    }

    /// <summary>
    /// テスト用: すべての TenDayAbility を100として表示・グラフ化。
    /// TMP側の行数（m_TenDaysTextの行数）と TenDayAbility の列挙数を一致させるため、
    /// ここで1行ずつテキストを生成します。
    /// </summary>
    private void BindTest_All100()
    {
        var abilities = (TenDayAbility[])System.Enum.GetValues(typeof(TenDayAbility));
        // テキスト: 「表示名:100」を列挙順で1行ずつ
        if (m_TenDaysText != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ability in abilities)
            {
                sb.Append(ability.ToDisplayText());
                sb.Append(':');
                sb.Append("100");
                sb.AppendLine();
            }
            m_TenDaysText.text = sb.ToString().TrimEnd('\r','\n');
        }

        // グラフ: すべて100
        if (m_TenDayBarsView != null)
        {
            var vals = new float[abilities.Length];
            for (int i = 0; i < vals.Length; i++) vals[i] = 100f;
            m_TenDayBarsView.SetValues(vals);
            float s = (m_ScaleSlider != null) ? Mathf.Clamp(m_ScaleSlider.value, m_SliderMinScale, m_SliderMaxScale) : 1f;
            m_TenDayBarsView.SetUserScale(s);
        }

        D("BindTest_All100: applied test data (all=100). ※バーが出ない場合は BarsView の m_LineText に m_TenDaysText を割り当ててください");
    }

    private bool m_NavTempDisabled = false;

    private void Awake()
    {
        if (m_LeftButton != null)  m_LeftButton.onClick.AddListener(OnLeftNavClicked);
        if (m_RightButton != null) m_RightButton.onClick.AddListener(OnRightNavClicked);
        if (m_ScaleSlider != null)
        {
            m_ScaleSlider.minValue = m_SliderMinScale;
            m_ScaleSlider.maxValue = m_SliderMaxScale;
            m_ScaleSlider.onValueChanged.AddListener(OnScaleSliderChanged);
        }
        if (m_AttackScaleSlider != null)
        {
            m_AttackScaleSlider.minValue = m_AttackSliderMinScale;
            m_AttackScaleSlider.maxValue = m_AttackSliderMaxScale;
            m_AttackScaleSlider.onValueChanged.AddListener(OnAttackScaleSliderChanged);
        }
        if (m_DefenseScaleSlider != null)
        {
            m_DefenseScaleSlider.minValue = m_DefenseSliderMinScale;
            m_DefenseScaleSlider.maxValue = m_DefenseSliderMaxScale;
            m_DefenseScaleSlider.onValueChanged.AddListener(OnDefenseScaleSliderChanged);
        }
    }

    private void OnEnable()
    {
        D($"OnEnable: activeInHierarchy={gameObject.activeInHierarchy}, pageIndex={m_PageIndex}, pageCount={PageCount()}");
        // New Input System: enable enhanced touch and (in editor) touch simulation
        EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
        TouchSimulation.Enable();
#endif
        ApplyPageIndex();
        UpdateNavInteractable(PageCount());
        if (m_ParentCanvas == null) m_ParentCanvas = GetComponentInParent<Canvas>(true);
        // スライダー初期適用（未設定や0近傍の場合は 1 を既定に）
        if (m_ScaleSlider != null)
        {
            m_ScaleSlider.minValue = m_SliderMinScale;
            m_ScaleSlider.maxValue = m_SliderMaxScale;
            if (m_ScaleSlider.value < m_SliderMinScale || m_ScaleSlider.value > m_SliderMaxScale)
            {
                m_ScaleSlider.value = 1f;
            }
            else
            {
                // 値はそのまま反映
                OnScaleSliderChanged(m_ScaleSlider.value);
            }
        }

        if (m_AttackScaleSlider != null)
        {
            m_AttackScaleSlider.minValue = m_AttackSliderMinScale;
            m_AttackScaleSlider.maxValue = m_AttackSliderMaxScale;
            if (m_AttackScaleSlider.value < m_AttackSliderMinScale || m_AttackScaleSlider.value > m_AttackSliderMaxScale)
            {
                m_AttackScaleSlider.value = 1f;
            }
            else
            {
                OnAttackScaleSliderChanged(m_AttackScaleSlider.value);
            }
        }

        if (m_DefenseScaleSlider != null)
        {
            m_DefenseScaleSlider.minValue = m_DefenseSliderMinScale;
            m_DefenseScaleSlider.maxValue = m_DefenseSliderMaxScale;
            if (m_DefenseScaleSlider.value < m_DefenseSliderMinScale || m_DefenseScaleSlider.value > m_DefenseSliderMaxScale)
            {
                m_DefenseScaleSlider.value = 1f;
            }
            else
            {
                OnDefenseScaleSliderChanged(m_DefenseScaleSlider.value);
            }
        }

        // 一時: 全て100のテストデータで表示確認
        if (m_UseTestAll100OnOpen)
        {
            BindTest_All100();
        }

        // ATK係数表（存在時のみ）
        if (m_ATKCoefficientsView == null) m_ATKCoefficientsView = GetComponentInChildren<AttackPowerCoefficientsTableView>(true);
        if (m_ATKCoefficientsView != null) m_ATKCoefficientsView.Render();

        // DEF/AGI/EYE 係数表（存在時のみ）
        if (m_DEFCoefficientsView == null) m_DEFCoefficientsView = GetComponentInChildren<DefensePowerCoefficientsTableView>(true);
        if (m_DEFCoefficientsView != null) m_DEFCoefficientsView.Render();
        if (m_AGICoefficientsView == null) m_AGICoefficientsView = GetComponentInChildren<AgiPowerCoefficientsTableView>(true);
        if (m_AGICoefficientsView != null) m_AGICoefficientsView.Render();
        if (m_EYECoefficientsView == null) m_EYECoefficientsView = GetComponentInChildren<EyePowerCoefficientsTableView>(true);
        if (m_EYECoefficientsView != null) m_EYECoefficientsView.Render();
    }

    private void OnDisable()
    {
        D("OnDisable");
#if UNITY_EDITOR
        TouchSimulation.Disable();
#endif
    }

    private void OnDestroy()
    {
        if (m_ScaleSlider != null)
        {
            m_ScaleSlider.onValueChanged.RemoveListener(OnScaleSliderChanged);
        }
        if (m_AttackScaleSlider != null)
        {
            m_AttackScaleSlider.onValueChanged.RemoveListener(OnAttackScaleSliderChanged);
        }
        if (m_DefenseScaleSlider != null)
        {
            m_DefenseScaleSlider.onValueChanged.RemoveListener(OnDefenseScaleSliderChanged);
        }
    }

    private void Update()
    {
        if (m_CloseGestureArea == null) return;

        // Mobile (New Input System): 一本指のダブルタップを時間・距離で判定
        var active = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        for (int i = 0; i < active.Count; i++)
        {
            var t = active[i];
            if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                var pos = t.screenPosition;
                if (IsPointInCloseArea(pos) && !IsBlockedByUI(pos))
                {
                    float now = Time.unscaledTime;
                    if (_lastTapTime > 0f && (now - _lastTapTime) <= m_DoubleTapMaxDelay &&
                        (Vector2.Distance(pos, _lastTapPos) <= m_DoubleTapMaxPixel))
                    {
                        // ダブルタップ成立
                        _lastTapTime = -1f;
                        OnReturnClicked();
                        return;
                    }
                    // 1回目として記録
                    _lastTapTime = now;
                    _lastTapPos = pos;
                    return; // このフレームで他のEndedを処理しない
                }
            }
        }

    }

    private bool IsPointInCloseArea(Vector2 screenPos)
    {
        var cam = (m_ParentCanvas != null) ? m_ParentCanvas.worldCamera : null;
        // Camera/World Space で worldCamera が未設定の場合は Camera.main をフォールバック
        if (cam == null && m_ParentCanvas != null && m_ParentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = Camera.main;
        }
        return RectTransformUtility.RectangleContainsScreenPoint(m_CloseGestureArea, screenPos, cam);
    }

    // 指定の無視ボタン群（およびその子）にヒットしている場合のみ true
    private bool IsBlockedByUI(Vector2 screenPos)
    {
        if (m_IgnoreTapButtons == null || m_IgnoreTapButtons.Length == 0) return false; // 無視対象が無ければブロックしない

        var es = EventSystem.current;
        if (es == null) return false;

        var data = new PointerEventData(es) { position = screenPos };
        var results = new List<RaycastResult>();
        es.RaycastAll(data, results);
        if (results == null || results.Count == 0) return false;

        for (int i = 0; i < results.Count; i++)
        {
            var tr = results[i].gameObject.transform;
            for (int j = 0; j < m_IgnoreTapButtons.Length; j++)
            {
                var btn = m_IgnoreTapButtons[j];
                if (btn == null) continue;
                var ignoreRoot = btn.transform;
                if (tr == ignoreRoot || tr.IsChildOf(ignoreRoot))
                {
                    return true; // いずれかの無視ボタンにヒット -> ブロック
                }
            }
        }
        return false; // それ以外はブロックしない（=閉じてOK）
    }

    public int GetPageIndex()
    {
        return m_PageIndex;
    }

    public void SetPageIndex(int index)
    {
        int count = PageCount();
        if (count <= 0) return;
        int clamped = Mathf.Clamp(index, 0, count - 1);
        D($"SetPageIndex: request={index}, clamped={clamped}, current={m_PageIndex}, count={count}");
        if (clamped == m_PageIndex)
        {
            ApplyPageIndex();
            UpdateNavInteractable(count);
            return;
        }
        m_PageIndex = clamped;
        ApplyPageIndex();
        UpdateNavInteractable(count);
    }

    // 次のページへ（末尾では止まる：Characonfig流儀）
    public void Next()
    {
        D("Next()");
        SetPageIndex(m_PageIndex + 1);
    }

    // 前のページへ（先頭では止まる）
    public void Prev()
    {
        D("Prev()");
        SetPageIndex(m_PageIndex - 1);
    }

    // ページ表示切替
    private void ApplyPageIndex()
    {
        int count = PageCount();
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
        D($"ApplyPageIndex: index={m_PageIndex}, count={count}");
    }

    private void UpdateNavInteractable(int count)
    {
        bool hasMany = count > 1;
        if (m_LeftButton != null)
            m_LeftButton.interactable = !m_NavTempDisabled && hasMany && (m_PageIndex > 0);
        if (m_RightButton != null)
            m_RightButton.interactable = !m_NavTempDisabled && hasMany && (m_PageIndex < count - 1);
    }

    private int PageCount()
    {
        return (m_Pages != null) ? m_Pages.Length : 0;
    }

    public void SetNavTemporarilyDisabled(bool disabled)
    {
        if (m_NavTempDisabled == disabled) return;
        m_NavTempDisabled = disabled;
        UpdateNavInteractable(PageCount());
    }

    private void OnLeftNavClicked()
    {
        Prev();
    }

    private void OnRightNavClicked()
    {
        Next();
    }

    private void OnReturnClicked()
    {
        // 先にページを0へ戻してから閉じる（次回オープン時の初期ページ統一のため）
        SetPageIndex(0);

        var mc = ModalAreaController.Instance ?? FindObjectOfType<ModalAreaController>(true);
        if (mc != null)
        {
            mc.CloseFor(this.gameObject);
        }
        else
        {
            Debug.LogWarning("[TenDaysModal] ModalAreaController not found on Return.");
        }
    }

    // Characonfig からの要求時にのみ文面を生成して表示する
    public void Bind(BaseStates actor)
    {
        if (actor == null)
        {
            if (m_TenDaysText != null) m_TenDaysText.text = string.Empty;
            D("Bind: actor is null -> clear");
            if (m_TenDayBarsView != null) m_TenDayBarsView.Clear();
            ClearAttackPage();
            ClearDefensePage();
            ClearAgiEyeTexts();
            // 係数表はアクターに依存しないので描画だけ行う
            if (m_ATKCoefficientsView == null) m_ATKCoefficientsView = GetComponentInChildren<AttackPowerCoefficientsTableView>(true);
            if (m_ATKCoefficientsView != null) m_ATKCoefficientsView.Render();
            if (m_DEFCoefficientsView == null) m_DEFCoefficientsView = GetComponentInChildren<DefensePowerCoefficientsTableView>(true);
            if (m_DEFCoefficientsView != null) m_DEFCoefficientsView.Render();
            if (m_AGICoefficientsView == null) m_AGICoefficientsView = GetComponentInChildren<AgiPowerCoefficientsTableView>(true);
            if (m_AGICoefficientsView != null) m_AGICoefficientsView.Render();
            if (m_EYECoefficientsView == null) m_EYECoefficientsView = GetComponentInChildren<EyePowerCoefficientsTableView>(true);
            if (m_EYECoefficientsView != null) m_EYECoefficientsView.Render();
            return;
        }

        m_LastActor = actor;

        // 係数表（存在時のみ）
        if (m_ATKCoefficientsView == null) m_ATKCoefficientsView = GetComponentInChildren<AttackPowerCoefficientsTableView>(true);
        if (m_ATKCoefficientsView != null) m_ATKCoefficientsView.Render();
        if (m_DEFCoefficientsView == null) m_DEFCoefficientsView = GetComponentInChildren<DefensePowerCoefficientsTableView>(true);
        if (m_DEFCoefficientsView != null) m_DEFCoefficientsView.Render();
        if (m_AGICoefficientsView == null) m_AGICoefficientsView = GetComponentInChildren<AgiPowerCoefficientsTableView>(true);
        if (m_AGICoefficientsView != null) m_AGICoefficientsView.Render();
        if (m_EYECoefficientsView == null) m_EYECoefficientsView = GetComponentInChildren<EyePowerCoefficientsTableView>(true);
        if (m_EYECoefficientsView != null) m_EYECoefficientsView.Render();

        // 攻撃ページは常に実データで更新（テストモードでも）
        BindAttackPage(actor);
        // 防御ページも実データで更新
        BindDefensePage(actor);

        // テストモード中は TenDayAbility の実データバインドのみスキップ
        if (m_UseTestAll100OnOpen)
        {
            D("Bind: test mode active -> skip TenDay real binding; attack page bound with real data");
            if (m_TenDayBarsView != null)
            {
                float s = (m_ScaleSlider != null) ? Mathf.Clamp(m_ScaleSlider.value, m_SliderMinScale, m_SliderMaxScale) : 1f;
                m_TenDayBarsView.SetUserScale(s);
            }
            return;
        }

        if (m_TenDaysText == null)
        {
            D("Bind: m_TenDaysText is null");
            return;
        }
        if (m_TenDayBarsView != null)
        {
            m_TenDayBarsView.Bind(actor);
            float s = (m_ScaleSlider != null) ? Mathf.Clamp(m_ScaleSlider.value, m_SliderMinScale, m_SliderMaxScale) : 1f;
            m_TenDayBarsView.SetUserScale(s);
        }

        // テスト用: 全能力を詳細行形式で列挙（ゼロも含む）
        if (m_ShowAllAbilities)
        {
            // BaseStates.GetTenDayDisplayRows() と同様の辞書群を用いて差分を算出。
            // ただし UI 側では全列挙（Enum）で回し、ゼロ値も含めて行を生成する。
            var baseWithNormal = actor.TenDayValues(false);

            // Normal と各特判のフル(=Normal+特判)
            TenDayAbilityDictionary normalDict = null;
            TenDayAbilityDictionary tloaFull = null;
            TenDayAbilityDictionary bladeFull = null;
            TenDayAbilityDictionary magicFull = null;

            if (actor.NowUseWeapon != null && actor.NowUseWeapon.TenDayBonusData != null)
            {
                var bonus = actor.NowUseWeapon.TenDayBonusData;
                // 引数の意味は BaseStates と同様: (blade, magic, tloa)
                normalDict = bonus.GetTenDayAbilityDictionary(false, false, false);
                tloaFull   = bonus.GetTenDayAbilityDictionary(false, false, true);
                bladeFull  = bonus.GetTenDayAbilityDictionary(true,  false, false);
                magicFull  = bonus.GetTenDayAbilityDictionary(false, true,  false);
            }
            else
            {
                normalDict = new TenDayAbilityDictionary();
                tloaFull   = new TenDayAbilityDictionary();
                bladeFull  = new TenDayAbilityDictionary();
                magicFull  = new TenDayAbilityDictionary();
            }

            var sbAll = new System.Text.StringBuilder();
            foreach (TenDayAbility ability in System.Enum.GetValues(typeof(TenDayAbility)))
            {
                var name = ability.ToDisplayText();
                float baseValue = baseWithNormal.GetValueOrZero(ability);

                float normalB = 0f;
                float tloaB = 0f;
                float bladeB = 0f;
                float magicB = 0f;

                if (normalDict != null && normalDict.TryGetValue(ability, out var n)) normalB = n;
                if (tloaFull != null && tloaFull.TryGetValue(ability, out var tf)) tloaB = tf - normalB;
                if (bladeFull != null && bladeFull.TryGetValue(ability, out var bf)) bladeB = bf - normalB;
                if (magicFull != null && magicFull.TryGetValue(ability, out var mf)) magicB = mf - normalB;

                // {名前}:{値(+Normal補正)} 武器のスキル特判補正:TLOA{+x},刃物{+y},魔法{+z}
                sbAll.Append(name);
                sbAll.Append(':');
                sbAll.Append(baseValue.ToString("0.##"));
                sbAll.Append('(');
                if (normalB >= 0f) sbAll.Append('+');
                sbAll.Append(normalB.ToString("0.##"));
                sbAll.Append(')');
                sbAll.Append(',');
                sbAll.Append("TLOA{"); sbAll.Append(FormatSigned(tloaB)); sbAll.Append("},");
                sbAll.Append("刃物{"); sbAll.Append(FormatSigned(bladeB)); sbAll.Append("},");
                sbAll.Append("魔法{"); sbAll.Append(FormatSigned(magicB)); sbAll.Append('}');
                sbAll.AppendLine();
            }

            m_TenDaysText.text = sbAll.ToString();
            D($"Bind(all-detail): updated text length={m_TenDaysText.text?.Length ?? 0}");
            return;
        }

        // 従来表示: 非ゼロのみ詳細行（基本値と特判の追加分）
        {
            var rows = actor.GetTenDayDisplayRows();
            var sb = new System.Text.StringBuilder();
            foreach (var row in rows)
            {
                // {名前}:{値(+Normal補正)} 武器のスキル特判補正:TLOA{+x},刃物{+y},魔法{+z}
                sb.Append(row.Name);
                sb.Append(':');
                sb.Append(row.BaseValue.ToString("0.##"));
                sb.Append('(');
                if (row.NormalBonus >= 0f) sb.Append('+');
                sb.Append(row.NormalBonus.ToString("0.##"));
                sb.Append(')');
                sb.Append(',');
                sb.Append("TLOA{"); sb.Append(FormatSigned(row.TloaBonus)); sb.Append("},");
                sb.Append("刃物{"); sb.Append(FormatSigned(row.BladeBonus)); sb.Append("},");
                sb.Append("魔法{"); sb.Append(FormatSigned(row.MagicBonus)); sb.Append('}');
                sb.AppendLine();
            }

            m_TenDaysText.text = sb.ToString();
            D($"Bind(detail): updated text length={m_TenDaysText.text?.Length ?? 0}");
        }
    }

    private static string FormatSigned(float v)
    {
        if (v > 0f) return "+" + v.ToString("0.##");
        if (v < 0f) return v.ToString("0.##");
        return "0";
    }

    private void BindAttackPage(BaseStates actor)
    {
        if (actor == null) return;
        if (m_AttackColumnsView == null) m_AttackColumnsView = GetComponentInChildren<AttackColumnsView>(true);
        if (m_AttackColumnsView != null)
        {
            if (m_AttackUseTestValues)
            {
                // テスト時: 全列に一定値を入れ、ハイライトを指定。none 指定時は AttackColumnsView 側でエラーを出す。
                m_AttackColumnsView.BindTest(m_AttackTestConstantValue, m_AttackTestHighlight, true);
            }
            else
            {
                m_AttackColumnsView.Bind(actor);
            }
            // スケール適用（攻撃グラフ）
            if (m_AttackScaleSlider != null)
            {
                float s = Mathf.Clamp(m_AttackScaleSlider.value, m_AttackSliderMinScale, m_AttackSliderMaxScale);
                m_AttackColumnsView.SetUserScale(s);
            }
        }
        if (m_AttackText != null)
        {
            m_AttackText.text = BuildAttackText(actor);
        }

        // 防御テキスト
        if (m_DefenseText != null)
        {
            m_DefenseText.text = BuildDefenseText(actor);
        }

        // AGI/EYE テキスト
        if (m_AgiText != null)
        {
            m_AgiText.text = BuildAgiText(actor);
        }
        if (m_EyeText != null)
        {
            m_EyeText.text = BuildEyeText(actor);
        }
    }

    private void ClearAttackPage()
    {
        if (m_AttackText != null) m_AttackText.text = string.Empty;
        if (m_AttackColumnsView == null) m_AttackColumnsView = GetComponentInChildren<AttackColumnsView>(true);
        if (m_AttackColumnsView != null)
        {
            var chart = m_AttackColumnsView.GetComponent<ColumnsChart>();
            if (chart != null) chart.Clear();
        }
    }

    private static string BuildAttackText(BaseStates actor)
    {
        int atkInt = Mathf.FloorToInt(actor.b_ATK.Total);
        var current = actor.NowBattleProtocol;
        int exInt = Mathf.FloorToInt(actor.ATKProtocolExclusiveTotal(current));

        var sb = new System.Text.StringBuilder();
        sb.Append("攻撃の力:"); sb.Append(atkInt);
        sb.Append('(');
        sb.Append(exInt.ToString("+0;-0;0"));
        sb.Append(')');
        sb.Append('-'); sb.Append(current.ToDisplayText());
        sb.AppendLine();
        sb.Append("追加排他ステ ");

        bool first = true;
        var protocols = (BattleProtocol[])System.Enum.GetValues(typeof(BattleProtocol));
        for (int i = 0; i < protocols.Length; i++)
        {
            var p = protocols[i];
            if (p == BattleProtocol.none) continue;
            if (!first) sb.Append(", ");
            first = false;
            int v = Mathf.FloorToInt(actor.ATKProtocolExclusiveTotal(p));
            sb.Append(p.ToDisplayText()); sb.Append(':'); sb.Append(v.ToString("+0;-0;0")); sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildDefenseText(BaseStates actor)
    {
        // 共通防御力のみ（TenDayValues(false) × CommonDEF の総和）
        float common = 0f;
        var baseWithNormal = actor.TenDayValues(false);
        foreach (var kv in global::DefensePowerConfig.CommonDEF)
        {
            float td = baseWithNormal.GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f) common += td * kv.Value;
        }

        int defCommonInt = Mathf.FloorToInt(common);

        var sb = new System.Text.StringBuilder();
        sb.Append("防御(共通):"); sb.Append(defCommonInt);
        sb.AppendLine();
        sb.Append("追加排他ステ ");
        sb.AppendLine();

        bool first = true;
        var styles = (AimStyle[])System.Enum.GetValues(typeof(AimStyle));
        for (int i = 0; i < styles.Length; i++)
        {
            var s = styles[i];
            if (s == AimStyle.none) continue;
            if (!first) sb.Append(", ");
            first = false;
            int v = Mathf.FloorToInt(actor.DEFProtocolExclusiveTotal(s));
            sb.Append(s.ToDisplayText()); sb.Append(':'); sb.Append(v.ToString("+0;-0;0")); sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildAgiText(BaseStates actor)
    {
        int agiInt = Mathf.FloorToInt(actor.b_AGI.Total);
        var sb = new System.Text.StringBuilder();
        sb.Append("機動:"); sb.Append(agiInt);
        return sb.ToString();
    }

    private static string BuildEyeText(BaseStates actor)
    {
        int eyeInt = Mathf.FloorToInt(actor.b_EYE.Total);
        var sb = new System.Text.StringBuilder();
        sb.Append("視力:"); sb.Append(eyeInt);
        return sb.ToString();
    }

    private void OnScaleSliderChanged(float v)
    {
        if (m_TenDayBarsView == null) return;
        float s = Mathf.Clamp(v, m_SliderMinScale, m_SliderMaxScale);
        m_TenDayBarsView.SetUserScale(s);
    }

    private void OnAttackScaleSliderChanged(float v)
    {
        if (m_AttackColumnsView == null) m_AttackColumnsView = GetComponentInChildren<AttackColumnsView>(true);
        if (m_AttackColumnsView == null) return;
        float s = Mathf.Clamp(v, m_AttackSliderMinScale, m_AttackSliderMaxScale);
        m_AttackColumnsView.SetUserScale(s);
    }

    private void OnDefenseScaleSliderChanged(float v)
    {
        if (m_DefenseColumnsView == null) m_DefenseColumnsView = GetComponentInChildren<DefenseColumnsView>(true);
        if (m_DefenseColumnsView == null) return;
        float s = Mathf.Clamp(v, m_DefenseSliderMinScale, m_DefenseSliderMaxScale);
        m_DefenseColumnsView.SetUserScale(s);
    }

    private void BindDefensePage(BaseStates actor)
    {
        if (actor == null) return;
        if (m_DefenseColumnsView == null) m_DefenseColumnsView = GetComponentInChildren<DefenseColumnsView>(true);
        if (m_DefenseColumnsView != null)
        {
            m_DefenseColumnsView.Bind(actor);
            if (m_DefenseScaleSlider != null)
            {
                float s = Mathf.Clamp(m_DefenseScaleSlider.value, m_DefenseSliderMinScale, m_DefenseSliderMaxScale);
                m_DefenseColumnsView.SetUserScale(s);
            }
        }
    }

    private void ClearDefensePage()
    {
        if (m_DefenseText != null) m_DefenseText.text = string.Empty;
        if (m_DefenseColumnsView == null) m_DefenseColumnsView = GetComponentInChildren<DefenseColumnsView>(true);
        if (m_DefenseColumnsView != null)
        {
            var chart = m_DefenseColumnsView.GetComponent<ColumnsChart>();
            if (chart != null) chart.Clear();
        }
    }

    private void ClearAgiEyeTexts()
    {
        if (m_AgiText != null) m_AgiText.text = string.Empty;
        if (m_EyeText != null) m_EyeText.text = string.Empty;
    }
}