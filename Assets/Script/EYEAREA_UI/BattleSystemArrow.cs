using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1本分のバトル矢印を描画するコンポーネント（描画は UILineRenderer に委譲）
/// 親 RectTransform は自動でいじりません（manager側で設定）。  
/// BuildFromBaseStates(actor, target) を呼ぶと、
/// - 方向は actor.Icon 中心 → target.Icon 中心（傾きは従来通り）
/// - 長さは固定長 FixedLength
/// - 配置は 2 点の中点を中心に対称（= 中点に矢印の中心が来る）
/// - 自己対象（actor==target）の場合は円＋接線方向の小さな矢じりを描画
/// アイコン内への侵入制約は設けません（重なり可）。
/// </summary>
[DisallowMultipleComponent]
public class BattleSystemArrow : MonoBehaviour
{
    [Header("参照（未設定なら自動付与）")]
    [Tooltip("矢印描画に使用する UILineRenderer。未指定なら自動でアタッチされます。")]
    [SerializeField] private UILineRenderer _line;
    [Tooltip("透明度(α)制御用の CanvasGroup。未指定なら自動でアタッチされます。")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("見た目（線の太さ・矢じり形状）")]
    [Tooltip("パーセント適用用の最小太さ（ピクセル）。SetThicknessFromPercentで使用します")]
    [SerializeField] private float _thicknessMin = 4f;
    [Tooltip("パーセント適用用の最大太さ（ピクセル）。SetThicknessFromPercentで使用します")]
    [SerializeField] private float _thicknessMax = 14f;
    [Tooltip("矢じりの長さ（ピクセル）。大きいほど矢じりが長くなります。")]
    [SerializeField] private float _headLength = 36f;
    [Tooltip("矢じりの開き角（度）。小さいほど鋭い形になります。")]
    [SerializeField] private float _headAngleDeg = 28f;

    [Header("固定長・中点配置")]
    [Tooltip("画面ピクセル基準の固定長。矢印はこの長さで、中点をアイコン間の中点に合わせて描画します。")]
    [SerializeField] private float _fixedLength = 180f;
    

    [Header("自己対象時（円形矢印）")]
    [Tooltip("自己対象時に描く円の半径（ピクセル）。")]
    [SerializeField] private float _circleRadiusSelf = 40f;
    [Tooltip("円の接線方向に付ける矢じりの長さ（ピクセル）。")]
    [SerializeField] private float _circleHeadLength = 16f;
    [Tooltip("円の矢じりの開き角（度）。")]
    [SerializeField] private float _circleHeadAngleDeg = 28f;

    [Header("色は外部適用（ステージテーマ/Managerから適用。Prefabでは編集しない）")]
    [System.NonSerialized] private Color _lineColor = Color.magenta;
    [System.NonSerialized] private Color _subColor = Color.magenta;

    [Header("ランダムノイズ（生成時のみ適用）")]
    [Tooltip("傾き（度）の最大揺れ幅。±この値の範囲でランダム回転します。0で無効。")]
    [SerializeField] private float _angleNoiseDeg = 2f;
    [Tooltip("全長（px）の最大揺れ幅。±この値の範囲でランダム。0で無効。")]
    [SerializeField] private float _lengthNoisePx = 8f;
    [Tooltip("中点からの位置オフセット（px）。各軸について±この値の範囲でランダム。0で無効。")]
    [SerializeField] private Vector2 _midOffsetNoiseXY = new Vector2(6f, 6f);
    [Tooltip("矢じり長さ（px）の最大揺れ幅。±この値の範囲でランダム。角度は固定。0で無効。")]
    [SerializeField] private float _headLengthNoisePx = 4f;

    [Tooltip("自己対象（円）時の中心オフセット（px）。各軸について±この値の範囲でランダム。0で無効。")]
    [SerializeField] private Vector2 _selfCenterNoiseXY = new Vector2(4f, 4f);
    [Tooltip("自己対象（円）時の矢じり長さノイズ（px）。±この値の範囲でランダム。0で無効。")]
    [SerializeField] private float _selfHeadLengthNoisePx = 3f;

    [Header("デバッグ")]
    [Tooltip("内部計算や配置の詳細ログを出力します。")]
    [SerializeField] private bool _debugLog = false;

    private const float EPS = 1e-6f;

