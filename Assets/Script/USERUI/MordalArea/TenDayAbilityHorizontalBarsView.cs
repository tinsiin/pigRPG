using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 十日能力用の横棒グラフビュー。
/// - TMP の各行中心にバーを縦位置合わせ（TenDayAbility の列挙順とテキスト行順が一致している前提）
/// - 値は actor.TenDayValues(false)（通常武器補正あり・非特判）を使用
/// - 生成時に自動スケール（最大値が表示幅の約70%になる）
/// - スライダー等からユーザースケール倍率を外部指定可能（SetUserScale）
/// - テキストや軸は描画しない。バー矩形のみ。
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class TenDayAbilityHorizontalBarsView : MonoBehaviour
{
    [Header("基本設定: TMPの行中心から縦位置を決める前提の実装です")]
    [Header("左右余白(PaddingLR): バーの左開始位置と右端の余白")]
    [SerializeField, Min(0f)] private float m_PaddingLR = 8f;
    [Header("太さ(BarHeight): 棒の厚み。中心から上下に等分に膨らみます")]
    [SerializeField, Min(1f)] private float m_BarHeight = 8f;

    [Header("参照: 行中心の取得元TMP（TenDayAbility と行順を一致させる）")]
    [SerializeField] private TextMeshProUGUI m_LineText;

    [Header("Appearance: バーの色を設定します")]
    [SerializeField] private Color m_BarColor = Color.white;

    [Header("Scaling: 自動スケールとユーザースケール倍率の設定（横幅の伸縮に関与）")]
    [SerializeField, Tooltip("SetValues/Bind時に最大値が幅の70%程度になるよう自動スケーリングする")]
    private bool m_AutoScaleOnSet = true;
    [SerializeField, Range(0.1f, 0.95f), Tooltip("最大値が占有する目標幅の比率（0.7 = 70%）")]
    private float m_AutoScaleTargetRatio = 0.7f;

    

    private RectTransform _rt;
    private readonly List<MaskableGraphic> _bars = new List<MaskableGraphic>();
    private float[] _values = Array.Empty<float>();
    private float _autoScale = 1f;  // 自動スケール（幅依存）
    private float _userScale = 1f;  // ユーザー操作による倍率
    private const string BarPrefix = "Bar_";
    private bool _mismatchWarned = false;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        // 実行開始時に、エディタプレビューで生成されたバーを掃除して二重生成を防止
        if (Application.isPlaying)
        {
            CleanupEditorPreviewChildren();
            _bars.Clear();
        }
    }

    private void OnEnable()
    {
        ApplyAll();
    }

    private void OnValidate()
    {
        _rt = GetComponent<RectTransform>();
        m_PaddingLR = Mathf.Max(0f, m_PaddingLR);
        m_BarHeight = Mathf.Max(1f, m_BarHeight);
        ApplyAll();
    }

    private void OnRectTransformDimensionsChange()
    {
        // サイズ変更時はレイアウトを更新。必要に応じて自動スケールを再計算。
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (m_AutoScaleOnSet)
        {
            RecalculateAutoScale();
        }
        ApplyLayout();
    }

    public void Clear()
    {
        _values = Array.Empty<float>();
        _autoScale = 1f;
        _userScale = 1f;
        for (int i = _bars.Count - 1; i >= 0; i--)
        {
            var b = _bars[i];
            if (b != null)
            {
                if (Application.isPlaying) Destroy(b.gameObject); else DestroyImmediate(b.gameObject);
            }
        }
        _bars.Clear();
    }

    public void SetLayout(float paddingTB, float paddingLR, float spacingY, float barHeight)
    {
        // 互換API: paddingTB / spacingY は廃止。TMP行中心方式のため無視します。
        m_PaddingLR = Mathf.Max(0f, paddingLR);
        m_BarHeight = Mathf.Max(1f, barHeight);
        ApplyLayout();
    }

    public void SetColor(Color bar)
    {
        m_BarColor = bar;
        ApplyColors();
    }

    /// <summary>
    /// ユーザー操作用のスケール倍率（1=自動スケールそのまま）。
    /// </summary>
    public void SetUserScale(float scale)
    {
        _userScale = Mathf.Max(0.0001f, scale);
        ApplyLayout();
    }

    /// <summary>
    /// 任意の値配列を設定する（0以上を想定）。
    /// </summary>
    public void SetValues(float[] values)
    {
        if (values == null) values = Array.Empty<float>();
        _values = values;
        EnsureBars(_values.Length);
        if (m_AutoScaleOnSet)
        {
            RecalculateAutoScale();
        }
        ApplyAll();
    }

    /// <summary>
    /// BaseStates から十日能力（通常武器補正あり・非特判）を列挙順で取得して設定。
    /// </summary>
    public void Bind(BaseStates actor)
    {
        if (actor == null)
        {
            SetValues(Array.Empty<float>());
            return;
        }

        var dict = actor.TenDayValues(false);
        var abilities = (TenDayAbility[])Enum.GetValues(typeof(TenDayAbility));
        var vals = new float[abilities.Length];
        for (int i = 0; i < abilities.Length; i++)
        {
            float v = dict.GetValueOrZero(abilities[i]);
            vals[i] = Mathf.Abs(v); // 絶対値で表示
        }
        SetValues(vals);
    }

    private void ApplyAll()
    {
        ApplyColors();
        ApplyLayout();
    }

    private void ApplyColors()
    {
        for (int i = 0; i < _bars.Count; i++)
        {
            var g = _bars[i];
            if (g != null) g.color = m_BarColor;
        }
    }

    private void ApplyLayout()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        float width = Mathf.Max(0f, _rt.rect.width - m_PaddingLR * 2f);
        int nVals = _values.Length;
        if (nVals <= 0 || width <= 0f)
        {
            return;
        }
        // 列挙数・テキスト行数の整合チェック
        int abilityCount = ((TenDayAbility[])Enum.GetValues(typeof(TenDayAbility))).Length;
        if (nVals != abilityCount)
        {
            if (!_mismatchWarned)
            {
                Debug.LogWarning($"[TenDayAbilityHorizontalBarsView] 値配列長({nVals}) と TenDayAbility 数({abilityCount}) が一致していません。Bind/SetValues の流れを確認してください。");
                _mismatchWarned = true;
            }
            return;
        }

        if (m_LineText == null)
        {
            if (!_mismatchWarned)
            {
                Debug.LogWarning("[TenDayAbilityHorizontalBarsView] m_LineText が未割り当てです。TMP を割り当ててください。");
                _mismatchWarned = true;
            }
            return;
        }
        var textRT = m_LineText.rectTransform;
        m_LineText.ForceMeshUpdate(true, true);
        var ti = m_LineText.textInfo;
        int lineCount = (ti != null) ? ti.lineCount : 0;
        if (lineCount != abilityCount)
        {
            if (!_mismatchWarned)
            {
                Debug.LogWarning($"[TenDayAbilityHorizontalBarsView] テキスト行数({lineCount}) と TenDayAbility 数({abilityCount}) が一致していません。テキストの行数/折返しを調整してください。");
                _mismatchWarned = true;
            }
            return;
        }
        _mismatchWarned = false;

        float scale = _autoScale * _userScale;

        int n = abilityCount;
        // バー生成数を同期
        if (!Application.isPlaying)
        {
            RebuildBarsListFromChildren();
            TrimBarsToCountSafe(n);
        }
        else
        {
            EnsureBars(n);
        }

        for (int i = 0; i < n; i++)
        {
            float v = Mathf.Abs(_values[i]);
            float w = v * scale;

            var g = _bars[i];
            if (g == null) continue;
            var rt = g.rectTransform;
            // テキスト行中心をバー中心へ
            var li = ti.lineInfo[i];
            float centerYLocalText = (li.lineExtents.max.y + li.lineExtents.min.y) * 0.5f;
            Vector3 world = textRT.TransformPoint(new Vector3(0f, centerYLocalText, 0f));
            Vector3 localInBars = _rt.InverseTransformPoint(world);

            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(m_PaddingLR, localInBars.y);
            rt.sizeDelta = new Vector2(w, m_BarHeight);
        }
    }

    private void RecalculateAutoScale()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        float widthAvail = Mathf.Max(0f, _rt.rect.width - m_PaddingLR * 2f);
        float maxVal = 0f;
        for (int i = 0; i < _values.Length; i++)
        {
            float av = Mathf.Abs(_values[i]);
            if (av > maxVal) maxVal = av;
        }
        if (maxVal <= 0f || widthAvail <= 0f)
        {
            _autoScale = 1f;
        }
        else
        {
            float ratio = m_AutoScaleTargetRatio;
            _autoScale = (widthAvail * Mathf.Clamp01(ratio)) / maxVal;
        }
    }

    private void EnsureBars(int count)
    {
        if (count < 0) count = 0;
        // remove extras
        for (int i = _bars.Count - 1; i >= count; i--)
        {
            var b = _bars[i];
            _bars.RemoveAt(i);
            if (b != null)
            {
                var go = b.gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediateSafe(go); else Destroy(go);
#else
                Destroy(go);
#endif
            }
        }
        // add missing
        while (_bars.Count < count)
        {
            _bars.Add(CreateBar(_bars.Count));
        }
    }

    private MaskableGraphic CreateBar(int index)
    {
        var go = new GameObject($"{BarPrefix}{index}", typeof(RectTransform), typeof(SolidGraphic));
        go.transform.SetParent(transform, false);
        if (!Application.isPlaying)
        {
            // エディタプレビュー用の生成物はシーンに保存/ビルドに含めない
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }
        var g = go.GetComponent<SolidGraphic>();
        g.raycastTarget = false;
        EnsureCanvasRenderer(g);
        g.color = m_BarColor;
        return g;
    }

    private static void EnsureCanvasRenderer(Component c)
    {
        if (c == null) return;
        var cr = c.GetComponent<CanvasRenderer>();
        if (cr == null) c.gameObject.AddComponent<CanvasRenderer>();
    }

    /// <summary>
    /// エディタプレビューで生成されたバー（Bar_ プレフィックス）を削除します。
    /// </summary>
    private void CleanupEditorPreviewChildren()
    {
        // 子 Transform を走査し、Bar_ で始まる SolidGraphic を全削除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child == null) continue;
            if (!child.name.StartsWith(BarPrefix, StringComparison.Ordinal)) continue;
            var sg = child.GetComponent<SolidGraphic>();
            if (sg == null) continue;
            var go = child.gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediateSafe(go); else Destroy(go);
#else
            Destroy(go);
#endif
        }
    }

