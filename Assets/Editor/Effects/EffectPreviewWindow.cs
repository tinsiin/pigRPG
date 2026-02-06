using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Effects;
using Effects.Core;
using Effects.Rendering;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace EffectsEditor
{
    /// <summary>
    /// エフェクトプレビューウィンドウ
    /// エディタ内でエフェクトJSONをプレビュー・確認できる
    /// </summary>
    public class EffectPreviewWindow : EditorWindow
    {
        #region State

        // エフェクト選択
        private string[] _effectFiles = Array.Empty<string>();
        private int _selectedIndex = -1;
        private string _selectedEffectName;

        // 読み込んだデータ
        private EffectDefinition _definition;
        private EffectRenderer _renderer;
        private Texture2D _previewTexture;

        // 再生状態
        private int _currentFrame;
        private bool _isPlaying;
        private bool _loop = true;
        private float _playbackSpeed = 1f;
        private double _lastFrameTime;

        // SE再生
        private AudioClip _seClip;
        private string _loadedSeName;

        // 表示設定
        private int _previewSize = 200;
        private BackgroundType _bgType = BackgroundType.Dark;
        private Color _customBgColor = Color.gray;

        // スクロール
        private Vector2 _scrollPosition;

        #endregion

        #region Constants

        private static readonly Color DarkBgColor = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color LightBgColor = new Color(0.94f, 0.94f, 0.94f);

        #endregion

        [MenuItem("Window/Effects/Effect Previewer %#e")]
        public static void ShowWindow()
        {
            var window = GetWindow<EffectPreviewWindow>();
            window.titleContent = new GUIContent("Effect Previewer");
            window.minSize = new Vector2(350, 500);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshEffectList();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupTexture();
        }

        private void OnEditorUpdate()
        {
            if (!_isPlaying || _definition == null || _definition.Frames.Count == 0) return;

            double currentTime = EditorApplication.timeSinceStartup;
            var frame = _definition.Frames[_currentFrame];
            float frameDuration = (frame.Duration > 0 ? frame.Duration : 1f / _definition.Fps) / _playbackSpeed;

            if (currentTime - _lastFrameTime >= frameDuration)
            {
                _lastFrameTime = currentTime;
                AdvanceFrame();
                RenderCurrentFrame();
                Repaint();
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawEffectSelector();
            EditorGUILayout.Space(10);

            DrawPreviewSection();
            EditorGUILayout.Space(10);

            DrawPlaybackControls();
            EditorGUILayout.Space(10);

            DrawEffectInfo();
            EditorGUILayout.Space(10);

            DrawFrameDetail();

            EditorGUILayout.EndScrollView();
        }

        #region UI Drawing

        private void DrawEffectSelector()
        {
            EditorGUILayout.LabelField("Effect Selection", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Effect:", GUILayout.Width(50));

                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup(_selectedIndex, _effectFiles);
                if (EditorGUI.EndChangeCheck() && newIndex != _selectedIndex)
                {
                    _selectedIndex = newIndex;
                    if (_selectedIndex >= 0 && _selectedIndex < _effectFiles.Length)
                    {
                        LoadEffect(_effectFiles[_selectedIndex]);
                    }
                }

                if (GUILayout.Button("Reload", GUILayout.Width(60)))
                {
                    if (!string.IsNullOrEmpty(_selectedEffectName))
                    {
                        LoadEffect(_selectedEffectName);
                    }
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                {
                    RefreshEffectList();
                }
            }
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 背景設定
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Background:", GUILayout.Width(80));
                    _bgType = (BackgroundType)EditorGUILayout.EnumPopup(_bgType, GUILayout.Width(100));

                    if (_bgType == BackgroundType.Custom)
                    {
                        _customBgColor = EditorGUILayout.ColorField(_customBgColor, GUILayout.Width(60));
                    }

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField("Size:", GUILayout.Width(35));
                    _previewSize = EditorGUILayout.IntSlider(_previewSize, 50, 400, GUILayout.Width(150));
                }

                EditorGUILayout.Space(5);

                // プレビュー描画
                Rect previewRect = GUILayoutUtility.GetRect(_previewSize, _previewSize, GUILayout.ExpandWidth(false));

                // 中央揃え
                float centerX = (EditorGUIUtility.currentViewWidth - _previewSize) / 2;
                previewRect.x = centerX;

                // 背景描画
                Color bgColor = GetBackgroundColor();
                EditorGUI.DrawRect(previewRect, bgColor);

                // チェッカーボード
                if (_bgType == BackgroundType.Checkerboard)
                {
                    DrawCheckerboard(previewRect);
                }

                // テクスチャ描画
                if (_previewTexture != null)
                {
                    GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit);
                }
                else
                {
                    // プレースホルダー
                    var style = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.gray }
                    };
                    GUI.Label(previewRect, "No Effect Loaded", style);
                }
            }
        }

        private void DrawPlaybackControls()
        {
            EditorGUILayout.LabelField("Playback Control", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool hasFrames = _definition != null && _definition.Frames.Count > 0;
                int totalFrames = hasFrames ? _definition.Frames.Count : 0;

                // フレーム操作
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField("Frame:", GUILayout.Width(45));

                    GUI.enabled = hasFrames && _currentFrame > 0;
                    if (GUILayout.Button("|◀", GUILayout.Width(30)))
                    {
                        GoToFrame(0);
                    }
                    if (GUILayout.Button("◀", GUILayout.Width(30)))
                    {
                        GoToFrame(_currentFrame - 1);
                    }

                    GUI.enabled = true;
                    string frameText = hasFrames ? $" {_currentFrame + 1} / {totalFrames} " : " - / - ";
                    EditorGUILayout.LabelField(frameText, EditorStyles.boldLabel, GUILayout.Width(60));

                    GUI.enabled = hasFrames && _currentFrame < totalFrames - 1;
                    if (GUILayout.Button("▶", GUILayout.Width(30)))
                    {
                        GoToFrame(_currentFrame + 1);
                    }
                    if (GUILayout.Button("▶|", GUILayout.Width(30)))
                    {
                        GoToFrame(totalFrames - 1);
                    }

                    GUI.enabled = true;
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.Space(5);

                // 再生制御
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    GUI.enabled = hasFrames;

                    if (_isPlaying)
                    {
                        if (GUILayout.Button("⏸ Pause", GUILayout.Width(70)))
                        {
                            _isPlaying = false;
                            StopClipInEditor();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("▶ Play", GUILayout.Width(70)))
                        {
                            _isPlaying = true;
                            _lastFrameTime = EditorApplication.timeSinceStartup;
                            GoToFrame(0);
                            PlayClipInEditor(_seClip);
                        }
                    }

                    if (GUILayout.Button("■ Stop", GUILayout.Width(70)))
                    {
                        _isPlaying = false;
                        GoToFrame(0);
                        StopClipInEditor();
                    }

                    GUI.enabled = true;

                    EditorGUILayout.Space(10);

                    _loop = EditorGUILayout.ToggleLeft("Loop", _loop, GUILayout.Width(50));

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.Space(5);

                // 速度調整
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Speed:", GUILayout.Width(45));
                    _playbackSpeed = EditorGUILayout.Slider(_playbackSpeed, 0.1f, 3f);
                    EditorGUILayout.LabelField($"{_playbackSpeed:F1}x", GUILayout.Width(35));
                }
            }
        }

        private void DrawEffectInfo()
        {
            EditorGUILayout.LabelField("Effect Info", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_definition == null)
                {
                    EditorGUILayout.LabelField("No effect loaded", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                DrawInfoRow("Name:", _definition.Name ?? "(unnamed)");
                DrawInfoRow("Canvas:", $"{_definition.Canvas} x {_definition.Canvas}");
                DrawInfoRow("FPS:", _definition.Fps.ToString());
                DrawInfoRow("Frames:", _definition.Frames.Count.ToString());
                string seStatus = string.IsNullOrEmpty(_definition.Se) ? "(none)"
                    : _seClip != null ? $"{_definition.Se} (loaded)"
                    : $"{_definition.Se} (not found)";
                DrawInfoRow("SE:", seStatus);

                // 総再生時間計算
                float totalDuration = 0f;
                float defaultDuration = 1f / _definition.Fps;
                foreach (var frame in _definition.Frames)
                {
                    totalDuration += frame.Duration > 0 ? frame.Duration : defaultDuration;
                }
                DrawInfoRow("Duration:", $"{totalDuration:F2}s");
            }
        }

        private void DrawFrameDetail()
        {
            EditorGUILayout.LabelField("Current Frame Detail", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_definition == null || _definition.Frames.Count == 0)
                {
                    EditorGUILayout.LabelField("No frame data", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                var frame = _definition.Frames[_currentFrame];
                float defaultDuration = 1f / _definition.Fps;
                float duration = frame.Duration > 0 ? frame.Duration : defaultDuration;

                EditorGUILayout.LabelField($"Frame #{_currentFrame + 1}  (Duration: {duration:F3}s)", EditorStyles.boldLabel);

                int shapeCount = frame.Shapes?.Count ?? 0;
                EditorGUILayout.LabelField($"Shapes: {shapeCount}");

                if (frame.Shapes != null && frame.Shapes.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < Mathf.Min(frame.Shapes.Count, 10); i++)
                    {
                        var shape = frame.Shapes[i];
                        string shapeInfo = FormatShapeInfo(shape);
                        EditorGUILayout.LabelField($"[{i}] {shapeInfo}", EditorStyles.miniLabel);
                    }
                    if (frame.Shapes.Count > 10)
                    {
                        EditorGUILayout.LabelField($"... and {frame.Shapes.Count - 10} more", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawInfoRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(70));
                EditorGUILayout.LabelField(value);
            }
        }

        private string FormatShapeInfo(ShapeDefinition shape)
        {
            string type = shape.Type ?? "unknown";
            string coords = "";
            string style = "";

            switch (shape.GetShapeType())
            {
                case ShapeType.Point:
                    coords = $"x:{shape.X:F0} y:{shape.Y:F0} size:{shape.Size:F0}";
                    break;
                case ShapeType.Line:
                    coords = $"({shape.X1:F0},{shape.Y1:F0})->({shape.X2:F0},{shape.Y2:F0})";
                    break;
                case ShapeType.Circle:
                    coords = $"x:{shape.X:F0} y:{shape.Y:F0} r:{shape.Radius:F0}";
                    break;
                case ShapeType.Ellipse:
                    coords = $"x:{shape.X:F0} y:{shape.Y:F0} rx:{shape.Rx:F0} ry:{shape.Ry:F0}";
                    break;
                case ShapeType.Arc:
                    coords = $"x:{shape.X:F0} y:{shape.Y:F0} r:{shape.Radius:F0}";
                    break;
                case ShapeType.Rect:
                    coords = $"x:{shape.X:F0} y:{shape.Y:F0} w:{shape.Width:F0} h:{shape.Height:F0}";
                    break;
                case ShapeType.Polygon:
                case ShapeType.Bezier:
                    int pointCount = shape.Points?.Count ?? 0;
                    coords = $"{pointCount} points";
                    break;
                case ShapeType.TaperedLine:
                    coords = $"({shape.X1:F0},{shape.Y1:F0})->({shape.X2:F0},{shape.Y2:F0}) w:{shape.WidthStart:F1}->{shape.WidthEnd:F1}";
                    break;
                case ShapeType.Ring:
                    coords = $"x:{shape.X:F0} y:{shape.Y:F0} r:{shape.InnerRadius:F0}-{shape.Radius:F0}";
                    break;
                case ShapeType.Emitter:
                    coords = $"x:{shape.X:F0} y:{shape.Y:F0} count:{shape.Count} life:{shape.Lifetime}";
                    break;
            }

            if (shape.Pen != null)
            {
                style += $" pen:{shape.Pen.Color}";
            }
            if (shape.Brush != null)
            {
                string brushType = shape.Brush.Type ?? "flat";
                style += brushType == "flat" || string.IsNullOrEmpty(shape.Brush.Type)
                    ? $" brush:{shape.Brush.Color}"
                    : $" brush:{brushType}";
            }
            if (!string.IsNullOrEmpty(shape.Blend))
            {
                style += $" [{shape.Blend}]";
            }

            return $"{type} {coords}{style}";
        }

        private void DrawCheckerboard(Rect rect)
        {
            int tileSize = 10;
            Color c1 = new Color(0.4f, 0.4f, 0.4f);
            Color c2 = new Color(0.6f, 0.6f, 0.6f);

            for (int y = 0; y < rect.height; y += tileSize)
            {
                for (int x = 0; x < rect.width; x += tileSize)
                {
                    bool isEven = ((x / tileSize) + (y / tileSize)) % 2 == 0;
                    Rect tileRect = new Rect(rect.x + x, rect.y + y,
                        Mathf.Min(tileSize, rect.width - x),
                        Mathf.Min(tileSize, rect.height - y));
                    EditorGUI.DrawRect(tileRect, isEven ? c1 : c2);
                }
            }
        }

        private Color GetBackgroundColor()
        {
            return _bgType switch
            {
                BackgroundType.Dark => DarkBgColor,
                BackgroundType.Light => LightBgColor,
                BackgroundType.Checkerboard => Color.clear,
                BackgroundType.Custom => _customBgColor,
                _ => DarkBgColor
            };
        }

        #endregion

        #region Effect Loading & Rendering

        private void RefreshEffectList()
        {
            var list = new List<string>();

            string effectsPath = "Assets/Resources/Effects";
            if (Directory.Exists(effectsPath))
            {
                var files = Directory.GetFiles(effectsPath, "*.json");
                foreach (var file in files)
                {
                    list.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            _effectFiles = list.ToArray();

            // 現在の選択を維持
            if (!string.IsNullOrEmpty(_selectedEffectName))
            {
                _selectedIndex = Array.IndexOf(_effectFiles, _selectedEffectName);
            }
        }

        private void LoadEffect(string effectName)
        {
            _selectedEffectName = effectName;
            _isPlaying = false;
            _currentFrame = 0;

            string path = $"Assets/Resources/Effects/{effectName}.json";

            if (!File.Exists(path))
            {
                Debug.LogError($"[EffectPreviewWindow] File not found: {path}");
                _definition = null;
                CleanupTexture();
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                _definition = KfxCompiler.LoadFromJson(json);

                if (_definition == null)
                {
                    Debug.LogError($"[EffectPreviewWindow] Failed to parse: {effectName}");
                    CleanupTexture();
                    return;
                }

                // 正規化
                _definition.Name = effectName;
                _definition.Normalize();

                // バリデーション
                if (_definition.Frames == null || _definition.Frames.Count == 0)
                {
                    Debug.LogError($"[EffectPreviewWindow] Effect has no frames: {effectName}");
                    _definition = null;
                    CleanupTexture();
                    return;
                }

                if (_definition.Canvas <= 0 || _definition.Canvas > EffectConstants.CanvasMax)
                {
                    Debug.LogError($"[EffectPreviewWindow] Invalid canvas size: {_definition.Canvas}");
                    _definition = null;
                    CleanupTexture();
                    return;
                }

                // SEロード
                LoadSeClip(_definition.Se);

                // レンダラー作成
                _renderer = new EffectRenderer(_definition.Canvas);

                // テクスチャ作成
                CleanupTexture();
                _previewTexture = _renderer.CreateTexture();

                // 最初のフレームを描画
                RenderCurrentFrame();

                Debug.Log($"[EffectPreviewWindow] Loaded: {effectName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EffectPreviewWindow] Error loading {effectName}: {e.Message}");
                _definition = null;
                CleanupTexture();
            }
        }

        private void RenderCurrentFrame()
        {
            if (_definition == null || _renderer == null || _previewTexture == null) return;
            if (_currentFrame < 0 || _currentFrame >= _definition.Frames.Count) return;

            var frame = _definition.Frames[_currentFrame];
            _renderer.RenderFrame(_previewTexture, frame, _currentFrame);
        }

        private void GoToFrame(int frameIndex)
        {
            if (_definition == null || _definition.Frames.Count == 0) return;

            _currentFrame = Mathf.Clamp(frameIndex, 0, _definition.Frames.Count - 1);
            RenderCurrentFrame();
            Repaint();
        }

        private void AdvanceFrame()
        {
            if (_definition == null || _definition.Frames.Count == 0) return;

            _currentFrame++;
            if (_currentFrame >= _definition.Frames.Count)
            {
                if (_loop)
                {
                    _currentFrame = 0;
                }
                else
                {
                    _currentFrame = _definition.Frames.Count - 1;
                    _isPlaying = false;
                }
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

        /// <summary>
        /// SEクリップをロード
        /// </summary>
        private void LoadSeClip(string seName)
        {
            if (string.IsNullOrEmpty(seName))
            {
                _seClip = null;
                _loadedSeName = null;
                return;
            }

            if (seName == _loadedSeName && _seClip != null) return;

            _loadedSeName = seName;
            _seClip = null;

            Debug.Log($"[EffectPreviewWindow] Loading SE: \"{seName}\"");

            // AssetDatabaseを更新（外部から追加されたファイルを認識させる）
            AssetDatabase.Refresh();

            // 1. Resources.Load（ランタイムと同じパス）
            string resourcePath = $"{EffectConstants.AudioResourcePath}{seName}";
            Debug.Log($"[EffectPreviewWindow] Trying Resources.Load(\"{resourcePath}\")");
            _seClip = Resources.Load<AudioClip>(resourcePath);
            if (_seClip != null)
            {
                Debug.Log($"[EffectPreviewWindow] SE loaded OK via Resources");
                return;
            }

            // 2. AssetDatabase で直接パスを試す
            string[] extensions = { ".wav", ".ogg", ".mp3", ".flac" };
            string basePath = $"Assets/Resources/{EffectConstants.AudioResourcePath}{seName}";
            foreach (var ext in extensions)
            {
                string fullPath = basePath + ext;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(fullPath);
                if (clip != null)
                {
                    _seClip = clip;
                    Debug.Log($"[EffectPreviewWindow] SE loaded OK via AssetDatabase: {fullPath}");
                    return;
                }
            }

            // 3. FindAssets で広く検索
            string[] guids = AssetDatabase.FindAssets($"{seName} t:AudioClip");
            Debug.Log($"[EffectPreviewWindow] FindAssets found {guids.Length} candidates");
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Debug.Log($"[EffectPreviewWindow]   candidate: {assetPath}");
                if (Path.GetFileNameWithoutExtension(assetPath) == seName)
                {
                    _seClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    if (_seClip != null)
                    {
                        Debug.Log($"[EffectPreviewWindow] SE loaded OK via FindAssets: {assetPath}");
                        return;
                    }
                }
            }

            Debug.Log($"[EffectPreviewWindow] SE FAILED to load: {seName}");
        }

        /// <summary>
        /// EditorでSEをプレビュー再生
        /// </summary>
        private static void PlayClipInEditor(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("[EffectPreviewWindow] No SE clip to play");
                return;
            }

            var audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilType == null)
            {
                Debug.LogWarning("[EffectPreviewWindow] AudioUtil type not found");
                return;
            }

            // 既存の再生を停止
            StopClipInEditor();

            // Unity バージョンによってメソッドシグネチャが異なるため複数試行
            // 1. PlayPreviewClip(AudioClip, int, bool)
            var playMethod = audioUtilType.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null);
            if (playMethod != null)
            {
                playMethod.Invoke(null, new object[] { clip, 0, false });
                return;
            }

            // 2. PlayPreviewClip(AudioClip)
            playMethod = audioUtilType.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(AudioClip) },
                null);
            if (playMethod != null)
            {
                playMethod.Invoke(null, new object[] { clip });
                return;
            }

            // 3. フォールバック: 名前だけで検索
            playMethod = audioUtilType.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (playMethod != null)
            {
                var parameters = playMethod.GetParameters();
                if (parameters.Length == 1)
                    playMethod.Invoke(null, new object[] { clip });
                else if (parameters.Length == 2)
                    playMethod.Invoke(null, new object[] { clip, 0 });
                else if (parameters.Length == 3)
                    playMethod.Invoke(null, new object[] { clip, 0, false });
                return;
            }

            Debug.LogWarning("[EffectPreviewWindow] PlayPreviewClip method not found in AudioUtil");
        }

        /// <summary>
        /// EditorでSEプレビュー停止
        /// </summary>
        private static void StopClipInEditor()
        {
            var audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilType == null) return;

            var stopMethod = audioUtilType.GetMethod("StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            stopMethod?.Invoke(null, null);
        }

        #endregion
    }

    /// <summary>
    /// 背景タイプ
    /// </summary>
    public enum BackgroundType
    {
        Dark,
        Light,
        Checkerboard,
        Custom
    }
}
