using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// シンプルな横長ゲージ（左→右）。
/// - 親の RectTransform サイズに追従（背景・表の子Imageを自動生成）
/// - 値は 0 以上（下限クランプ）。100% を超える値も表示可能（はみ出しOK）
/// - スプライト不使用（独自SolidGraphicで単色矩形描画）
/// - アニメーションなし、即時反映
///
/// 使い方:
/// 1. 任意のUIオブジェクトに本コンポーネントを追加し、RectTransformで幅・高さを調整
/// 2. Inspectorで FillColor / BackgroundColor と Percent を設定
/// 3. スクリプトからは SetPercent(value) で更新（value は 0 以上、100 超え可）
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class StatesBannerGauge : MonoBehaviour
{
    [Header("値設定")]
    [Min(0f)]
    [SerializeField] private float m_Percent = 100f; // 0以上。100%超えも許容。

    [Header("外観設定")]
    [SerializeField] private Color m_BackgroundColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color m_FillColor = Color.white;

    [Header("子オブジェクト（自動生成）")]
    [SerializeField] private string m_BackgroundName = "Background";
    [SerializeField] private string m_FillName = "Fill";

    [SerializeField, Tooltip("参照が未割り当て時に子Imageを自動生成します")] 
    private bool m_AutoCreateChildren = true;

    [SerializeField, Tooltip("Inspectorで参照割当したい場合に使用（未設定なら自動探索/生成）")] 
    private MaskableGraphic m_BackgroundGraphic;

    [SerializeField, Tooltip("Inspectorで参照割当したい場合に使用（未設定なら自動探索/生成）")] 
    private MaskableGraphic m_FillGraphic;

    private RectTransform _rt;

    public float Percent
    {
        get => m_Percent;
        set
        {
            // 下限のみ 0 クランプ。上限はクランプしない（>100% で幅がはみ出す表示も許容）。
            float v = Mathf.Max(0f, value);
            if (!Mathf.Approximately(m_Percent, v))
            {
                m_Percent = v;
                ApplyLayout();
            }
        }
    }

    public Color BackgroundColor
    {
        get => m_BackgroundColor;
        set { m_BackgroundColor = value; ApplyColors(); }
    }

    public Color FillColor
    {
        get => m_FillColor;
        set { m_FillColor = value; ApplyColors(); }
    }

    private void Reset()
    {
        _rt = GetComponent<RectTransform>();
        // Reset中は子生成を行わない（Editor制約回避）
        ApplyAll();
    }

    private void Awake()
    {
        var image = GetComponent<Image>();//デザイン時の指標用の同オブジェクトのimageをfalseにする
        if (image != null) image.enabled = false;

        _rt = GetComponent<RectTransform>();
        // Awake中は子生成を行わない（Editor制約回避）
        ApplyAll();
    }

    private void OnEnable()
    {
        // 実行時/有効化タイミングでのみ子生成（エディタ編集中は生成しない）
        if (Application.isPlaying)
        {
            TryEnsureChildren(false);
            ApplyAll();
        }
    }

    private void OnValidate()
    {
        // Editor上での値変更即時反映
        _rt = GetComponent<RectTransform>();
        m_Percent = Mathf.Max(0f, m_Percent);
        // OnValidateでは子生成を行わない
        if (m_BackgroundGraphic != null && m_FillGraphic != null)
        {
            ApplyAll();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        // 親サイズ変更時に追従
        if (!Application.isPlaying) return; // エディタ編集中はレイアウトしない
        ApplyLayout();
    }

    /// <summary>
    /// 値（パーセント）を即時更新（アニメーションなし）。
    /// </summary>
    public void SetPercent(float value)
    {
        Percent = value; // 下限クランプ + 即時レイアウト
    }

    /// <summary>
    /// 子Graphicの有無を確認し、必要なら自動生成します。
    /// </summary>
    private void TryEnsureChildren(bool forceNew)
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();

        // 既存探索
        if (!forceNew)
        {
            if (m_BackgroundGraphic == null)
            {
                Transform bgTr = transform.Find(m_BackgroundName);
                if (bgTr != null)
                {
                    var g = bgTr.GetComponent<SolidGraphic>();
                    if (g == null)
                    {
                        // 既存にImageがある場合は無効化し、SolidGraphicを付与
                        var img = bgTr.GetComponent<Image>();
                        if (img != null) img.enabled = false;
                        g = bgTr.gameObject.AddComponent<SolidGraphic>();
                    }
                    m_BackgroundGraphic = g;
                    EnsureCanvasRenderer(m_BackgroundGraphic);
                }
            }
            if (m_FillGraphic == null)
            {
                Transform fillTr = transform.Find(m_FillName);
                if (fillTr != null)
                {
                    var g = fillTr.GetComponent<SolidGraphic>();
                    if (g == null)
                    {
                        var img = fillTr.GetComponent<Image>();
                        if (img != null) img.enabled = false;
                        g = fillTr.gameObject.AddComponent<SolidGraphic>();
                    }
                    m_FillGraphic = g;
                    EnsureCanvasRenderer(m_FillGraphic);
                }
            }
        }

        if (!m_AutoCreateChildren) return;

        // 背景
        if (m_BackgroundGraphic == null)
        {
            var go = new GameObject(m_BackgroundName, typeof(RectTransform), typeof(SolidGraphic));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero; // 親に完全追従

            m_BackgroundGraphic = go.GetComponent<SolidGraphic>();
            SetupGraphic(m_BackgroundGraphic);
            EnsureCanvasRenderer(m_BackgroundGraphic);
        }

        // 表
        if (m_FillGraphic == null)
        {
            var go = new GameObject(m_FillName, typeof(RectTransform), typeof(SolidGraphic));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f); // 左端固定 + 縦ストレッチ
            rt.pivot = new Vector2(0f, 0.5f);   // 左基点
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 0f);

            m_FillGraphic = go.GetComponent<SolidGraphic>();
            SetupGraphic(m_FillGraphic);
            EnsureCanvasRenderer(m_FillGraphic);
        }

        // 並び順（背景→表）
        m_BackgroundGraphic.transform.SetSiblingIndex(0);
        m_FillGraphic.transform.SetSiblingIndex(1);
    }

    private static void SetupGraphic(MaskableGraphic g)
    {
        if (g == null) return;
        g.raycastTarget = false;
    }

    private static void EnsureCanvasRenderer(Component c)
    {
        if (c == null) return;
        var cr = c.GetComponent<CanvasRenderer>();
        if (cr == null) c.gameObject.AddComponent<CanvasRenderer>();
    }

    private void ApplyAll()
    {
        ApplyColors();
        ApplyLayout();
    }

    private void ApplyColors()
    {
        if (m_BackgroundGraphic != null) m_BackgroundGraphic.color = m_BackgroundColor;
        if (m_FillGraphic != null) m_FillGraphic.color = m_FillColor;
    }

    private void ApplyLayout()
    {
        if (!Application.isPlaying) return; // ランタイム限定で反映
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (m_BackgroundGraphic == null || m_FillGraphic == null)
        {
            // ここでは自動生成しない（OnEnableでのみ生成）
            return;
        }

        // 背景は親にフル追従（anchors 0..1, sizeDelta 0 のまま）
        var bgRt = m_BackgroundGraphic.rectTransform;
        bgRt.anchorMin = new Vector2(0f, 0f);
        bgRt.anchorMax = new Vector2(1f, 1f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = Vector2.zero;

        // 表は左基点で幅のみ可変。100%超えはそのままはみ出し表示。
        float parentWidth = Mathf.Max(0f, _rt.rect.width);
        float fillWidth = parentWidth * (m_Percent / 100f);

        var fillRt = m_FillGraphic.rectTransform;
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = new Vector2(0f, 0f);
        fillRt.sizeDelta = new Vector2(fillWidth, 0f);

        // 並び順を保持
        m_BackgroundGraphic.transform.SetSiblingIndex(0);
        m_FillGraphic.transform.SetSiblingIndex(1);
    }
}

/// <summary>
/// スプライトを使わず単色矩形を描画するシンプルなGraphic。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class SolidGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var r = GetPixelAdjustedRect();
        var col = color;

        vh.AddVert(new Vector3(r.xMin, r.yMin), col, new Vector2(0f, 0f));
        vh.AddVert(new Vector3(r.xMin, r.yMax), col, new Vector2(0f, 1f));
        vh.AddVert(new Vector3(r.xMax, r.yMax), col, new Vector2(1f, 1f));
        vh.AddVert(new Vector3(r.xMax, r.yMin), col, new Vector2(1f, 0f));

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}
