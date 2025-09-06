using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 単一ボタンによるON/OFFトグルを提供する汎用コンポーネント。
/// ・内部でON/OFF状態（bool）を保持
/// ・クリックで状態反転し、intコールバック(0/1)およびboolコールバック(true/false)を通知
/// ・ラベル(Text)の自動更新に対応（任意）
/// </summary>
public class ToggleSingleController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button m_Button;
    [SerializeField] private TextMeshProUGUI m_LabelTMP; // 任意（TMP）。存在すればこちらを優先

    [Header("Label Texts")] 
    [SerializeField] private string m_OnText = "ON";
    [SerializeField] private string m_OffText = "OFF";

    [Header("Initial State")]
    [SerializeField] private bool m_IsOn = true;

    // コールバック（ToggleGroupController と合わせて int 0/1 にも対応）
    private UnityAction<int> m_OnToggleSelectedIndex; // 0: ON, 1: OFF
    private UnityAction<bool> m_OnToggleSelectedBool; // true: ON, false: OFF

    /// <summary>
    /// グローバルに有効/無効を設定
    /// </summary>
    public bool interactable
    {
        get { return m_Button != null ? m_Button.interactable : false; }
        set { if (m_Button != null) m_Button.interactable = value; }
    }

    /// <summary>
    /// 現在のインデックス（0: ON, 1: OFF）
    /// </summary>
    public int CurrentIndex
    {
        get { return m_IsOn ? 0 : 1; }
    }

    /// <summary>
    /// 現在のON/OFF状態
    /// </summary>
    public bool IsOn
    {
        get { return m_IsOn; }
    }

    private void Awake()
    {
        if (m_Button == null)
        {
            m_Button = GetComponentInChildren<Button>();
        }
        if (m_LabelTMP == null)
        {
            m_LabelTMP = GetComponentInChildren<TextMeshProUGUI>(true);
        }
        UpdateLabel();
    }

    private void OnEnable()
    {
        if (m_Button != null)
        {
            m_Button.onClick.AddListener(OnClick);
        }
    }

    private void OnDisable()
    {
        if (m_Button != null)
        {
            m_Button.onClick.RemoveListener(OnClick);
        }
    }

    /// <summary>
    /// int(0/1)リスナーを追加（0: ON, 1: OFF）
    /// </summary>
    public void AddListener(UnityAction<int> listener)
    {
        m_OnToggleSelectedIndex += listener;
    }

    /// <summary>
    /// bool(true/false)リスナーを追加（true: ON, false: OFF）
    /// </summary>
    public void AddBoolListener(UnityAction<bool> listener)
    {
        m_OnToggleSelectedBool += listener;
    }

    /// <summary>
    /// int(0/1)リスナーを削除
    /// </summary>
    public void RemoveListener(UnityAction<int> listener)
    {
        m_OnToggleSelectedIndex -= listener;
    }

    /// <summary>
    /// bool(true/false)リスナーを削除
    /// </summary>
    public void RemoveBoolListener(UnityAction<bool> listener)
    {
        m_OnToggleSelectedBool -= listener;
    }

    /// <summary>
    /// 現在状態を反転させて通知
    /// </summary>
    public void Toggle()
    {
        SetOn(!m_IsOn);
    }

    /// <summary>
    /// 状態を設定して通知（true: ON, false: OFF）
    /// </summary>
    public void SetOn(bool isOn)
    {
        m_IsOn = isOn;
        UpdateLabel();
        Notify();
    }

    /// <summary>
    /// 状態を設定（通知なし）
    /// </summary>
    public void SetOnWithoutNotify(bool isOn)
    {
        m_IsOn = isOn;
        UpdateLabel();
    }

    /// <summary>
    /// ラベル文言を一括設定（必要に応じて即時リフレッシュ）
    /// </summary>
    public void SetLabelTexts(string onText, string offText, bool refresh = true)
    {
        if (onText != null) m_OnText = onText;
        if (offText != null) m_OffText = offText;
        if (refresh) UpdateLabel();
    }

    /// <summary>
    /// 状態をインデックス(0/1)で設定して通知（0: ON, 1: OFF）
    /// </summary>
    public void SetOn(int index)
    {
        SetOn(index == 0);
    }

    /// <summary>
    /// 状態をインデックス(0/1)で設定（通知なし）
    /// </summary>
    public void SetOnWithoutNotify(int index)
    {
        SetOnWithoutNotify(index == 0);
    }

    private void OnClick()
    {
        Toggle();
    }

    private void Notify()
    {
        // int と bool の両方に通知
        m_OnToggleSelectedIndex?.Invoke(CurrentIndex);
        m_OnToggleSelectedBool?.Invoke(m_IsOn);
    }

    private void UpdateLabel()
    {
        string txt = m_IsOn ? m_OnText : m_OffText;
        if (m_LabelTMP != null)
        {
            m_LabelTMP.text = txt;
        }
    }
}