#if UNITY_EDITOR
    // エディタの OnValidate タイミング等での即時破棄による SerializedObject 例外を避ける
    private static void DestroyImmediateSafe(GameObject go)
    {
        if (go == null) return;
        EditorApplication.delayCall += () => { if (go != null) DestroyImmediate(go); };
    }
#endif

    // 既存の Bar_ 子から _bars リストを再構築（エディタ時）
    private void RebuildBarsListFromChildren()
    {
        _bars.Clear();
        int childCount = transform.childCount;
        var temp = new List<(int idx, MaskableGraphic g)>();
        for (int i = 0; i < childCount; i++)
        {
            var t = transform.GetChild(i);
            if (t == null) continue;
            var name = t.name;
            if (!name.StartsWith(BarPrefix, StringComparison.Ordinal)) continue;
            var sg = t.GetComponent<SolidGraphic>();
            if (sg == null) continue;
            int parsed = i;
            // 名前末尾の数値（Bar_###）を優先採用
            if (name.Length > BarPrefix.Length)
            {
                var suf = name.Substring(BarPrefix.Length);
                if (int.TryParse(suf, out int num)) parsed = num;
            }
            temp.Add((parsed, sg));
        }
        temp.Sort((a, b) => a.idx.CompareTo(b.idx));
        for (int i = 0; i < temp.Count; i++) _bars.Add(temp[i].g);
    }

    // 余剰バーを安全に削減（エディタは遅延破棄）
    private void TrimBarsToCountSafe(int count)
    {
        if (count < 0) count = 0;
        for (int i = _bars.Count - 1; i >= count; i--)
        {
            var b = _bars[i];
            _bars.RemoveAt(i);
            if (b == null) continue;
            var go = b.gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediateSafe(go); else Destroy(go);
#else
            Destroy(go);
#endif
        }
        while (_bars.Count < count)
        {
            _bars.Add(CreateBar(_bars.Count));
        }
    }
}