    // --- 生成時ノイズの永続化（同じactor/targetペアの間では固定） ---
    private BaseStates _lastActor;
    private BaseStates _lastTarget;
    private bool _hasJitter = false;
    private float _jAngleDeg = 0f;
    private float _jLengthPx = 0f;
    private Vector2 _jMidOffset = Vector2.zero;
    private float _jHeadLengthPx = 0f;
    private Vector2 _jSelfCenterOffset = Vector2.zero;
    private float _jSelfHeadLengthPx = 0f;

    private void Awake()
    {
        EnsureComponents();
        ApplyDefaultsToLine();
    }

    private void Reset()
    {
        EnsureComponents();
        ApplyDefaultsToLine();
    }

    private void OnValidate()
    {
        // 値の妥当化
        if (_thicknessMin < 0f) _thicknessMin = 0f;
        if (_thicknessMax < 0f) _thicknessMax = 0f;
        if (_thicknessMax < _thicknessMin) _thicknessMax = _thicknessMin;
        if (_headLength < 0f) _headLength = 0f;
        _headAngleDeg = Mathf.Clamp(_headAngleDeg, 0f, 89f);
        if (_fixedLength < 0f) _fixedLength = 0f;
        

        if (_circleRadiusSelf < 0f) _circleRadiusSelf = 0f;
        if (_circleHeadLength < 0f) _circleHeadLength = 0f;
        _circleHeadAngleDeg = Mathf.Clamp(_circleHeadAngleDeg, 0f, 89f);

        if (_angleNoiseDeg < 0f) _angleNoiseDeg = 0f;
        if (_lengthNoisePx < 0f) _lengthNoisePx = 0f;
        _midOffsetNoiseXY = new Vector2(Mathf.Max(0f, _midOffsetNoiseXY.x), Mathf.Max(0f, _midOffsetNoiseXY.y));
        if (_headLengthNoisePx < 0f) _headLengthNoisePx = 0f;
        _selfCenterNoiseXY = new Vector2(Mathf.Max(0f, _selfCenterNoiseXY.x), Mathf.Max(0f, _selfCenterNoiseXY.y));
        if (_selfHeadLengthNoisePx < 0f) _selfHeadLengthNoisePx = 0f;

#if UNITY_EDITOR
        // エディタ上で見た目を即時反映
        if (!Application.isPlaying)
        {
            EnsureComponents();
            // エディタ時も常にブロックしない
            if (_line != null) _line.raycastTarget = false;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
            ApplyDefaultsToLine();
            if (_line != null) _line.SetAllDirty();
        }
#endif
    }

    private void OnEnable()
    {
        // プールからの再有効化や CanvasRenderer 再アタッチ後などに確実に再描画させる
        EnsureComponents();
        // この描画は入力を受けない（背面のUI操作を遮らない）
        if (_line != null) _line.raycastTarget = false;
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
        if (_line != null) _line.SetAllDirty();
    }

    private void EnsureComponents()
    {
        if (_line == null)
        {
            _line = GetComponent<UILineRenderer>();
            if (_line == null) _line = gameObject.AddComponent<UILineRenderer>();
        }
        // CanvasRenderer が外されているケースに備えて確保
        var cr = GetComponent<CanvasRenderer>();
        if (cr == null)
        {
            cr = gameObject.AddComponent<CanvasRenderer>();
        }
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 念押しでレイキャストを遮らない設定を適用
        if (_line != null) _line.raycastTarget = false;
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
    }

    private void ApplyDefaultsToLine()
    {
        // 既定は最小太さにしておく（パーセント適用で上書きされる想定）
        _line.thickness = _thicknessMin;
        _line.lineColor = _lineColor;
        _line.two = _subColor;

        // 演出揺れはデフォルト無効（狙い位置に正確に描画）
        _line.shakeAmplitude = 0f;
        _line.shakeFrequency = 0f;
        _line.sideObjectType = SideObjectType.Normal;
    }

    public void Clear()
    {
        EnsureComponents();
        _line.lines.Clear();
        _line.circles.Clear();
        _line.SetVerticesDirty();
        // 矢印の表示をやめるときにジッターも破棄（次の別ペアで再生成）
        _hasJitter = false;
        _lastActor = null;
        _lastTarget = null;
    }

    public void SetAlpha(float a)
    {
        if (_canvasGroup == null) EnsureComponents();
        _canvasGroup.alpha = Mathf.Clamp01(a);
    }

