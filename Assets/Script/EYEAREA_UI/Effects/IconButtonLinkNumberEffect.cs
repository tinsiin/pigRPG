using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// このコンポーネントを付与したUIアイコンの上下左右に同じ数字を表示し、
/// 数字ボタンとアイコンの関連を視覚的に示します。
/// - 半径とフォントサイズは調整可能
/// - 標準の UnityEngine.UI.Text を使用（TMP 依存なし）
/// Canvas 配下の対象アイコンの GameObject にアタッチしてください。
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class IconButtonLinkNumberEffect : MonoBehaviour
{
    [SerializeField] private RectTransform _iconRect;

    [Header("数字の設定")]
    [SerializeField] private int m_Number = 1;
    [SerializeField] private Color m_Color = Color.white;

    [Header("レイアウト設定")]
    [Min(0f)]
    [SerializeField] private float m_Radius = 40f;
    [Min(1)]
    [SerializeField] private int m_FontSize = 28;

    [Tooltip("任意のカスタムフォント。未設定の場合は組み込みの LegacyRuntime.ttf を使用します。")]
    [SerializeField] private Font m_Font;

    [Header("アイコンに合わせて自動調整")]
    [SerializeField] private bool m_AutoAdjustByIcon = true;
    [Tooltip("半径 = アイコンの幅 × 倍率")] 
    [SerializeField] private float m_RadiusRatioToIcon = 0.5f;
    [Tooltip("フォントサイズ = アイコンの高さ × 倍率")] 
    [SerializeField] private float m_FontSizeRatioToIcon = 0.35f;

    private Text _top;
    private Text _bottom;
    private Text _left;
    private Text _right;

    private const string TopName = "NumberTop";
    private const string BottomName = "NumberBottom";
    private const string LeftName = "NumberLeft";
    private const string RightName = "NumberRight";

    private void Awake()
    {
        EnsureRefs();
        EnsureTexts();
        ApplyAll();
    }

    private void OnEnable()
    {
        EnsureRefs();
        EnsureTexts();
        ApplyAll();
    }

    private void OnValidate()
    {
        EnsureRefs();
        EnsureTexts();
        ApplyAll();
    }

    private void EnsureRefs()
    {
        if (_iconRect == null)
        {
            _iconRect = GetComponent<RectTransform>();
        }
    }

    private void EnsureTexts()
    {
        if (_top == null) _top = GetOrCreateChildText(TopName);
        if (_bottom == null) _bottom = GetOrCreateChildText(BottomName);
        if (_left == null) _left = GetOrCreateChildText(LeftName);
        if (_right == null) _right = GetOrCreateChildText(RightName);
    }

    /// <summary>
    /// アイコンの矩形サイズに基づいて半径とフォントサイズを初期化します。
    /// ArrowGrowAndVanish.InitializeArrowByIcon と同様の意図で、倍率を用いて決定します。
    /// </summary>
    /// <param name="iconOverride">ここに渡した場合、内部のRectTransform参照を上書きして使用します。</param>
    public void InitializeByIcon(RectTransform iconOverride = null)
    {
        EnsureRefs();
        EnsureTexts();

        if (iconOverride != null)
        {
            _iconRect = iconOverride;
        }

        if (_iconRect == null)
        {
            Debug.LogWarning("[IconButtonLinkNumberEffect] InitializeByIcon: RectTransform が見つかりません。");
            return;
        }

        Vector2 iconSize = _iconRect.rect.size;
        if (m_AutoAdjustByIcon)
        {
            // 倍率から現在の適用値を計算し、ApplyAllで使用されるよう更新
            float r = Mathf.Max(0f, iconSize.x * m_RadiusRatioToIcon);
            int f = Mathf.Max(1, Mathf.RoundToInt(iconSize.y * m_FontSizeRatioToIcon));
            // シリアライズ値自体は保持したい場合は代入しない選択もあるが、
            // Inspectorで確認しやすいようここでは反映する。
            m_Radius = r;
            m_FontSize = f;
        }

        ApplyAll();
    }

    private Text GetOrCreateChildText(string childName)
    {
        var t = transform.Find(childName) as RectTransform;
        if (t == null)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            t = go.GetComponent<RectTransform>();
            t.SetParent(transform, false);
            t.anchorMin = new Vector2(0.5f, 0.5f);
            t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = new Vector2(0.5f, 0.5f);
        }

        var text = t.GetComponent<Text>();
        if (text == null)
        {
            text = t.gameObject.AddComponent<Text>();
        }

        // フォントが未設定の場合はデフォルト（組み込み）を割り当て
        if (m_Font == null)
        {
            // Unity 2023 以降は Arial.ttf の組み込み参照が非推奨。代わりに LegacyRuntime.ttf を使用。
            m_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        text.font = m_Font;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private void ApplyAll()
    {
        if (_top == null) return;

        // 自動調整の計算（必ずしもシリアライズ値を上書きしない）
        float radiusToUse = m_Radius;
        int fontSizeToUse = m_FontSize;
        if (m_AutoAdjustByIcon && _iconRect != null)
        {
            Vector2 iconSize = _iconRect.rect.size;
            radiusToUse = Mathf.Max(0f, iconSize.x * m_RadiusRatioToIcon);
            fontSizeToUse = Mathf.Max(1, Mathf.RoundToInt(iconSize.y * m_FontSizeRatioToIcon));
        }

        // 表示内容（同じ数値を四方向に表示）
        string s = m_Number.ToString();
        _top.text = s;
        _bottom.text = s;
        _left.text = s;
        _right.text = s;

        // 見た目（フォントサイズと色）
        _top.fontSize = fontSizeToUse;
        _bottom.fontSize = fontSizeToUse;
        _left.fontSize = fontSizeToUse;
        _right.fontSize = fontSizeToUse;

        _top.color = m_Color;
        _bottom.color = m_Color;
        _left.color = m_Color;
        _right.color = m_Color;

        // 文字が収まる最小のサイズボックス（余裕を持って少し広めに）
        Vector2 box = new Vector2(Mathf.Max(24f, fontSizeToUse * 1.4f), Mathf.Max(24f, fontSizeToUse * 1.6f));
        SetSize(_top.rectTransform, box);
        SetSize(_bottom.rectTransform, box);
        SetSize(_left.rectTransform, box);
        SetSize(_right.rectTransform, box);

        // 位置（上下左右に半径分だけ配置）
        SetAnchoredPos(_top.rectTransform, new Vector2(0f, +radiusToUse));
        SetAnchoredPos(_bottom.rectTransform, new Vector2(0f, -radiusToUse));
        SetAnchoredPos(_left.rectTransform, new Vector2(-radiusToUse, 0f));
        SetAnchoredPos(_right.rectTransform, new Vector2(+radiusToUse, 0f));
    }

    private static void SetSize(RectTransform rt, Vector2 size)
    {
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
    }

    private static void SetAnchoredPos(RectTransform rt, Vector2 pos)
    {
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    // 公開 API
    public int Number
    {
        get => m_Number;
        set { m_Number = value; ApplyAll(); }
    }

    public float Radius
    {
        get => m_Radius;
        set { m_Radius = Mathf.Max(0f, value); ApplyAll(); }
    }

    public int FontSize
    {
        get => m_FontSize;
        set { m_FontSize = Mathf.Max(1, value); ApplyAll(); }
    }

    public Color Color
    {
        get => m_Color;
        set { m_Color = value; ApplyAll(); }
    }
    /// <summary>
    /// 数字部分を表示するか非表示する
    /// </summary>
    public void SetActive(bool isActive)
    {
        _top.gameObject.SetActive(isActive);
        _bottom.gameObject.SetActive(isActive);
        _left.gameObject.SetActive(isActive);
        _right.gameObject.SetActive(isActive);
    }
}
