using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// KFX（キーフレームエフェクト）エディタウィンドウ
    /// </summary>
    public partial class KfxEditorWindow : EditorWindow
    {
        // ===== State =====
        private KfxDefinition _kfxDef;
        private string _filePath;
        private bool _isDirty;

        // Compiled
        private EffectDefinition _compiledDef;
        private EffectRenderer _renderer;
        private Texture2D _previewTexture;
        private bool _needsRecompile;
        private string _compileError;

        // File list
        private string[] _effectFiles = Array.Empty<string>();
        private int _selectedFileIndex = -1;

        // Playback
        private int _currentFrame;
        private bool _isPlaying;
        private bool _loop = true;
        private float _playbackSpeed = 1f;
        private double _lastFrameTime;

        // SE
        private AudioClip _seClip;
        private string _loadedSeName;

        // Selection
        private int _selectedLayerIndex = -1;
        private int _selectedKeyframeIndex = -1;

        // Display
        private int _previewSize = 200;
        private Vector2 _scrollPosition;

        // Layer visibility (editor-only, not saved)
        private readonly HashSet<int> _hiddenLayers = new HashSet<int>();

        // Undo/Redo
        private readonly List<string> _undoStack = new List<string>();
        private readonly List<string> _redoStack = new List<string>();
        private const int MaxUndoDepth = 30;
        private string _preEventSnapshot;
        private bool _undoPushedThisEvent;

        // Timeline drag
        private int _tlDragLayerIdx = -1;
        private int _tlDragKfIdx = -1;
        private bool _tlDragging;

        // Preview drag
        private bool _previewDragging;

        // Foldout
        private bool _metadataFoldout;

        // Focus
        private bool _needsFocusLayerId;

        // ===== Constants =====

        // Shape types for dropdown
        private static readonly string[] ShapeTypes = {
            "circle", "ellipse", "rect", "ring", "arc",
            "line", "tapered_line", "point", "emitter"
        };
        private static readonly string[] ShapeDisplayNames = {
            "円 (circle)", "楕円 (ellipse)", "矩形 (rect)", "リング (ring)", "弧 (arc)",
            "線 (line)", "テーパー線 (tapered_line)", "点 (point)", "エミッター (emitter)"
        };

        private static readonly string[] BlendDisplayNames = { "通常", "加算" };

        private static readonly string[] EaseValues = { "linear", "easeIn", "easeOut", "easeInOut", "step" };
        private static readonly string[] EaseDisplayNames = { "等速", "加速", "減速", "緩急", "ステップ" };

        private static readonly string[] BrushTypeValues = { "flat", "radial", "linear" };
        private static readonly string[] BrushTypeDisplayNames = { "単色", "放射", "線形" };

        // ===== PropRow & ShapeLayouts (property grouping) =====

        private class PropRow
        {
            public readonly string Label;
            public readonly string[] Props;
            public readonly bool IsAppearance;
            public PropRow(string label, string[] props, bool isAppearance = false)
            { Label = label; Props = props; IsAppearance = isAppearance; }
        }

        private static readonly Dictionary<string, PropRow[]> ShapeLayouts =
            new Dictionary<string, PropRow[]>
        {
            { "circle", new[] {
                new PropRow("位置", new[] { "x", "y" }),
                new PropRow("半径", new[] { "radius" }),
                new PropRow("線", new[] { "pen" }, isAppearance: true),
                new PropRow("塗り", new[] { "brush" }, isAppearance: true),
                new PropRow("不透明度", new[] { "opacity" }, isAppearance: true),
            }},
            { "ellipse", new[] {
                new PropRow("位置", new[] { "x", "y" }),
                new PropRow("サイズ", new[] { "rx", "ry" }),
                new PropRow("回転", new[] { "rotation" }),
                new PropRow("線", new[] { "pen" }, isAppearance: true),
                new PropRow("塗り", new[] { "brush" }, isAppearance: true),
                new PropRow("不透明度", new[] { "opacity" }, isAppearance: true),
            }},
            { "rect", new[] {
                new PropRow("位置", new[] { "x", "y" }),
                new PropRow("サイズ", new[] { "width", "height" }),
                new PropRow("回転", new[] { "rotation" }),
                new PropRow("線", new[] { "pen" }, isAppearance: true),
                new PropRow("塗り", new[] { "brush" }, isAppearance: true),
                new PropRow("不透明度", new[] { "opacity" }, isAppearance: true),
            }},
            { "ring", new[] {
                new PropRow("位置", new[] { "x", "y" }),
                new PropRow("外径", new[] { "radius" }),
                new PropRow("内径", new[] { "inner_radius" }),
                new PropRow("線", new[] { "pen" }, isAppearance: true),
                new PropRow("塗り", new[] { "brush" }, isAppearance: true),
                new PropRow("不透明度", new[] { "opacity" }, isAppearance: true),
            }},
            { "arc", new[] {
                new PropRow("位置", new[] { "x", "y" }),
                new PropRow("半径", new[] { "radius" }),
                new PropRow("角度範囲", new[] { "startAngle", "endAngle" }),
                new PropRow("線", new[] { "pen" }, isAppearance: true),
                new PropRow("不透明度", new[] { "opacity" }, isAppearance: true),
            }},
            { "line", new[] {
                new PropRow("始点", new[] { "x1", "y1" }),
                new PropRow("終点", new[] { "x2", "y2" }),
                new PropRow("線", new[] { "pen" }, isAppearance: true),
                new PropRow("不透明度", new[] { "opacity" }, isAppearance: true),
            }},
            { "tapered_line", new[] {
                new PropRow("始点", new[] { "x1", "y1" }),
                new PropRow("終点", new[] { "x2", "y2" }),
                new PropRow("太さ", new[] { "width_start", "width_end" }),
                new PropRow("線", new[] { "pen" }, isAppearance: true),
                new PropRow("不透明度", new[] { "opacity" }, isAppearance: true),
            }},
            { "point", new[] {
                new PropRow("位置", new[] { "x", "y" }),
                new PropRow("サイズ", new[] { "size" }),
                new PropRow("線", new[] { "pen" }, isAppearance: true),
                new PropRow("不透明度", new[] { "opacity" }, isAppearance: true),
            }},
        };

        // ===== EffectsPath =====

        private static readonly string EffectsPath =
            Path.Combine(Application.dataPath, "Resources", "Effects");

        // ===================================================================
        // ShowWindow / OnEnable / OnDisable / OnEditorUpdate
        // ===================================================================

        [MenuItem("Window/Effects/KFX Editor %#k")]
        public static void ShowWindow()
        {
            var window = GetWindow<KfxEditorWindow>();
            window.titleContent = new GUIContent("KFX \u30a8\u30c7\u30a3\u30bf");
            window.minSize = new Vector2(480, 600);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshFileList();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupTexture();
        }

        private void OnEditorUpdate()
        {
            if (!_isPlaying || _compiledDef == null || _compiledDef.Frames.Count == 0) return;

            double now = EditorApplication.timeSinceStartup;
            float frameDuration = (1f / _compiledDef.Fps) / _playbackSpeed;

            if (now - _lastFrameTime >= frameDuration)
            {
                _lastFrameTime = now;
                _currentFrame++;
                if (_currentFrame >= _compiledDef.Frames.Count)
                {
                    if (_loop) _currentFrame = 0;
                    else { _currentFrame = _compiledDef.Frames.Count - 1; _isPlaying = false; }
                }
                RenderCurrentFrame();
                Repaint();
            }
        }

        // ===================================================================
        // OnGUI
        // ===================================================================
        private void OnGUI()
        {
            // Capture pre-event snapshot for undo at the start of each event
            if (Event.current.type == EventType.MouseDown ||
                Event.current.type == EventType.KeyDown)
            {
                if (_kfxDef != null && _preEventSnapshot == null)
                {
                    _preEventSnapshot = SerializeForUndo();
                }
                _undoPushedThisEvent = false;
            }

            HandleKeyboardShortcuts();

            if (_needsRecompile)
            {
                Recompile();
                _needsRecompile = false;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Section order: File -> Preview -> Playback -> Timeline -> Metadata(foldable) -> Layers -> SelectedLayer
            DrawFileSection();
            EditorGUILayout.Space(8);
            DrawPreviewSection();
            EditorGUILayout.Space(4);
            DrawPlaybackControls();

            if (_kfxDef != null)
            {
                EditorGUILayout.Space(4);
                DrawTimeline();
                EditorGUILayout.Space(8);

                // Metadata (foldable)
                _metadataFoldout = EditorGUILayout.Foldout(_metadataFoldout, "\u25b6 \u57fa\u672c\u8a2d\u5b9a", true, EditorStyles.foldoutHeader);
                if (_metadataFoldout)
                {
                    DrawMetadataSection();
                }

                EditorGUILayout.Space(8);
                DrawLayerList();
                EditorGUILayout.Space(8);
                DrawSelectedLayer();
            }

            if (!string.IsNullOrEmpty(_compileError))
            {
                EditorGUILayout.HelpBox(_compileError, MessageType.Error);
            }

            EditorGUILayout.EndScrollView();

            // Clear pre-event snapshot at end of event
            if (Event.current.type == EventType.Used ||
                Event.current.type == EventType.Repaint ||
                Event.current.type == EventType.Layout)
            {
                _preEventSnapshot = null;
            }
        }

        // ===================================================================
        // Keyboard Shortcuts
        // ===================================================================
        private void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            // テキスト編集中はショートカット無効（←→等がテキスト操作と衝突するため）
            // ただし Ctrl+S, Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, Space, Shift+Space は常時有効
            bool editing = EditorGUIUtility.editingTextField;

            // === グローバル（常時有効） ===
            if (e.control && e.keyCode == KeyCode.S)
            {
                SaveFile();
                e.Use();
                return;
            }
            if (e.control && e.keyCode == KeyCode.Z && !e.shift)
            {
                PerformUndo();
                e.Use();
                return;
            }
            if (e.control && e.keyCode == KeyCode.Y)
            {
                PerformRedo();
                e.Use();
                return;
            }
            if (e.control && e.shift && e.keyCode == KeyCode.Z)
            {
                PerformRedo();
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.F1)
            {
                ShortcutReferenceWindow.Show();
                e.Use();
                return;
            }
            // Shift+Space: 常時有効（テキスト編集中でもPlayFromStart）
            if (e.shift && e.keyCode == KeyCode.Space)
            {
                PlayFromStart();
                e.Use();
                return;
            }
            // Space: テキスト編集中は無効
            if (e.keyCode == KeyCode.Space && !editing)
            {
                TogglePlayback();
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.Backspace && !editing)
            {
                StopAndRewind();
                e.Use();
                return;
            }

            if (editing) return; // 以降はテキスト編集中に無効

            // === 移動 ===
            if (e.keyCode == KeyCode.Home)
            {
                GoToFrame(0);
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.End)
            {
                int total = _compiledDef != null ? _compiledDef.Frames.Count - 1 : 0;
                GoToFrame(total);
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.LeftArrow)
            {
                if (e.control) JumpToPrevKeyframe();
                else if (e.shift) GoToFrame(_currentFrame - 10);
                else GoToFrame(_currentFrame - 1);
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.RightArrow)
            {
                if (e.control) JumpToNextKeyframe();
                else if (e.shift) GoToFrame(_currentFrame + 10);
                else GoToFrame(_currentFrame + 1);
                e.Use();
                return;
            }

            // === 再生速度・ループ ===
            if (e.control && e.keyCode == KeyCode.L)
            {
                _loop = !_loop;
                e.Use();
                return;
            }
            if (e.control && e.keyCode == KeyCode.Equals)
            {
                _playbackSpeed = Mathf.Clamp(_playbackSpeed + 0.25f, 0.1f, 3f);
                e.Use();
                return;
            }
            if (e.control && e.keyCode == KeyCode.Minus)
            {
                _playbackSpeed = Mathf.Clamp(_playbackSpeed - 0.25f, 0.1f, 3f);
                e.Use();
                return;
            }

            // === レイヤー操作 ===
            if (e.keyCode == KeyCode.UpArrow)
            {
                if (e.alt) SelectPrevLayer();
                else if (e.control && e.shift) MoveLayerUp();
                else SelectPrevKeyframe();
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.DownArrow)
            {
                if (e.alt) SelectNextLayer();
                else if (e.control && e.shift) MoveLayerDown();
                else SelectNextKeyframe();
                e.Use();
                return;
            }

            // === KF操作 ===
            if (e.keyCode == KeyCode.K)
            {
                AddKeyframeAtCurrentTime();
                e.Use();
                return;
            }
            if (e.control && e.keyCode == KeyCode.D)
            {
                DuplicateSelected();
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.Delete)
            {
                DeleteSelected();
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.Escape)
            {
                DeselectStep();
                e.Use();
                return;
            }

            // === KF時刻微調整 ===
            if (e.keyCode == KeyCode.LeftBracket)
            {
                NudgeKeyframeTime(e.shift ? -0.1f : -0.01f);
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.RightBracket)
            {
                NudgeKeyframeTime(e.shift ? 0.1f : 0.01f);
                e.Use();
                return;
            }

            // === F2: レイヤーID名フォーカス ===
            if (e.keyCode == KeyCode.F2)
            {
                _needsFocusLayerId = true;
                e.Use();
                return;
            }
        }

        // ===================================================================
        // Playback Helpers
        // ===================================================================
        private void TogglePlayback()
        {
            if (_compiledDef == null || _compiledDef.Frames.Count == 0) return;

            if (_isPlaying)
            {
                _isPlaying = false;
            }
            else
            {
                _isPlaying = true;
                _lastFrameTime = EditorApplication.timeSinceStartup;
                if (_currentFrame >= _compiledDef.Frames.Count - 1)
                    _currentFrame = 0;
                RenderCurrentFrame();
                PlaySe();
            }
            Repaint();
        }

        private void PlayFromStart()
        {
            if (_compiledDef == null || _compiledDef.Frames.Count == 0) return;
            _currentFrame = 0;
            _isPlaying = true;
            _lastFrameTime = EditorApplication.timeSinceStartup;
            RenderCurrentFrame();
            PlaySe();
            Repaint();
        }

        private void StopAndRewind()
        {
            _isPlaying = false;
            _currentFrame = 0;
            StopSe();
            RenderCurrentFrame();
            Repaint();
        }

        // ===================================================================
        // Navigation
        // ===================================================================
        private void JumpToPrevKeyframe()
        {
            if (_kfxDef == null || _compiledDef == null || _compiledDef.Fps <= 0) return;
            if (_selectedLayerIndex < 0 || _selectedLayerIndex >= _kfxDef.Layers.Count) return;

            var layer = _kfxDef.Layers[_selectedLayerIndex];
            if (layer.IsEmitter || layer.Keyframes == null || layer.Keyframes.Count == 0) return;

            float currentTime = _currentFrame / (float)_compiledDef.Fps;
            for (int i = layer.Keyframes.Count - 1; i >= 0; i--)
            {
                if (layer.Keyframes[i].Time < currentTime - 0.001f)
                {
                    int frame = Mathf.RoundToInt(layer.Keyframes[i].Time * _compiledDef.Fps);
                    GoToFrame(Mathf.Clamp(frame, 0, _compiledDef.Frames.Count - 1));
                    _selectedKeyframeIndex = i;
                    return;
                }
            }
        }

        private void JumpToNextKeyframe()
        {
            if (_kfxDef == null || _compiledDef == null || _compiledDef.Fps <= 0) return;
            if (_selectedLayerIndex < 0 || _selectedLayerIndex >= _kfxDef.Layers.Count) return;

            var layer = _kfxDef.Layers[_selectedLayerIndex];
            if (layer.IsEmitter || layer.Keyframes == null || layer.Keyframes.Count == 0) return;

            float currentTime = _currentFrame / (float)_compiledDef.Fps;
            for (int i = 0; i < layer.Keyframes.Count; i++)
            {
                if (layer.Keyframes[i].Time > currentTime + 0.001f)
                {
                    int frame = Mathf.RoundToInt(layer.Keyframes[i].Time * _compiledDef.Fps);
                    GoToFrame(Mathf.Clamp(frame, 0, _compiledDef.Frames.Count - 1));
                    _selectedKeyframeIndex = i;
                    return;
                }
            }
        }

        private void SelectPrevKeyframe()
        {
            if (_kfxDef == null || _selectedLayerIndex < 0 || _selectedLayerIndex >= _kfxDef.Layers.Count) return;
            var layer = _kfxDef.Layers[_selectedLayerIndex];
            if (layer.IsEmitter || layer.Keyframes == null || layer.Keyframes.Count == 0) return;

            if (_selectedKeyframeIndex > 0)
                _selectedKeyframeIndex--;
            Repaint();
        }

        private void SelectNextKeyframe()
        {
            if (_kfxDef == null || _selectedLayerIndex < 0 || _selectedLayerIndex >= _kfxDef.Layers.Count) return;
            var layer = _kfxDef.Layers[_selectedLayerIndex];
            if (layer.IsEmitter || layer.Keyframes == null || layer.Keyframes.Count == 0) return;

            if (_selectedKeyframeIndex < layer.Keyframes.Count - 1)
                _selectedKeyframeIndex++;
            Repaint();
        }

        private void SelectPrevLayer()
        {
            if (_kfxDef == null || _kfxDef.Layers.Count == 0) return;
            if (_selectedLayerIndex > 0)
            {
                _selectedLayerIndex--;
                _selectedKeyframeIndex = -1;
            }
            Repaint();
        }

        private void SelectNextLayer()
        {
            if (_kfxDef == null || _kfxDef.Layers.Count == 0) return;
            if (_selectedLayerIndex < _kfxDef.Layers.Count - 1)
            {
                _selectedLayerIndex++;
                _selectedKeyframeIndex = -1;
            }
            Repaint();
        }

        private void MoveLayerUp()
        {
            if (_kfxDef == null || _selectedLayerIndex <= 0) return;
            SwapLayers(_selectedLayerIndex, _selectedLayerIndex - 1);
        }

        private void MoveLayerDown()
        {
            if (_kfxDef == null || _selectedLayerIndex < 0 || _selectedLayerIndex >= _kfxDef.Layers.Count - 1) return;
            SwapLayers(_selectedLayerIndex, _selectedLayerIndex + 1);
        }

        // ===================================================================
        // Operations: Duplicate, Delete, Deselect, Nudge, AddKF, DeepCopy
        // ===================================================================
        private void DuplicateSelected()
        {
            if (_kfxDef == null) return;

            // KF選択中ならKF複製、そうでなければレイヤー複製
            if (_selectedKeyframeIndex >= 0 && _selectedLayerIndex >= 0 &&
                _selectedLayerIndex < _kfxDef.Layers.Count)
            {
                var layer = _kfxDef.Layers[_selectedLayerIndex];
                if (!layer.IsEmitter)
                    DuplicateKeyframe(layer, _selectedKeyframeIndex);
            }
            else if (_selectedLayerIndex >= 0 && _selectedLayerIndex < _kfxDef.Layers.Count)
            {
                DuplicateLayer(_selectedLayerIndex);
            }
        }

        private void DeleteSelected()
        {
            if (_kfxDef == null) return;

            // KF選択中ならKF削除、そうでなければレイヤー削除
            if (_selectedKeyframeIndex >= 0 && _selectedLayerIndex >= 0 &&
                _selectedLayerIndex < _kfxDef.Layers.Count)
            {
                var layer = _kfxDef.Layers[_selectedLayerIndex];
                if (!layer.IsEmitter && layer.Keyframes != null &&
                    _selectedKeyframeIndex < layer.Keyframes.Count &&
                    layer.Keyframes.Count > 1)
                {
                    layer.Keyframes.RemoveAt(_selectedKeyframeIndex);
                    _selectedKeyframeIndex = Mathf.Min(_selectedKeyframeIndex, layer.Keyframes.Count - 1);
                    MarkDirty();
                }
            }
            else if (_selectedLayerIndex >= 0 && _selectedLayerIndex < _kfxDef.Layers.Count)
            {
                _kfxDef.Layers.RemoveAt(_selectedLayerIndex);
                _selectedLayerIndex = Mathf.Min(_selectedLayerIndex, _kfxDef.Layers.Count - 1);
                _selectedKeyframeIndex = -1;
                _hiddenLayers.Clear();
                MarkDirty();
            }
        }

        private void DeselectStep()
        {
            // 段階的解除: KF → レイヤー
            if (_selectedKeyframeIndex >= 0)
            {
                _selectedKeyframeIndex = -1;
            }
            else if (_selectedLayerIndex >= 0)
            {
                _selectedLayerIndex = -1;
            }
            Repaint();
        }

        private void NudgeKeyframeTime(float delta)
        {
            if (_kfxDef == null || _selectedLayerIndex < 0 || _selectedLayerIndex >= _kfxDef.Layers.Count) return;
            var layer = _kfxDef.Layers[_selectedLayerIndex];
            if (layer.IsEmitter || layer.Keyframes == null || _selectedKeyframeIndex < 0 ||
                _selectedKeyframeIndex >= layer.Keyframes.Count) return;

            var kf = layer.Keyframes[_selectedKeyframeIndex];
            kf.Time = Mathf.Max(0f, Mathf.Round((kf.Time + delta) * 100f) / 100f);
            MarkDirty();
        }

        private void AddKeyframeAtCurrentTime()
        {
            if (_kfxDef == null || _selectedLayerIndex < 0 || _selectedLayerIndex >= _kfxDef.Layers.Count) return;
            var layer = _kfxDef.Layers[_selectedLayerIndex];
            if (layer.IsEmitter) return;

            AddKeyframe(layer);
        }

        private void DuplicateKeyframe(KfxLayer layer, int kfIndex)
        {
            if (layer.Keyframes == null || kfIndex < 0 || kfIndex >= layer.Keyframes.Count) return;

            var original = layer.Keyframes[kfIndex];
            var clone = DeepCopy(original);
            clone.Time = Mathf.Round((clone.Time + 0.1f) * 100f) / 100f;

            // 時間順で挿入位置を決定
            int insertIdx = layer.Keyframes.Count;
            for (int i = 0; i < layer.Keyframes.Count; i++)
            {
                if (layer.Keyframes[i].Time > clone.Time)
                {
                    insertIdx = i;
                    break;
                }
            }
            layer.Keyframes.Insert(insertIdx, clone);
            _selectedKeyframeIndex = insertIdx;
            MarkDirty();
        }

        private void DuplicateLayer(int layerIndex)
        {
            if (_kfxDef == null || layerIndex < 0 || layerIndex >= _kfxDef.Layers.Count) return;

            var original = _kfxDef.Layers[layerIndex];
            var clone = DeepCopy(original);
            clone.Id = (clone.Id ?? "layer") + "_copy";
            _kfxDef.Layers.Insert(layerIndex + 1, clone);
            _selectedLayerIndex = layerIndex + 1;
            _selectedKeyframeIndex = -1;
            MarkDirty();
        }

        private T DeepCopy<T>(T obj)
        {
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            string json = JsonConvert.SerializeObject(obj, settings);
            return JsonConvert.DeserializeObject<T>(json);
        }

        // ===================================================================
        // Undo / Redo
        // ===================================================================
        private string SerializeForUndo()
        {
            if (_kfxDef == null) return null;
            return JsonConvert.SerializeObject(_kfxDef,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        private void PushUndo()
        {
            if (_undoPushedThisEvent) return;

            // Use pre-event snapshot if available, otherwise serialize current state
            string snapshot = _preEventSnapshot ?? SerializeForUndo();
            if (snapshot == null) return;

            _undoStack.Add(snapshot);
            if (_undoStack.Count > MaxUndoDepth)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
            _undoPushedThisEvent = true;
        }

        private void PerformUndo()
        {
            if (_undoStack.Count == 0) return;

            string current = SerializeForUndo();
            if (current != null)
                _redoStack.Add(current);

            string prev = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            RestoreFromSnapshot(prev);
        }

        private void PerformRedo()
        {
            if (_redoStack.Count == 0) return;

            string current = SerializeForUndo();
            if (current != null)
                _undoStack.Add(current);

            string next = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            RestoreFromSnapshot(next);
        }

        private void RestoreFromSnapshot(string snapshot)
        {
            _kfxDef = JsonConvert.DeserializeObject<KfxDefinition>(snapshot);
            _needsRecompile = true;
            _isDirty = true;

            if (_kfxDef != null)
            {
                _selectedLayerIndex = Mathf.Clamp(_selectedLayerIndex, -1, _kfxDef.Layers.Count - 1);
                if (_selectedLayerIndex >= 0 && _selectedLayerIndex < _kfxDef.Layers.Count)
                {
                    var layer = _kfxDef.Layers[_selectedLayerIndex];
                    if (layer.Keyframes != null)
                        _selectedKeyframeIndex = Mathf.Clamp(_selectedKeyframeIndex, -1, layer.Keyframes.Count - 1);
                    else
                        _selectedKeyframeIndex = -1;
                }
            }
            Repaint();
        }

        // ===================================================================
        // MarkDirty (includes PushUndo)
        // ===================================================================
        private void MarkDirty()
        {
            PushUndo();
            _isDirty = true;
            _needsRecompile = true;
        }

        // ===================================================================
        // File Section (日本語ラベル、[?]ボタン、テンプレートメニュー日本語化)
        // ===================================================================
        private void DrawFileSection()
        {
            EditorGUILayout.LabelField("\u30d5\u30a1\u30a4\u30eb", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                int newIdx = EditorGUILayout.Popup(_selectedFileIndex, _effectFiles);
                if (newIdx != _selectedFileIndex && newIdx >= 0 && newIdx < _effectFiles.Length)
                {
                    _selectedFileIndex = newIdx;
                    LoadFile(_effectFiles[_selectedFileIndex]);
                }

                if (GUILayout.Button("\u65b0\u898f", GUILayout.Width(50)))
                    ShowNewTemplateMenu();

                if (GUILayout.Button("\u4fdd\u5b58", GUILayout.Width(50)))
                    SaveFile();

                if (GUILayout.Button("\u66f4\u65b0", GUILayout.Width(50)))
                    RefreshFileList();

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("?", "\u30ad\u30fc\u30dc\u30fc\u30c9\u30b7\u30e7\u30fc\u30c8\u30ab\u30c3\u30c8\u4e00\u89a7 (F1)"),
                    GUILayout.Width(24), GUILayout.Height(18)))
                {
                    ShortcutReferenceWindow.Show();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_isDirty && _kfxDef != null)
            {
                EditorGUILayout.HelpBox("\u672a\u4fdd\u5b58\u306e\u5909\u66f4\u3042\u308a", MessageType.Info);
            }
        }

        // ===================================================================
        // ShowNewTemplateMenu (日本語メニュー)
        // ===================================================================
        private void ShowNewTemplateMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("\u7a7a\u306e\u30a8\u30d5\u30a7\u30af\u30c8"), false,
                () => ApplyTemplate(KfxTemplates.Empty()));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("\u30d1\u30eb\u30b9"), false,
                () => ApplyTemplate(KfxTemplates.Pulse()));
            menu.AddItem(new GUIContent("\u885d\u6483\u6ce2"), false,
                () => ApplyTemplate(KfxTemplates.Shockwave()));
            menu.AddItem(new GUIContent("\u30d5\u30e9\u30c3\u30b7\u30e5"), false,
                () => ApplyTemplate(KfxTemplates.Flash()));
            menu.AddItem(new GUIContent("\u30d5\u30a7\u30fc\u30c9\u30a4\u30f3/\u30a2\u30a6\u30c8"), false,
                () => ApplyTemplate(KfxTemplates.FadeInOut()));
            menu.AddItem(new GUIContent("\u30d1\u30fc\u30c6\u30a3\u30af\u30eb"), false,
                () => ApplyTemplate(KfxTemplates.ParticleBurst()));
            menu.AddItem(new GUIContent("\u659c\u6483"), false,
                () => ApplyTemplate(KfxTemplates.Slash()));
            menu.ShowAsContext();
        }

        private void ApplyTemplate(KfxDefinition template)
        {
            _kfxDef = template;
            _filePath = null;
            _selectedLayerIndex = _kfxDef.Layers.Count > 0 ? 0 : -1;
            var firstLayer = _selectedLayerIndex >= 0 ? _kfxDef.Layers[0] : null;
            _selectedKeyframeIndex = (firstLayer != null && !firstLayer.IsEmitter &&
                                      firstLayer.Keyframes?.Count > 0) ? 0 : -1;
            _hiddenLayers.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            _isDirty = true;
            _needsRecompile = true;
        }

        // ===================================================================
        // Preview Section (日本語、プレビュードラッグ対応)
        // ===================================================================
        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("\u30d7\u30ec\u30d3\u30e5\u30fc", EditorStyles.boldLabel);

            _previewSize = EditorGUILayout.IntSlider("\u30b5\u30a4\u30ba", _previewSize, 80, 400);

            var previewRect = GUILayoutUtility.GetRect(_previewSize, _previewSize);
            float centerX = (EditorGUIUtility.currentViewWidth - _previewSize) / 2f;
            previewRect.x = centerX;
            previewRect.width = _previewSize;
            previewRect.height = _previewSize;

            EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.12f, 0.12f));

            if (_previewTexture != null)
            {
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.LabelField(previewRect, "\u30d7\u30ec\u30d3\u30e5\u30fc\u306a\u3057", EditorStyles.centeredGreyMiniLabel);
            }

            HandlePreviewDrag(previewRect);
        }

        private void HandlePreviewDrag(Rect previewRect)
        {
            if (_kfxDef == null || _selectedLayerIndex < 0 || _selectedLayerIndex >= _kfxDef.Layers.Count) return;
            var layer = _kfxDef.Layers[_selectedLayerIndex];
            if (layer.IsEmitter || layer.Keyframes == null || _selectedKeyframeIndex < 0 ||
                _selectedKeyframeIndex >= layer.Keyframes.Count) return;

            Event e = Event.current;

            if (e.type == EventType.MouseDown && previewRect.Contains(e.mousePosition))
            {
                _previewDragging = true;
                e.Use();
            }

            if (_previewDragging && e.type == EventType.MouseDrag)
            {
                float canvasX = ((e.mousePosition.x - previewRect.x) / previewRect.width) * _kfxDef.Canvas;
                float canvasY = ((e.mousePosition.y - previewRect.y) / previewRect.height) * _kfxDef.Canvas;

                var kf = layer.Keyframes[_selectedKeyframeIndex];
                if (kf.X.HasValue || kf.Y.HasValue || _selectedKeyframeIndex == 0)
                {
                    kf.X = Mathf.Round(canvasX);
                    kf.Y = Mathf.Round(canvasY);
                    _needsRecompile = true;
                }
                e.Use();
            }

            if (_previewDragging && e.type == EventType.MouseUp)
            {
                _previewDragging = false;
                MarkDirty();
                e.Use();
            }
        }

        // ===================================================================
        // Playback Controls (日本語ラベル)
        // ===================================================================
        private void DrawPlaybackControls()
        {
            if (_compiledDef == null || _compiledDef.Frames.Count == 0) return;

            int totalFrames = _compiledDef.Frames.Count;

            // Frame navigation
            EditorGUILayout.BeginHorizontal();
            {
                GUI.enabled = _currentFrame > 0;
                if (GUILayout.Button("|<", GUILayout.Width(30))) GoToFrame(0);
                if (GUILayout.Button("<", GUILayout.Width(30))) GoToFrame(_currentFrame - 1);
                GUI.enabled = true;

                float curTime = _currentFrame / (float)_compiledDef.Fps;
                EditorGUILayout.LabelField(
                    $"{curTime:F2}\u79d2  ({_currentFrame + 1} / {totalFrames})",
                    EditorStyles.centeredGreyMiniLabel);

                GUI.enabled = _currentFrame < totalFrames - 1;
                if (GUILayout.Button(">", GUILayout.Width(30))) GoToFrame(_currentFrame + 1);
                if (GUILayout.Button(">|", GUILayout.Width(30))) GoToFrame(totalFrames - 1);
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();

            // Play/Stop/Loop/Speed
            EditorGUILayout.BeginHorizontal();
            {
                if (_isPlaying)
                {
                    if (GUILayout.Button("\u4e00\u6642\u505c\u6b62")) _isPlaying = false;
                }
                else
                {
                    if (GUILayout.Button("\u25b6 \u518d\u751f"))
                    {
                        _currentFrame = 0;
                        _isPlaying = true;
                        _lastFrameTime = EditorApplication.timeSinceStartup;
                        RenderCurrentFrame();
                        PlaySe();
                    }
                }
                if (GUILayout.Button("\u25a0 \u505c\u6b62"))
                {
                    _isPlaying = false;
                    GoToFrame(0);
                    StopSe();
                }
                _loop = GUILayout.Toggle(_loop, "\u30eb\u30fc\u30d7", GUILayout.Width(60));
                EditorGUILayout.LabelField("\u901f\u5ea6:", GUILayout.Width(35));
                _playbackSpeed = EditorGUILayout.Slider(_playbackSpeed, 0.1f, 3f);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ===================================================================
        // Visual Timeline (enhanced: drag, double-click KF add, right-click menu)
        // ===================================================================
        private void DrawTimeline()
        {
            if (_kfxDef == null || _kfxDef.Layers.Count == 0) return;

            EditorGUILayout.LabelField("\u30bf\u30a4\u30e0\u30e9\u30a4\u30f3", EditorStyles.boldLabel);

            float duration = _kfxDef.Duration;
            if (duration <= 0) return;

            float labelWidth = 90f;
            float padding = 10f;
            float viewWidth = EditorGUIUtility.currentViewWidth;
            float barWidth = viewWidth - labelWidth - padding * 2;
            if (barWidth < 50) return;

            float rowHeight = 22f;
            float scaleHeight = 18f;
            float totalHeight = _kfxDef.Layers.Count * rowHeight + scaleHeight;

            var timelineRect = GUILayoutUtility.GetRect(viewWidth, totalHeight);

            // Background
            EditorGUI.DrawRect(timelineRect, new Color(0.16f, 0.16f, 0.16f));

            float barX = timelineRect.x + labelWidth;

            // Each layer row
            for (int i = 0; i < _kfxDef.Layers.Count; i++)
            {
                var layer = _kfxDef.Layers[i];
                float rowY = timelineRect.y + i * rowHeight;
                bool isSelected = (i == _selectedLayerIndex);

                // Layer name label
                var labelRect = new Rect(timelineRect.x + 4, rowY, labelWidth - 8, rowHeight);
                var labelStyle = isSelected ? EditorStyles.whiteBoldLabel : EditorStyles.miniLabel;
                GUI.Label(labelRect, layer.Id ?? $"layer_{i}", labelStyle);

                // Click label to select
                if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
                {
                    _selectedLayerIndex = i;
                    _selectedKeyframeIndex = -1;
                    Event.current.Use();
                    Repaint();
                }

                // Visible range bar
                float visStart = (layer.Visible != null && layer.Visible.Count >= 1) ? layer.Visible[0] : 0;
                float visEnd = (layer.Visible != null && layer.Visible.Count >= 2) ? layer.Visible[1] : duration;
                float barStartX = barX + (visStart / duration) * barWidth;
                float barEndX = barX + (visEnd / duration) * barWidth;
                var barRect = new Rect(barStartX, rowY + 3, barEndX - barStartX, rowHeight - 6);

                Color barColor;
                if (layer.IsEmitter)
                    barColor = isSelected ? new Color(0.6f, 0.4f, 0.2f, 0.6f) : new Color(0.4f, 0.3f, 0.2f, 0.4f);
                else
                    barColor = isSelected ? new Color(0.3f, 0.5f, 0.8f, 0.6f) : new Color(0.3f, 0.3f, 0.5f, 0.4f);

                EditorGUI.DrawRect(barRect, barColor);

                // Keyframe markers (non-emitter)
                if (!layer.IsEmitter && layer.Keyframes != null)
                {
                    for (int k = 0; k < layer.Keyframes.Count; k++)
                    {
                        var kf = layer.Keyframes[k];
                        float kfX = barX + (kf.Time / duration) * barWidth;
                        float kfY = rowY + rowHeight / 2;
                        bool kfSelected = isSelected && k == _selectedKeyframeIndex;
                        Color markerColor = kfSelected ? Color.yellow : (isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f));

                        // Diamond shape (small rotated square)
                        float s = kfSelected ? 5f : 4f;
                        var markerRect = new Rect(kfX - s, kfY - s, s * 2, s * 2);
                        EditorGUI.DrawRect(new Rect(kfX - s * 0.5f, kfY - s, s, s * 2), markerColor);
                        EditorGUI.DrawRect(new Rect(kfX - s, kfY - s * 0.5f, s * 2, s), markerColor);

                        // MouseDown on marker: start drag or select
                        if (Event.current.type == EventType.MouseDown && markerRect.Contains(Event.current.mousePosition))
                        {
                            // Double-click to add KF
                            if (Event.current.clickCount == 2)
                            {
                                // Double-click on existing marker does nothing special,
                                // double-click on empty space is handled below
                            }

                            _selectedLayerIndex = i;
                            _selectedKeyframeIndex = k;
                            _tlDragLayerIdx = i;
                            _tlDragKfIdx = k;
                            _tlDragging = true;
                            Event.current.Use();
                            Repaint();
                        }
                    }
                }

                // Row separator
                EditorGUI.DrawRect(new Rect(timelineRect.x, rowY + rowHeight - 1, timelineRect.width, 1),
                    new Color(0.2f, 0.2f, 0.2f));
            }

            // Handle timeline drag (MouseDrag) — recompile only, no undo per drag event
            if (_tlDragging && Event.current.type == EventType.MouseDrag)
            {
                if (_tlDragLayerIdx >= 0 && _tlDragLayerIdx < _kfxDef.Layers.Count)
                {
                    var layer = _kfxDef.Layers[_tlDragLayerIdx];
                    if (layer.Keyframes != null && _tlDragKfIdx >= 0 && _tlDragKfIdx < layer.Keyframes.Count)
                    {
                        float clickT = (Event.current.mousePosition.x - barX) / barWidth;
                        float newTime = Mathf.Clamp01(clickT) * duration;
                        newTime = Mathf.Round(newTime * 100f) / 100f;
                        layer.Keyframes[_tlDragKfIdx].Time = newTime;
                        _needsRecompile = true;
                        Event.current.Use();
                    }
                }
            }

            // MouseUp: finish drag, sort keyframes, push single undo
            if (_tlDragging && Event.current.type == EventType.MouseUp)
            {
                if (_tlDragLayerIdx >= 0 && _tlDragLayerIdx < _kfxDef.Layers.Count)
                {
                    var layer = _kfxDef.Layers[_tlDragLayerIdx];
                    if (layer.Keyframes != null && layer.Keyframes.Count > 1)
                    {
                        var draggedKf = (_tlDragKfIdx >= 0 && _tlDragKfIdx < layer.Keyframes.Count)
                            ? layer.Keyframes[_tlDragKfIdx] : null;
                        layer.Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
                        if (draggedKf != null)
                            _selectedKeyframeIndex = layer.Keyframes.IndexOf(draggedKf);
                    }
                    MarkDirty();
                }
                _tlDragging = false;
                _tlDragLayerIdx = -1;
                _tlDragKfIdx = -1;
                Event.current.Use();
            }

            // Current position indicator (vertical line)
            if (_compiledDef != null && _compiledDef.Fps > 0)
            {
                float currentTime = _currentFrame / (float)_compiledDef.Fps;
                float lineX = barX + (currentTime / duration) * barWidth;
                EditorGUI.DrawRect(new Rect(lineX - 1, timelineRect.y, 2, _kfxDef.Layers.Count * rowHeight),
                    new Color(1f, 0.3f, 0.3f, 0.9f));
            }

            // Time scale (bottom)
            float scaleY = timelineRect.y + _kfxDef.Layers.Count * rowHeight;
            float step = duration <= 0.5f ? 0.1f : (duration <= 2f ? 0.25f : 0.5f);
            var scaleStyle = EditorStyles.centeredGreyMiniLabel;
            for (float t = 0; t <= duration + 0.001f; t += step)
            {
                float tx = barX + (t / duration) * barWidth;
                EditorGUI.DrawRect(new Rect(tx, scaleY, 1, 4), Color.gray);
                var scaleLabelRect = new Rect(tx - 15, scaleY + 3, 30, 14);
                GUI.Label(scaleLabelRect, $"{t:F1}", scaleStyle);
            }

            // Double-click on timeline empty space to add KF
            var timelineScrubRect = new Rect(barX, timelineRect.y, barWidth, _kfxDef.Layers.Count * rowHeight);
            if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 &&
                timelineScrubRect.Contains(Event.current.mousePosition))
            {
                float clickT = (Event.current.mousePosition.x - barX) / barWidth;
                float clickTime = Mathf.Clamp01(clickT) * duration;
                clickTime = Mathf.Round(clickTime * 100f) / 100f;

                // Determine which layer row was clicked
                int clickedLayer = Mathf.FloorToInt((Event.current.mousePosition.y - timelineRect.y) / rowHeight);
                clickedLayer = Mathf.Clamp(clickedLayer, 0, _kfxDef.Layers.Count - 1);

                _selectedLayerIndex = clickedLayer;
                var layer = _kfxDef.Layers[clickedLayer];
                if (!layer.IsEmitter)
                {
                    if (layer.Keyframes == null)
                        layer.Keyframes = new List<KfxKeyframe>();

                    var newKf = new KfxKeyframe { Time = clickTime, Ease = "linear" };
                    int insertIdx = layer.Keyframes.Count;
                    for (int i = 0; i < layer.Keyframes.Count; i++)
                    {
                        if (layer.Keyframes[i].Time > clickTime)
                        {
                            insertIdx = i;
                            break;
                        }
                    }
                    layer.Keyframes.Insert(insertIdx, newKf);
                    _selectedKeyframeIndex = insertIdx;
                    MarkDirty();
                }
                Event.current.Use();
                Repaint();
                return;
            }

            // Right-click context menu on timeline
            if (Event.current.type == EventType.ContextClick &&
                timelineScrubRect.Contains(Event.current.mousePosition))
            {
                float clickT = (Event.current.mousePosition.x - barX) / barWidth;
                float clickTime = Mathf.Clamp01(clickT) * duration;
                clickTime = Mathf.Round(clickTime * 100f) / 100f;

                int clickedLayer = Mathf.FloorToInt((Event.current.mousePosition.y - timelineRect.y) / rowHeight);
                clickedLayer = Mathf.Clamp(clickedLayer, 0, _kfxDef.Layers.Count - 1);

                var menu = new GenericMenu();

                // Capture for closure
                int capturedLayer = clickedLayer;
                float capturedTime = clickTime;

                var layer = _kfxDef.Layers[capturedLayer];
                if (!layer.IsEmitter)
                {
                    menu.AddItem(new GUIContent("\u3053\u3053\u306b\u30ad\u30fc\u30d5\u30ec\u30fc\u30e0\u3092\u8ffd\u52a0"), false, () =>
                    {
                        _selectedLayerIndex = capturedLayer;
                        var targetLayer = _kfxDef.Layers[capturedLayer];
                        if (targetLayer.Keyframes == null)
                            targetLayer.Keyframes = new List<KfxKeyframe>();
                        var newKf = new KfxKeyframe { Time = capturedTime, Ease = "linear" };
                        int insertIdx = targetLayer.Keyframes.Count;
                        for (int i = 0; i < targetLayer.Keyframes.Count; i++)
                        {
                            if (targetLayer.Keyframes[i].Time > capturedTime)
                            {
                                insertIdx = i;
                                break;
                            }
                        }
                        targetLayer.Keyframes.Insert(insertIdx, newKf);
                        _selectedKeyframeIndex = insertIdx;
                        MarkDirty();
                    });
                }

                menu.AddItem(new GUIContent("\u3053\u3053\u306b\u518d\u751f\u4f4d\u7f6e\u3092\u79fb\u52d5"), false, () =>
                {
                    if (_compiledDef != null && _compiledDef.Fps > 0)
                    {
                        int frame = Mathf.RoundToInt(capturedTime * _compiledDef.Fps);
                        GoToFrame(Mathf.Clamp(frame, 0, _compiledDef.Frames.Count - 1));
                    }
                });

                menu.ShowAsContext();
                Event.current.Use();
                return;
            }

            // Click timeline to scrub (single click, not on a marker)
            if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 1 &&
                timelineScrubRect.Contains(Event.current.mousePosition))
            {
                float clickT = (Event.current.mousePosition.x - barX) / barWidth;
                float clickTime = Mathf.Clamp01(clickT) * duration;
                if (_compiledDef != null && _compiledDef.Fps > 0)
                {
                    int frame = Mathf.RoundToInt(clickTime * _compiledDef.Fps);
                    GoToFrame(Mathf.Clamp(frame, 0, _compiledDef.Frames.Count - 1));
                }
                Event.current.Use();
            }
        }

    } // end partial class KfxEditorWindow
} // end namespace EffectsEditor