    public void SetVisual(float? thickness = null, Color? colorMain = null, Color? colorSub = null, float? headLength = null, float? headAngleDeg = null)
    {
        EnsureComponents();
        if (thickness.HasValue) { _line.thickness = thickness.Value; }
        if (colorMain.HasValue) { _lineColor = colorMain.Value; _line.lineColor = _lineColor; }
        if (colorSub.HasValue) { _subColor = colorSub.Value; _line.two = _subColor; }
        if (headLength.HasValue) { _headLength = headLength.Value; }
        if (headAngleDeg.HasValue) { _headAngleDeg = headAngleDeg.Value; }
        _line.SetVerticesDirty();
    }

    /// <summary>
    /// 0〜1 のパーセントに基づき、太さを可動域内で適用する。
    /// </summary>
    public void SetThicknessFromPercent(float percent01)
    {
        EnsureComponents();
        float p = Mathf.Clamp01(percent01);
        var t = Mathf.Lerp(_thicknessMin, _thicknessMax, p);
        _line.thickness = t;
        _line.SetVerticesDirty();
    }

    /// <summary>
    /// actor から target へ矢印を構築。
    /// parent は描画座標の参照親（省略時は自分の RectTransform 基準）。
    /// </summary>
    public void BuildFromBaseStates(BaseStates actor, BaseStates target, RectTransform parent = null)
    {
        EnsureComponents();
        // 描画データのみクリア（ジッターは保持）
        _line.lines.Clear();
        _line.circles.Clear();
        _line.SetVerticesDirty();

        if (actor == null || target == null || actor.UI == null || target.UI == null || actor.UI.Icon == null || target.UI.Icon == null)
        {
            Debug.LogError("[BattleSystemArrow] BuildFromBaseStates: 必要参照が null です。");
            return;
        }

        var myRect = _line.rectTransform;                 // 描画はこれのローカル座標で行う
        var space = parent != null ? parent : myRect;     // 参照空間（親は外部で決める前提）

        if (_debugLog)
        {
            var canvas = space != null ? space.GetComponentInParent<Canvas>() : null;
            Debug.Log($"[Arrow] Build start: actor={(actor != null ? actor.ToString() : "null")}, target={(target != null ? target.ToString() : "null")}, myRect={myRect?.name}, space={space?.name}, spaceCanvas={canvas?.name ?? "null"}");
        }

        // 画面座標（ScreenPoint）で中心位置を取得（Canvas/eventCamera 差異を吸収）
        Vector2 startScreen = GetRectScreenCenter(actor.UI.Icon.rectTransform);
        Vector2 targetCenterScreen = GetRectScreenCenter(target.UI.Icon.rectTransform);

        // 自己対象（円）も含めて、ペアごとのジッターを事前に確定
        EnsureJitter(actor, target);

        if (actor == target)
        {
            if (_debugLog)
            {
                Debug.Log($"[Arrow] SelfTarget: centerScreen={startScreen}");
            }
            BuildCircleArrow(startScreen, space, myRect);
            _line.SetVerticesDirty();
            return;
        }

        // 新仕様: 固定長・中点配置（方向は actor→target）
        Vector2 dirScreen = (targetCenterScreen - startScreen);
        float distToCenter = dirScreen.magnitude;
        if (distToCenter < EPS)
        {
            if (_debugLog)
            {
                Debug.Log("[Arrow] distToCenter < EPS -> Circle fallback");
            }
            // ほぼ同一点 -> 円描画にフォールバック
            BuildCircleArrow(startScreen, space, myRect);
            _line.SetVerticesDirty();
            return;
        }
        dirScreen /= distToCenter;

        // 生成時ランダムノイズ（同一ペアでは固定）
        EnsureJitter(actor, target);
        float angleJitter = _jAngleDeg;
        float lengthJitter = _jLengthPx;
        Vector2 midJitter = _jMidOffset;
        float headLenJitter = _jHeadLengthPx;

        // 傾きノイズ適用
        if (Mathf.Abs(angleJitter) > EPS)
        {
            dirScreen = RotateDeg(dirScreen, angleJitter).normalized;
        }

        // 長さノイズ適用
        float halfLen = Mathf.Max(0f, _fixedLength + lengthJitter) * 0.5f;

        // 中点からの位置ノイズ適用
        Vector2 midScreen = (startScreen + targetCenterScreen) * 0.5f + midJitter;
        Vector2 startScreenFixed = midScreen - dirScreen * halfLen;
        Vector2 endScreen = midScreen + dirScreen * halfLen;

        // スクリーン -> 自ローカル（描画座標）に変換
        var myCanvas = myRect.GetComponentInParent<Canvas>();
        var myCam = myCanvas != null ? myCanvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(myRect, startScreenFixed, myCam, out Vector2 startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(myRect, endScreen, myCam, out Vector2 endLocal);

        if (_debugLog)
        {
            var rect = myRect.rect;
            Debug.Log($"[Arrow] Local(Fixed): start={startLocal}, end={endLocal}, length={(endLocal - startLocal).magnitude}, myRectSize=({rect.width},{rect.height})");
        }

        // 幹線
        _line.lines.Add(new UILineRenderer.LineData { startPoint = startLocal, endPoint = endLocal });

        // 矢じり（ローカル空間ベース）
        Vector2 dirLocal = (endLocal - startLocal).normalized;
        // 矢じり長さノイズ（角度は固定）
        float headLenThis = Mathf.Max(0f, _headLength + headLenJitter);
        AddArrowHead(_line.lines, endLocal, dirLocal, headLenThis, _headAngleDeg);

        // 厚み等を反映（厚みは SetThicknessFromPercent/SetVisual で外部から上書き想定）
        _line.SetVerticesDirty();
        if (_debugLog)
        {
            Debug.Log("[Arrow] Build complete: lines=" + _line.lines.Count + ", circles=" + _line.circles.Count);
        }
    }

    private void DL(string msg)
    {
        if (_debugLog) Debug.Log("[Arrow] " + msg);
    }

    public void SetDebug(bool on)
    {
        _debugLog = on;
    }

    private void BuildCircleArrow(Vector2 centerScreen, RectTransform space, RectTransform myRect)
    {
        // 自己対象専用のランダムオフセット（同一ペアでは固定）
        if (!_hasJitter)
        {
            // 予防的に生成（通常は BuildFromBaseStates 側でセット済）
            _jSelfCenterOffset = new Vector2(
                (_selfCenterNoiseXY.x > 0f) ? Random.Range(-_selfCenterNoiseXY.x, _selfCenterNoiseXY.x) : 0f,
                (_selfCenterNoiseXY.y > 0f) ? Random.Range(-_selfCenterNoiseXY.y, _selfCenterNoiseXY.y) : 0f
            );
            _jSelfHeadLengthPx = (_selfHeadLengthNoisePx > 0f) ? Random.Range(-_selfHeadLengthNoisePx, _selfHeadLengthNoisePx) : 0f;
            _hasJitter = true;
        }
        Vector2 centerJitter = _jSelfCenterOffset;

        // 円の中心（ローカル）: ScreenPoint -> Local 変換
        var myCanvas = myRect.GetComponentInParent<Canvas>();
        var cam = myCanvas != null ? myCanvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(myRect, centerScreen + centerJitter, cam, out Vector2 centerLocal);
        _line.circles.Add(new UILineRenderer.CircleData
        {
            center = centerLocal,
            radius = _circleRadiusSelf
        });

        // 円の右側(0°)の接線方向（上向き）に矢じり
        Vector2 rimPointLocal = centerLocal + new Vector2(_circleRadiusSelf, 0f);
        Vector2 tangentDirLocal = new Vector2(0f, 1f); // 上方向（反時計回り接線）

        // 自己対象時の矢じり長さノイズ
        float selfHeadLenThis = Mathf.Max(0f, _circleHeadLength + _jSelfHeadLengthPx);
        AddArrowHead(_line.lines, rimPointLocal, tangentDirLocal, selfHeadLenThis, _circleHeadAngleDeg);
    }

    private void EnsureJitter(BaseStates actor, BaseStates target)
    {
        // ペアが変わったらジッター再生成
        if (!_hasJitter || actor != _lastActor || target != _lastTarget)
        {
            _jAngleDeg = (_angleNoiseDeg > 0f) ? Random.Range(-_angleNoiseDeg, _angleNoiseDeg) : 0f;
            _jLengthPx = (_lengthNoisePx > 0f) ? Random.Range(-_lengthNoisePx, _lengthNoisePx) : 0f;
            _jMidOffset = new Vector2(
                (_midOffsetNoiseXY.x > 0f) ? Random.Range(-_midOffsetNoiseXY.x, _midOffsetNoiseXY.x) : 0f,
                (_midOffsetNoiseXY.y > 0f) ? Random.Range(-_midOffsetNoiseXY.y, _midOffsetNoiseXY.y) : 0f
            );
            _jHeadLengthPx = (_headLengthNoisePx > 0f) ? Random.Range(-_headLengthNoisePx, _headLengthNoisePx) : 0f;

            // 自己対象（円）用
            _jSelfCenterOffset = new Vector2(
                (_selfCenterNoiseXY.x > 0f) ? Random.Range(-_selfCenterNoiseXY.x, _selfCenterNoiseXY.x) : 0f,
                (_selfCenterNoiseXY.y > 0f) ? Random.Range(-_selfCenterNoiseXY.y, _selfCenterNoiseXY.y) : 0f
            );
            _jSelfHeadLengthPx = (_selfHeadLengthNoisePx > 0f) ? Random.Range(-_selfHeadLengthNoisePx, _selfHeadLengthNoisePx) : 0f;

            _lastActor = actor;
            _lastTarget = target;
            _hasJitter = true;
        }
    }

    private static Vector2 GetRectScreenCenter(RectTransform rt)
    {
        var canvas = rt != null ? rt.GetComponentInParent<Canvas>() : null;
        var cam = canvas != null ? canvas.worldCamera : null;
        Vector3 world = rt.TransformPoint(rt.rect.center);
        return RectTransformUtility.WorldToScreenPoint(cam, world);
    }

    private static void GetScreenAABB(RectTransform rt, out Vector2 min, out Vector2 max)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        var canvas = rt != null ? rt.GetComponentInParent<Canvas>() : null;
        var cam = canvas != null ? canvas.worldCamera : null;
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            var sp = (Vector2)RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            if (sp.x < min.x) min.x = sp.x;
            if (sp.y < min.y) min.y = sp.y;
            if (sp.x > max.x) max.x = sp.x;
            if (sp.y > max.y) max.y = sp.y;
        }
    }

    private static bool PointInsideAABB(Vector2 p, Vector2 min, Vector2 max)
    {
        return (p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y);
    }

    /// <summary>
    /// 2D レイと AABB の交差（正方向最初の tEnter を返す）。ヒットしなければ false。
    /// </summary>
    private static bool IntersectRayAABB(Vector2 origin, Vector2 dir, Vector2 min, Vector2 max, out float tEnter)
    {
        tEnter = 0f;
        float tExit = float.PositiveInfinity;

        // X
        if (Mathf.Abs(dir.x) < EPS)
        {
            if (origin.x < min.x || origin.x > max.x) return false;
        }
        else
        {
            float tx1 = (min.x - origin.x) / dir.x;
            float tx2 = (max.x - origin.x) / dir.x;
            if (tx1 > tx2) (tx1, tx2) = (tx2, tx1);
            tEnter = Mathf.Max(tEnter, tx1);
            tExit = Mathf.Min(tExit, tx2);
        }

        // Y
        if (Mathf.Abs(dir.y) < EPS)
        {
            if (origin.y < min.y || origin.y > max.y) return false;
        }
        else
        {
            float ty1 = (min.y - origin.y) / dir.y;
            float ty2 = (max.y - origin.y) / dir.y;
            if (ty1 > ty2) (ty1, ty2) = (ty2, ty1);
            tEnter = Mathf.Max(tEnter, ty1);
            tExit = Mathf.Min(tExit, ty2);
        }

        if (tExit < 0f) return false;      // 全体が負方向
        if (tEnter > tExit) return false;  // 交差しない
        if (tEnter < 0f) tEnter = 0f;      // 原点が手前側の場合は 0 にクランプ
        return true;
    }

    private static Vector2 RotateDeg(Vector2 v, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private static void AddArrowHead(List<UILineRenderer.LineData> lines, Vector2 end, Vector2 dir, float headLen, float headAngleDeg)
    {
        Vector2 back = -dir;
        Vector2 left = RotateDeg(back, +headAngleDeg) * headLen;
        Vector2 right = RotateDeg(back, -headAngleDeg) * headLen;

        lines.Add(new UILineRenderer.LineData { startPoint = end, endPoint = end + left });
        lines.Add(new UILineRenderer.LineData { startPoint = end, endPoint = end + right });
    }
}