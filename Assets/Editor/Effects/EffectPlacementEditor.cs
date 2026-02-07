using System;
using System.Collections.Generic;
using System.IO;
using Effects;
using Effects.Core;
using Effects.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace EffectsEditor
{
    /// <summary>
    /// エフェクト配置エディタ（v2）。
    /// キャンバス上でicon_rectをドラッグ操作し、実機プレビューで表示結果を確認できる。
    ///
    /// 操作モデル:
    /// - キャンバス: エフェクトを表示する固定領域
    /// - アイコン参照: 中央に表示される非操作の参照矩形（スライダーでサイズ変更可）
    /// - icon_rect: 自由にドラッグ/リサイズできる緑の矩形。アイコンに対応するキャンバス領域を定義
    /// </summary>
    public class EffectPlacementEditor : EditorWindow
    {
        #region State

        private string[] _effectFiles = Array.Empty<string>();
        // SerializeFieldでリコンパイル後も選択状態を保持
        [SerializeField] private int _selectedIndex = -1;
        [SerializeField] private string _selectedEffectName;

        // 非シリアライズ（リコンパイル時にnull→OnEnableで自動再読み込み）
        private EffectDefinition _definition;
        private EffectRenderer _renderer;
        private Texture2D _previewTexture;

        // Placement（SerializeFieldでリコンパイル後も値を保持）
        [SerializeField] private int _targetMode; // 0=icon, 1=field
        [SerializeField] private float _iconRectX, _iconRectY, _iconRectW, _iconRectH;

        // Playback
        private int _currentFrame;
        private bool _isPlaying;
        [SerializeField] private bool _loop = true;
        [SerializeField] private float _playbackSpeed = 1f;
        private double _lastFrameTime;

        // Display（SerializeFieldでリコンパイル後も値を保持）
        [SerializeField] private int _canvasPreviewSize = 300;
        [SerializeField] private int _resultPreviewSize = 300;
        [SerializeField] private float _iconRefRatio = 0.4f;
        [SerializeField] private int _mockIconPreset;
        [SerializeField] private float _resultZoom = 1f;

        // Drag
        private enum DragMode { None, Move, ResizeTL, ResizeTR, ResizeBL, ResizeBR }
        private DragMode _dragMode;
        private Vector2 _dragStart;
        private Rect _dragStartRect;

        private Vector2 _scrollPos;
        [SerializeField] private bool _isDirty;

        #endregion

        #region Constants

        private static readonly string[] TargetOptions = { "icon", "field" };

        private static readonly Color IconRefFill = new Color(0.4f, 0.4f, 0.5f, 0.15f);
        private static readonly Color IconRefBorder = new Color(0.6f, 0.6f, 0.7f, 0.5f);
        private static readonly Color PlaceColor = new Color(0.2f, 1f, 0.4f, 0.9f);
        private static readonly Color PlaceFill = new Color(0.2f, 1f, 0.4f, 0.08f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color DarkBg = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color HandleFill = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color MockFill = new Color(0.3f, 0.35f, 0.4f, 0.6f);
        private static readonly Color MockBorder = new Color(0.5f, 0.55f, 0.6f, 0.8f);
        private const float HandleR = 4f;
        private const float IconRefMinRatio = 0.1f;
        private const float IconRefMaxRatio = 0.95f;

        private static readonly string[] MockNames =
        {
            "味方 (170x257)", "小敵 (100x100)", "中敵 (200x200)", "大敵 (400x300)"
        };
        private static readonly Vector2[] MockSizes =
        {
            new Vector2(170, 257), new Vector2(100, 100), new Vector2(200, 200), new Vector2(400, 300)
        };

        #endregion

        [MenuItem("Window/Effects/Effect Placement Editor %#p")]
        public static void ShowWindow()
        {
            var w = GetWindow<EffectPlacementEditor>();
            w.titleContent = new GUIContent("Effect Placement");
            w.minSize = new Vector2(420, 700);
            w.Show();
        }

        private void OnEnable()
        {
            RefreshEffectList();
            EditorApplication.update += OnEditorUpdate;

            // リコンパイル後の自動復元:
            // _selectedEffectNameはSerializeFieldなので残るが、
            // _definitionは非シリアライズなのでnullになる → 再読み込み
            if (!string.IsNullOrEmpty(_selectedEffectName) && _definition == null)
            {
                // 未保存の配置変更がある場合はplacement上書きをスキップ
                bool hadDirty = _isDirty;
                float sx = _iconRectX, sy = _iconRectY, sw = _iconRectW, sh = _iconRectH;
                int st = _targetMode;

                LoadEffect(_selectedEffectName);

                if (hadDirty)
                {
                    // 未保存の編集値を復元
                    _iconRectX = sx; _iconRectY = sy; _iconRectW = sw; _iconRectH = sh;
                    _targetMode = st;
                    _isDirty = true;
                }
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupTexture();
        }

        private void OnEditorUpdate()
        {
            if (!_isPlaying || _definition == null || _definition.Frames.Count == 0) return;
            double now = EditorApplication.timeSinceStartup;
            var frame = _definition.Frames[_currentFrame];
            float dur = (frame.Duration > 0 ? frame.Duration : 1f / _definition.Fps) / _playbackSpeed;
            if (now - _lastFrameTime >= dur)
            {
                _lastFrameTime = now;
                AdvanceFrame();
                RenderCurrentFrame();
                Repaint();
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawEffectSelector();
            EditorGUILayout.Space(6);

            if (_definition != null)
            {
                DrawTargetMode();
                EditorGUILayout.Space(6);

                if (_targetMode == 0)
                {
                    DrawCanvasPreview();
                    EditorGUILayout.Space(4);
                    DrawPlacementControls();
                    EditorGUILayout.Space(8);
                    DrawResultPreview();
                }
                else
                {
                    DrawFieldPreview();
                }

                EditorGUILayout.Space(8);
                DrawPlaybackControls();
            }

            EditorGUILayout.Space(8);
            DrawSaveButton();

            EditorGUILayout.EndScrollView();
        }

        #region Effect Selector

        private void DrawEffectSelector()
        {
            EditorGUILayout.LabelField("エフェクト選択", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Effect:", GUILayout.Width(50));
                EditorGUI.BeginChangeCheck();
                int idx = EditorGUILayout.Popup(_selectedIndex, _effectFiles);
                if (EditorGUI.EndChangeCheck() && idx != _selectedIndex)
                {
                    if (_isDirty &&
                        !EditorUtility.DisplayDialog("未保存の変更", "変更を破棄しますか？", "破棄", "キャンセル"))
                    {
                        // cancelled
                    }
                    else
                    {
                        _selectedIndex = idx;
                        if (_selectedIndex >= 0 && _selectedIndex < _effectFiles.Length)
                            LoadEffect(_effectFiles[_selectedIndex]);
                    }
                }

                if (GUILayout.Button("↻", GUILayout.Width(24)))
                {
                    if (!string.IsNullOrEmpty(_selectedEffectName)) LoadEffect(_selectedEffectName);
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(55)))
                    RefreshEffectList();
            }
        }

        #endregion

        #region Target Mode

        private void DrawTargetMode()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Target:", GUILayout.Width(50));
                EditorGUI.BeginChangeCheck();
                _targetMode = EditorGUILayout.Popup(_targetMode, TargetOptions, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck()) _isDirty = true;
            }
        }

        #endregion

        #region Canvas Preview (icon mode)

        private void DrawCanvasPreview()
        {
            EditorGUILayout.LabelField("キャンバスプレビュー", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "緑の矩形（icon_rect）をドラッグで移動、四隅ハンドルでリサイズ。\n" +
                "明るい領域 = アイコンに対応する部分。暗い領域 = アイコン外にはみ出す部分。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("表示サイズ:", GUILayout.Width(65));
                _canvasPreviewSize = EditorGUILayout.IntSlider(_canvasPreviewSize, 150, 500);
            }

            float size = _canvasPreviewSize;
            int canvas = _definition.Canvas;

            // Reserve space (extra 18px for label below)
            Rect reserved = GUILayoutUtility.GetRect(size, size + 18, GUILayout.ExpandWidth(false));
            float cx = (EditorGUIUtility.currentViewWidth - size) / 2;
            Rect texRect = new Rect(cx, reserved.y, size, size);

            // Background
            EditorGUI.DrawRect(texRect, DarkBg);

            // Effect texture
            if (_previewTexture != null)
                GUI.DrawTexture(texRect, _previewTexture, ScaleMode.StretchToFill);

            float ppu = size / canvas; // pixels per unit

            // icon_rect in screen coords
            Rect placeScreen = new Rect(
                texRect.x + _iconRectX * ppu,
                texRect.y + _iconRectY * ppu,
                _iconRectW * ppu,
                _iconRectH * ppu
            );

            // Dim outside icon_rect
            DrawDimmedOutside(texRect, placeScreen);

            // Icon reference (centered, non-interactive)
            float refSize = size * _iconRefRatio;
            Rect iconRef = new Rect(
                texRect.x + (size - refSize) / 2,
                texRect.y + (size - refSize) / 2,
                refSize, refSize
            );
            EditorGUI.DrawRect(iconRef, IconRefFill);
            DrawBorder(iconRef, IconRefBorder, 1f);
            GUI.Label(
                new Rect(iconRef.x + 2, iconRef.y + 1, 80, 14),
                "アイコン参照",
                MiniLabel(IconRefBorder));

            // icon_rect overlay
            EditorGUI.DrawRect(placeScreen, PlaceFill);
            DrawBorder(placeScreen, PlaceColor, 2f);

            // Corner handles
            DrawHandle(placeScreen.x, placeScreen.y);
            DrawHandle(placeScreen.xMax, placeScreen.y);
            DrawHandle(placeScreen.x, placeScreen.yMax);
            DrawHandle(placeScreen.xMax, placeScreen.yMax);

            // Label
            GUI.Label(
                new Rect(placeScreen.x, placeScreen.yMax + 2, 260, 14),
                $"icon_rect ({_iconRectX:F0}, {_iconRectY:F0}, {_iconRectW:F0}, {_iconRectH:F0})",
                MiniLabel(PlaceColor));

            // Drag interaction
            HandleDrag(texRect, canvas, ppu);

            // Icon ref slider
            EditorGUILayout.Space(18);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("アイコン参照サイズ:", GUILayout.Width(110));
                _iconRefRatio = EditorGUILayout.Slider(_iconRefRatio, IconRefMinRatio, IconRefMaxRatio);
            }
        }

        private static void DrawHandle(float hx, float hy)
        {
            Rect r = new Rect(hx - HandleR, hy - HandleR, HandleR * 2, HandleR * 2);
            EditorGUI.DrawRect(r, HandleFill);
            DrawBorder(r, PlaceColor, 1f);
        }

        private void HandleDrag(Rect texRect, int canvas, float ppu)
        {
            Event e = Event.current;
            int ctrlId = GUIUtility.GetControlID(FocusType.Passive);

            Rect ps = new Rect(
                texRect.x + _iconRectX * ppu,
                texRect.y + _iconRectY * ppu,
                _iconRectW * ppu,
                _iconRectH * ppu
            );

            float hr = HandleR + 2;
            Rect hTL = CenteredRect(ps.x, ps.y, hr);
            Rect hTR = CenteredRect(ps.xMax, ps.y, hr);
            Rect hBL = CenteredRect(ps.x, ps.yMax, hr);
            Rect hBR = CenteredRect(ps.xMax, ps.yMax, hr);

            EditorGUIUtility.AddCursorRect(hTL, MouseCursor.ResizeUpLeft);
            EditorGUIUtility.AddCursorRect(hBR, MouseCursor.ResizeUpLeft);
            EditorGUIUtility.AddCursorRect(hTR, MouseCursor.ResizeUpRight);
            EditorGUIUtility.AddCursorRect(hBL, MouseCursor.ResizeUpRight);
            if (_dragMode == DragMode.None)
                EditorGUIUtility.AddCursorRect(ps, MouseCursor.MoveArrow);

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                {
                    DragMode mode = DragMode.None;
                    if (hTL.Contains(e.mousePosition)) mode = DragMode.ResizeTL;
                    else if (hTR.Contains(e.mousePosition)) mode = DragMode.ResizeTR;
                    else if (hBL.Contains(e.mousePosition)) mode = DragMode.ResizeBL;
                    else if (hBR.Contains(e.mousePosition)) mode = DragMode.ResizeBR;
                    else if (ps.Contains(e.mousePosition)) mode = DragMode.Move;

                    if (mode != DragMode.None)
                    {
                        _dragMode = mode;
                        _dragStart = e.mousePosition;
                        _dragStartRect = new Rect(_iconRectX, _iconRectY, _iconRectW, _iconRectH);
                        GUIUtility.hotControl = ctrlId;
                        e.Use();
                    }
                    break;
                }

                case EventType.MouseDrag when _dragMode != DragMode.None:
                {
                    Vector2 delta = e.mousePosition - _dragStart;
                    ApplyDrag(delta.x / ppu, delta.y / ppu, canvas);
                    _isDirty = true;
                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.MouseUp when _dragMode != DragMode.None:
                    _dragMode = DragMode.None;
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
            }
        }

        private void ApplyDrag(float dx, float dy, int canvas)
        {
            var s = _dragStartRect;
            switch (_dragMode)
            {
                case DragMode.Move:
                    _iconRectX = Mathf.Clamp(s.x + dx, 0, canvas - s.width);
                    _iconRectY = Mathf.Clamp(s.y + dy, 0, canvas - s.height);
                    break;

                case DragMode.ResizeTL:
                {
                    float nx = Mathf.Clamp(s.x + dx, 0, s.xMax - 1);
                    float ny = Mathf.Clamp(s.y + dy, 0, s.yMax - 1);
                    _iconRectX = nx;
                    _iconRectY = ny;
                    _iconRectW = s.xMax - nx;
                    _iconRectH = s.yMax - ny;
                    break;
                }

                case DragMode.ResizeTR:
                {
                    float ny = Mathf.Clamp(s.y + dy, 0, s.yMax - 1);
                    _iconRectY = ny;
                    _iconRectW = Mathf.Clamp(s.width + dx, 1, canvas - s.x);
                    _iconRectH = s.yMax - ny;
                    break;
                }

                case DragMode.ResizeBL:
                {
                    float nx = Mathf.Clamp(s.x + dx, 0, s.xMax - 1);
                    _iconRectX = nx;
                    _iconRectW = s.xMax - nx;
                    _iconRectH = Mathf.Clamp(s.height + dy, 1, canvas - s.y);
                    break;
                }

                case DragMode.ResizeBR:
                    _iconRectW = Mathf.Clamp(s.width + dx, 1, canvas - s.x);
                    _iconRectH = Mathf.Clamp(s.height + dy, 1, canvas - s.y);
                    break;
            }
        }

        private static void DrawDimmedOutside(Rect outer, Rect inner)
        {
            float iL = Mathf.Max(inner.x, outer.x);
            float iT = Mathf.Max(inner.y, outer.y);
            float iR = Mathf.Min(inner.xMax, outer.xMax);
            float iB = Mathf.Min(inner.yMax, outer.yMax);

            if (iR <= iL || iB <= iT)
            {
                EditorGUI.DrawRect(outer, DimColor);
                return;
            }

            if (iT > outer.y)
                EditorGUI.DrawRect(new Rect(outer.x, outer.y, outer.width, iT - outer.y), DimColor);
            if (iB < outer.yMax)
                EditorGUI.DrawRect(new Rect(outer.x, iB, outer.width, outer.yMax - iB), DimColor);
            if (iL > outer.x)
                EditorGUI.DrawRect(new Rect(outer.x, iT, iL - outer.x, iB - iT), DimColor);
            if (iR < outer.xMax)
                EditorGUI.DrawRect(new Rect(iR, iT, outer.xMax - iR, iB - iT), DimColor);
        }

        #endregion

        #region Placement Controls

        private void DrawPlacementControls()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("icon_rect 数値入力", EditorStyles.miniBoldLabel);
                int canvas = _definition.Canvas;

                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("X", GUILayout.Width(14));
                    _iconRectX = EditorGUILayout.FloatField(_iconRectX, GUILayout.Width(50));
                    GUILayout.Label("Y", GUILayout.Width(14));
                    _iconRectY = EditorGUILayout.FloatField(_iconRectY, GUILayout.Width(50));
                    GUILayout.Label("W", GUILayout.Width(14));
                    _iconRectW = EditorGUILayout.FloatField(_iconRectW, GUILayout.Width(50));
                    GUILayout.Label("H", GUILayout.Width(14));
                    _iconRectH = EditorGUILayout.FloatField(_iconRectH, GUILayout.Width(50));
                }
                if (EditorGUI.EndChangeCheck())
                {
                    _iconRectX = Mathf.Clamp(_iconRectX, 0, canvas);
                    _iconRectY = Mathf.Clamp(_iconRectY, 0, canvas);
                    _iconRectW = Mathf.Clamp(_iconRectW, 1, canvas);
                    _iconRectH = Mathf.Clamp(_iconRectH, 1, canvas);
                    _isDirty = true;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("全体"))
                        SetRect(0, 0, canvas, canvas);
                    if (GUILayout.Button("中央50%"))
                    {
                        float q = canvas * 0.25f;
                        SetRect(q, q, canvas * 0.5f, canvas * 0.5f);
                    }
                    if (GUILayout.Button("中央75%"))
                    {
                        float q = canvas * 0.125f;
                        SetRect(q, q, canvas * 0.75f, canvas * 0.75f);
                    }
                    if (GUILayout.Button("中央寄せ"))
                        SetRect((canvas - _iconRectW) / 2, (canvas - _iconRectH) / 2, _iconRectW, _iconRectH);
                }
            }
        }

        private void SetRect(float x, float y, float w, float h)
        {
            _iconRectX = x; _iconRectY = y; _iconRectW = w; _iconRectH = h;
            _isDirty = true;
        }

        #endregion

        #region Result Preview

        private void DrawResultPreview()
        {
            EditorGUILayout.LabelField("実機プレビュー", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "選択アイコンサイズでの実際の表示結果。灰色 = アイコン領域。\n" +
                    "ズームで拡大すると、アイコン付近の詳細を確認できます。",
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("アイコン:", GUILayout.Width(50));
                    _mockIconPreset = EditorGUILayout.Popup(_mockIconPreset, MockNames);
                }

                int canvas = _definition.Canvas;
                Vector2 mockSize = MockSizes[_mockIconPreset];

                float irW = _iconRectW > 0 ? _iconRectW : canvas;
                float irH = _iconRectH > 0 ? _iconRectH : canvas;
                float effScale = Mathf.Min(mockSize.x / irW, mockSize.y / irH);
                float dispSize = canvas * effScale;

                float canvasC = canvas / 2f;
                float irCX = _iconRectX + irW / 2f;
                float irCY = _iconRectY + irH / 2f;
                float offX = (canvasC - irCX) * effScale;
                float offYui = (irCY - canvasC) * effScale; // Unity UI Y-up

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("表示サイズ:", GUILayout.Width(65));
                    _resultPreviewSize = EditorGUILayout.IntSlider(_resultPreviewSize, 150, 500);
                }

                float areaSize = _resultPreviewSize;

                // Calculate fit scale (zoom=1 → everything fits)
                float contentExtent = Mathf.Max(dispSize, Mathf.Max(mockSize.x, mockSize.y));
                float fitScale = (areaSize * 0.85f) / contentExtent;

                // Zoom min: icon appears at IconRefMinRatio of preview (same as canvas preview slider min)
                float iconMaxDim = Mathf.Max(mockSize.x, mockSize.y);
                float iconDisplayAtFit = iconMaxDim * fitScale;
                float zoomMin = (IconRefMinRatio * areaSize) / iconDisplayAtFit;
                zoomMin = Mathf.Min(zoomMin, 1f); // never exceed 1.0 as minimum

                _resultZoom = Mathf.Clamp(_resultZoom, zoomMin, 8f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("ズーム:", GUILayout.Width(45));
                    _resultZoom = EditorGUILayout.Slider(_resultZoom, zoomMin, 8f);
                }

                float finalScale = fitScale * _resultZoom;

                Rect area = GUILayoutUtility.GetRect(areaSize, areaSize, GUILayout.ExpandWidth(false));
                float ax = (EditorGUIUtility.currentViewWidth - areaSize) / 2;
                area = new Rect(ax, area.y, areaSize, areaSize);

                // Clip to preview area
                GUI.BeginGroup(area);

                // Background (local coords: 0,0 to areaSize,areaSize)
                EditorGUI.DrawRect(new Rect(0, 0, areaSize, areaSize), DarkBg);

                float cx = areaSize / 2;
                float cy = areaSize / 2;

                // Mock icon (centered)
                float mw = mockSize.x * finalScale;
                float mh = mockSize.y * finalScale;
                Rect mockRect = new Rect(cx - mw / 2, cy - mh / 2, mw, mh);
                EditorGUI.DrawRect(mockRect, MockFill);
                DrawBorder(mockRect, MockBorder, 1f);

                // Effect (positioned relative to icon center)
                if (_previewTexture != null)
                {
                    float ew = dispSize * finalScale;
                    float eh = dispSize * finalScale;
                    float eox = offX * finalScale;
                    float eoy = -offYui * finalScale;

                    Rect effRect = new Rect(cx - ew / 2 + eox, cy - eh / 2 + eoy, ew, eh);
                    GUI.DrawTexture(effRect, _previewTexture, ScaleMode.StretchToFill);
                }

                // Label on mock icon
                GUI.Label(mockRect, "アイコン", new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 0.5f) }
                });

                GUI.EndGroup();

                EditorGUILayout.LabelField(
                    $"スケール: {effScale:F2}x  エフェクト: {dispSize:F0}x{dispSize:F0}px  offset: ({offX:F0}, {offYui:F0})",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        #endregion

        #region Field Preview

        private void DrawFieldPreview()
        {
            EditorGUILayout.LabelField("フィールドプレビュー", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "field モード: ViewportArea 全体にエフェクトを描画。\n" +
                    "画面フラッシュ、天候、全体攻撃演出等に使用。",
                    MessageType.Info);

                float pw = _canvasPreviewSize;
                float ph = pw * 9f / 16f;
                Rect rect = GUILayoutUtility.GetRect(pw, ph, GUILayout.ExpandWidth(false));
                float cx = (EditorGUIUtility.currentViewWidth - pw) / 2;
                rect = new Rect(cx, rect.y, pw, ph);

                EditorGUI.DrawRect(rect, DarkBg);
                if (_previewTexture != null)
                    GUI.DrawTexture(rect, _previewTexture, ScaleMode.ScaleToFit);
                else
                    GUI.Label(rect, "エフェクト未読込", CenteredGrayStyle());
            }
        }

        #endregion

        #region Playback

        private void DrawPlaybackControls()
        {
            EditorGUILayout.LabelField("再生", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool hasFrames = _definition != null && _definition.Frames.Count > 0;
                int total = hasFrames ? _definition.Frames.Count : 0;

                // フレーム移動（|◀ ◀ ▶ ▶|）
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Frame:", GUILayout.Width(45));

                    GUI.enabled = hasFrames && _currentFrame > 0;
                    if (GUILayout.Button("|◀", GUILayout.Width(30)))
                        GoToFrame(0);
                    if (GUILayout.Button("◀", GUILayout.Width(30)))
                        GoToFrame(_currentFrame - 1);

                    GUI.enabled = true;
                    string frameText = hasFrames ? $" {_currentFrame + 1} / {total} " : " - / - ";
                    EditorGUILayout.LabelField(frameText, EditorStyles.boldLabel, GUILayout.Width(65));

                    GUI.enabled = hasFrames && _currentFrame < total - 1;
                    if (GUILayout.Button("▶", GUILayout.Width(30)))
                        GoToFrame(_currentFrame + 1);
                    if (GUILayout.Button("▶|", GUILayout.Width(30)))
                        GoToFrame(total - 1);

                    GUI.enabled = true;
                    GUILayout.FlexibleSpace();
                }

                // フレームスライダー
                if (hasFrames && total > 1)
                {
                    EditorGUI.BeginChangeCheck();
                    int sliderFrame = EditorGUILayout.IntSlider(_currentFrame, 0, total - 1);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _isPlaying = false;
                        GoToFrame(sliderFrame);
                    }
                }

                EditorGUILayout.Space(4);

                // 再生/停止
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUI.enabled = hasFrames;

                    if (_isPlaying)
                    {
                        if (GUILayout.Button("⏸ Pause", GUILayout.Width(70)))
                            _isPlaying = false;
                    }
                    else
                    {
                        if (GUILayout.Button("▶ Play", GUILayout.Width(70)))
                        {
                            _isPlaying = true;
                            _lastFrameTime = EditorApplication.timeSinceStartup;
                            if (_currentFrame >= total - 1)
                                GoToFrame(0);
                            else
                            {
                                RenderCurrentFrame();
                                Repaint();
                            }
                            Debug.Log($"[Placement] Play: {_selectedEffectName} frames={total} fps={_definition.Fps} tex={_previewTexture != null}");
                        }
                    }

                    if (GUILayout.Button("■ Stop", GUILayout.Width(70)))
                    {
                        _isPlaying = false;
                        GoToFrame(0);
                    }

                    GUI.enabled = true;
                    EditorGUILayout.Space(8);
                    _loop = EditorGUILayout.ToggleLeft("Loop", _loop, GUILayout.Width(50));
                    GUILayout.FlexibleSpace();
                }

                // フレーム情報
                if (hasFrames)
                {
                    var frame = _definition.Frames[_currentFrame];
                    int shapeCount = frame.Shapes?.Count ?? 0;
                    float frameTime = _currentFrame * (1f / _definition.Fps);
                    string playState = _isPlaying ? "▶" : "⏸";
                    EditorGUILayout.LabelField(
                        $"{playState}  Time: {frameTime:F2}s  |  Shapes: {shapeCount}",
                        EditorStyles.centeredGreyMiniLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Speed:", GUILayout.Width(42));
                    _playbackSpeed = EditorGUILayout.Slider(_playbackSpeed, 0.1f, 3f);
                }
            }
        }

        #endregion

        #region Save

        private void DrawSaveButton()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUI.enabled = _definition != null && _isDirty;
                var style = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fixedHeight = 28 };
                if (GUILayout.Button(_isDirty ? "Save *" : "Save", style, GUILayout.Width(100)))
                    SavePlacementData();
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }
        }

        private void SavePlacementData()
        {
            if (_definition == null || string.IsNullOrEmpty(_selectedEffectName)) return;

            string path = $"Assets/Resources/Effects/{_selectedEffectName}.json";
            if (!File.Exists(path))
            {
                Debug.LogError($"[Placement] File not found: {path}");
                return;
            }

            try
            {
                string original = File.ReadAllText(path);
                JObject jo = JObject.Parse(original);

                // target
                string target = TargetOptions[_targetMode];
                if (target == "icon") jo.Remove("target");
                else jo["target"] = target;

                // icon_rect
                if (_targetMode == 0)
                {
                    int canvas = _definition.Canvas;
                    bool isDefault = Mathf.Approximately(_iconRectX, 0) &&
                                     Mathf.Approximately(_iconRectY, 0) &&
                                     Mathf.Approximately(_iconRectW, canvas) &&
                                     Mathf.Approximately(_iconRectH, canvas);

                    if (isDefault)
                    {
                        jo.Remove("icon_rect");
                    }
                    else
                    {
                        jo["icon_rect"] = new JObject
                        {
                            ["x"] = Math.Round(_iconRectX, 1),
                            ["y"] = Math.Round(_iconRectY, 1),
                            ["width"] = Math.Round(_iconRectW, 1),
                            ["height"] = Math.Round(_iconRectH, 1)
                        };
                    }
                }
                else
                {
                    jo.Remove("icon_rect");
                }

                jo.Remove("field_layer");

                string output = jo.ToString(Formatting.Indented);
                File.WriteAllText(path, output);
                AssetDatabase.Refresh();
                _isDirty = false;
                Debug.Log($"[Placement] Saved: {_selectedEffectName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Placement] Save error: {ex.Message}");
            }
        }

        #endregion

        #region Loading & Rendering

        private void RefreshEffectList()
        {
            var list = new List<string>();
            string dir = "Assets/Resources/Effects";
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, "*.json"))
                    list.Add(Path.GetFileNameWithoutExtension(f));
            }
            _effectFiles = list.ToArray();
            if (!string.IsNullOrEmpty(_selectedEffectName))
                _selectedIndex = Array.IndexOf(_effectFiles, _selectedEffectName);
        }

        private void LoadEffect(string effectName)
        {
            _selectedEffectName = effectName;
            _isPlaying = false;
            _currentFrame = 0;
            _isDirty = false;

            string path = $"Assets/Resources/Effects/{effectName}.json";
            if (!File.Exists(path))
            {
                Debug.LogError($"[Placement] File not found: {path}");
                _definition = null;
                CleanupTexture();
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                _definition = KfxCompiler.LoadFromJson(json);

                if (_definition == null || _definition.Frames == null || _definition.Frames.Count == 0)
                {
                    Debug.LogError($"[Placement] Invalid effect: {effectName}");
                    _definition = null;
                    CleanupTexture();
                    return;
                }

                _definition.Name = effectName;
                _definition.Normalize();
                LoadPlacement();

                _renderer = new EffectRenderer(_definition.Canvas);
                CleanupTexture();
                _previewTexture = _renderer.CreateTexture();
                RenderCurrentFrame();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Placement] Load error: {ex.Message}");
                _definition = null;
                CleanupTexture();
            }
        }

        private void LoadPlacement()
        {
            _targetMode = _definition.Target == "field" ? 1 : 0;

            int canvas = _definition.Canvas;
            if (_definition.IconRect != null &&
                _definition.IconRect.Width > 0 && _definition.IconRect.Height > 0)
            {
                _iconRectX = _definition.IconRect.X;
                _iconRectY = _definition.IconRect.Y;
                _iconRectW = _definition.IconRect.Width;
                _iconRectH = _definition.IconRect.Height;
            }
            else
            {
                _iconRectX = 0;
                _iconRectY = 0;
                _iconRectW = canvas;
                _iconRectH = canvas;
            }
        }

        private void RenderCurrentFrame()
        {
            if (_definition == null || _renderer == null || _previewTexture == null) return;
            if (_currentFrame < 0 || _currentFrame >= _definition.Frames.Count) return;
            _renderer.RenderFrame(_previewTexture, _definition.Frames[_currentFrame], _currentFrame);
        }

        private void GoToFrame(int frame)
        {
            if (_definition == null || _definition.Frames.Count == 0) return;
            _currentFrame = Mathf.Clamp(frame, 0, _definition.Frames.Count - 1);
            RenderCurrentFrame();
            Repaint();
        }

        private void AdvanceFrame()
        {
            if (_definition == null || _definition.Frames.Count == 0) return;
            _currentFrame++;
            if (_currentFrame >= _definition.Frames.Count)
            {
                if (_loop) _currentFrame = 0;
                else { _currentFrame = _definition.Frames.Count - 1; _isPlaying = false; }
            }
        }

        private void CleanupTexture()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        #endregion

        #region Utility

        private static void DrawBorder(Rect r, Color c, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static Rect CenteredRect(float cx, float cy, float halfSize)
        {
            return new Rect(cx - halfSize, cy - halfSize, halfSize * 2, halfSize * 2);
        }

        private static GUIStyle MiniLabel(Color color)
        {
            return new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = color } };
        }

        private static GUIStyle CenteredGrayStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray }
            };
        }

        #endregion
    }
}
