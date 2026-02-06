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
    public partial class KfxEditorWindow
    {
        // =============================================================
        // DrawMetadataSection (折りたたみ可能)
        // =============================================================
        private void DrawMetadataSection()
        {
            if (_kfxDef == null) return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            _kfxDef.Name = EditorGUILayout.TextField(
                new GUIContent("エフェクト名"), _kfxDef.Name);

            EditorGUILayout.BeginHorizontal();
            _kfxDef.Canvas = EditorGUILayout.IntField(
                new GUIContent("キャンバス", "描画領域のピクセルサイズ（正方形）"), _kfxDef.Canvas);
            _kfxDef.Fps = EditorGUILayout.IntField(
                new GUIContent("FPS", "1秒あたりのフレーム数"), _kfxDef.Fps);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _kfxDef.Duration = EditorGUILayout.FloatField(
                new GUIContent("長さ（秒）", "エフェクト全体の再生時間"), _kfxDef.Duration);
            _kfxDef.Se = EditorGUILayout.TextField(
                new GUIContent("効果音", "Assets/Resources/Audio/ のファイル名（拡張子なし）"), _kfxDef.Se);
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
                MarkDirty();

            EditorGUI.indentLevel--;
        }

        // =============================================================
        // DrawLayerList (日本語、複製、右クリック、並替)
        // =============================================================
        private void DrawLayerList()
        {
            EditorGUILayout.LabelField("レイヤー", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ レイヤー追加", GUILayout.Width(110)))
            {
                AddLayer();
            }
            GUI.enabled = _selectedLayerIndex >= 0 && _selectedLayerIndex < _kfxDef.Layers.Count;
            if (GUILayout.Button("複製", GUILayout.Width(50)))
            {
                DuplicateLayer(_selectedLayerIndex);
            }
            if (GUILayout.Button("削除", GUILayout.Width(50)))
            {
                RemoveLayer(_selectedLayerIndex);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < _kfxDef.Layers.Count; i++)
            {
                var layer = _kfxDef.Layers[i];
                bool selected = i == _selectedLayerIndex;
                bool visible = !_hiddenLayers.Contains(i);

                var rowRect = EditorGUILayout.BeginHorizontal(selected ? "SelectionRect" : "box");
                {
                    // Visibility toggle
                    bool newVisible = GUILayout.Toggle(visible, visible ? "E" : "-",
                        "Button", GUILayout.Width(22), GUILayout.Height(18));
                    if (newVisible != visible)
                    {
                        if (newVisible) _hiddenLayers.Remove(i);
                        else _hiddenLayers.Add(i);
                        _needsRecompile = true; // エディタ専用状態なのでundoに含めない
                    }

                    // Representative color dot
                    Color repColor = GetLayerColor(layer);
                    var dotRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
                    dotRect.y += 2;
                    EditorGUI.DrawRect(dotRect, Color.black);
                    EditorGUI.DrawRect(new Rect(dotRect.x + 1, dotRect.y + 1, 12, 12), repColor);

                    // Layer name (click to select)
                    if (GUILayout.Button(layer.Id ?? $"layer_{i}",
                        selected ? EditorStyles.boldLabel : EditorStyles.label,
                        GUILayout.Width(90)))
                    {
                        _selectedLayerIndex = i;
                        _selectedKeyframeIndex = -1;
                    }

                    // Type display name
                    int typeIdx = Array.IndexOf(ShapeTypes, layer.Type?.ToLowerInvariant());
                    string typeDisplay = typeIdx >= 0 && typeIdx < ShapeDisplayNames.Length
                        ? ShapeDisplayNames[typeIdx] : (layer.Type ?? "?");
                    EditorGUILayout.LabelField(typeDisplay, GUILayout.Width(70));

                    // Blend display name
                    string blendDisplay = layer.Blend == "additive" ? "加算" : "通常";
                    EditorGUILayout.LabelField(blendDisplay, GUILayout.Width(40));

                    // Reorder buttons
                    GUI.enabled = i > 0;
                    if (GUILayout.Button("\u25B2", GUILayout.Width(20), GUILayout.Height(18)))
                        SwapLayers(i, i - 1);
                    GUI.enabled = i < _kfxDef.Layers.Count - 1;
                    if (GUILayout.Button("\u25BC", GUILayout.Width(20), GUILayout.Height(18)))
                        SwapLayers(i, i + 1);
                    GUI.enabled = true;
                }
                EditorGUILayout.EndHorizontal();

                // Right-click context menu
                if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    var menu = new GenericMenu();
                    int idx = i;
                    menu.AddItem(new GUIContent("複製"), false, () => DuplicateLayer(idx));
                    menu.AddItem(new GUIContent("削除"), false, () => RemoveLayer(idx));
                    menu.AddSeparator("");
                    if (i > 0)
                        menu.AddItem(new GUIContent("上に移動"), false, () => SwapLayers(idx, idx - 1));
                    else
                        menu.AddDisabledItem(new GUIContent("上に移動"));
                    if (i < _kfxDef.Layers.Count - 1)
                        menu.AddItem(new GUIContent("下に移動"), false, () => SwapLayers(idx, idx + 1));
                    else
                        menu.AddDisabledItem(new GUIContent("下に移動"));
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }
        }

        private Color GetLayerColor(KfxLayer layer)
        {
            if (layer.IsEmitter)
                return EffectColorUtility.ParseColor(layer.ColorStart ?? "#FFFFFF");

            if (layer.Keyframes != null && layer.Keyframes.Count > 0)
            {
                var kf = layer.Keyframes[0];
                if (kf.Brush != null)
                {
                    if (!string.IsNullOrEmpty(kf.Brush.Color))
                        return EffectColorUtility.ParseColor(kf.Brush.Color);
                    if (!string.IsNullOrEmpty(kf.Brush.Center))
                        return EffectColorUtility.ParseColor(kf.Brush.Center);
                    if (!string.IsNullOrEmpty(kf.Brush.Start))
                        return EffectColorUtility.ParseColor(kf.Brush.Start);
                }
                if (kf.Pen != null && !string.IsNullOrEmpty(kf.Pen.Color))
                    return EffectColorUtility.ParseColor(kf.Pen.Color);
            }
            return Color.white;
        }

        private void SwapLayers(int a, int b)
        {
            if (a < 0 || a >= _kfxDef.Layers.Count || b < 0 || b >= _kfxDef.Layers.Count) return;

            var temp = _kfxDef.Layers[a];
            _kfxDef.Layers[a] = _kfxDef.Layers[b];
            _kfxDef.Layers[b] = temp;

            if (_selectedLayerIndex == a) _selectedLayerIndex = b;
            else if (_selectedLayerIndex == b) _selectedLayerIndex = a;

            _hiddenLayers.Clear();
            MarkDirty();
        }

        private void RemoveLayer(int index)
        {
            if (_kfxDef == null || index < 0 || index >= _kfxDef.Layers.Count) return;

            _kfxDef.Layers.RemoveAt(index);
            _selectedLayerIndex = Mathf.Min(_selectedLayerIndex, _kfxDef.Layers.Count - 1);
            _selectedKeyframeIndex = -1;
            _hiddenLayers.Clear();
            MarkDirty();
        }

        // =============================================================
        // DrawSelectedLayer (日本語)
        // =============================================================
        private const string LayerIdControlName = "KfxLayerIdField";

        private void DrawSelectedLayer()
        {
            if (_selectedLayerIndex < 0 || _selectedLayerIndex >= _kfxDef.Layers.Count) return;

            var layer = _kfxDef.Layers[_selectedLayerIndex];

            EditorGUILayout.LabelField($"レイヤー: {layer.Id}", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            // ID text field with focus support (F2 key triggers _needsFocusLayerId)
            GUI.SetNextControlName(LayerIdControlName);
            layer.Id = EditorGUILayout.TextField("ID", layer.Id);
            if (_needsFocusLayerId)
            {
                EditorGUI.FocusTextInControl(LayerIdControlName);
                _needsFocusLayerId = false;
            }

            // Type dropdown (shape display names)
            int typeIdx = Array.IndexOf(ShapeTypes, layer.Type?.ToLowerInvariant());
            if (typeIdx < 0) typeIdx = 0;
            int newTypeIdx = EditorGUILayout.Popup("図形", typeIdx, ShapeDisplayNames);
            if (newTypeIdx != typeIdx)
            {
                layer.Type = ShapeTypes[newTypeIdx];
                _selectedKeyframeIndex = -1;
            }

            // Blend dropdown
            int blendIdx = layer.Blend == "additive" ? 1 : 0;
            int newBlendIdx = EditorGUILayout.Popup("ブレンド", blendIdx, BlendDisplayNames);
            layer.Blend = newBlendIdx == 1 ? "additive" : null;

            // Visible range
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("表示範囲", "このレイヤーが表示される時間帯"),
                GUILayout.Width(60));
            float visStart = layer.Visible != null && layer.Visible.Count >= 1 ? layer.Visible[0] : 0f;
            float visEnd = layer.Visible != null && layer.Visible.Count >= 2 ? layer.Visible[1] : _kfxDef.Duration;
            float newVisStart = EditorGUILayout.FloatField(visStart, GUILayout.Width(60));
            EditorGUILayout.LabelField("-", GUILayout.Width(10));
            float newVisEnd = EditorGUILayout.FloatField(visEnd, GUILayout.Width(60));
            if (newVisStart != visStart || newVisEnd != visEnd)
                layer.Visible = new List<float> { newVisStart, newVisEnd };
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
                MarkDirty();

            EditorGUILayout.Space(6);

            if (layer.IsEmitter)
                DrawEmitterProperties(layer);
            else
                DrawKeyframeList(layer);
        }

        // =============================================================
        // DrawEmitterProperties (日本語)
        // =============================================================
        private void DrawEmitterProperties(KfxLayer layer)
        {
            EditorGUILayout.LabelField("エミッター設定", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            // Position: X/Y pair on one line
            float x = layer.X ?? 50;
            float y = layer.Y ?? 50;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("位置", GUILayout.Width(50));
            x = EditorGUILayout.FloatField("X", x);
            y = EditorGUILayout.FloatField("Y", y);
            EditorGUILayout.EndHorizontal();
            layer.X = x;
            layer.Y = y;

            layer.Count = EditorGUILayout.IntField("粒子数", layer.Count ?? 10);
            layer.Lifetime = EditorGUILayout.FloatField("寿命（秒）", layer.Lifetime ?? 1f);

            // Angle range (MinMaxSlider)
            float angMin = (layer.AngleRange != null && layer.AngleRange.Count >= 1) ? layer.AngleRange[0] : 0;
            float angMax = (layer.AngleRange != null && layer.AngleRange.Count >= 2) ? layer.AngleRange[1] : 360;
            float newAngMin = angMin, newAngMax = angMax;
            DrawRangeSlider("角度", ref newAngMin, ref newAngMax, 0f, 360f);
            if (newAngMin != angMin || newAngMax != angMax)
                layer.AngleRange = new List<float> { newAngMin, newAngMax };

            // Speed range (MinMaxSlider)
            float spdMin = (layer.SpeedRange != null && layer.SpeedRange.Count >= 1) ? layer.SpeedRange[0] : 1;
            float spdMax = (layer.SpeedRange != null && layer.SpeedRange.Count >= 2) ? layer.SpeedRange[1] : 4;
            float newSpdMin = spdMin, newSpdMax = spdMax;
            DrawRangeSlider("速度", ref newSpdMin, ref newSpdMax, 0f, 10f);
            if (newSpdMin != spdMin || newSpdMax != spdMax)
                layer.SpeedRange = new List<float> { newSpdMin, newSpdMax };

            // Size range (MinMaxSlider)
            float szMin = (layer.SizeRange != null && layer.SizeRange.Count >= 1) ? layer.SizeRange[0] : 1;
            float szMax = (layer.SizeRange != null && layer.SizeRange.Count >= 2) ? layer.SizeRange[1] : 3;
            float newSzMin = szMin, newSzMax = szMax;
            DrawRangeSlider("サイズ", ref newSzMin, ref newSzMax, 0f, 10f);
            if (newSzMin != szMin || newSzMax != szMax)
                layer.SizeRange = new List<float> { newSzMin, newSzMax };

            layer.Gravity = EditorGUILayout.FloatField("重力", layer.Gravity ?? 0);
            layer.Drag = EditorGUILayout.Slider("抵抗", layer.Drag ?? 1f, 0f, 1f);

            // Colors (ColorPicker)
            layer.ColorStart = DrawColorField("開始色", layer.ColorStart ?? "#FFFFFFCC");
            layer.ColorEnd = DrawColorField("終了色", layer.ColorEnd ?? "#FFFFFF00");

            layer.Seed = EditorGUILayout.IntField(
                new GUIContent("シード", "同じ値なら同じパーティクル配置になる"),
                layer.Seed ?? 0);

            if (EditorGUI.EndChangeCheck())
                MarkDirty();
        }

        // =============================================================
        // DrawKeyframeList (日本語、複製、右クリック)
        // =============================================================
        private void DrawKeyframeList(KfxLayer layer)
        {
            EditorGUILayout.LabelField("キーフレーム", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ キーフレーム追加", GUILayout.Width(140)))
            {
                AddKeyframe(layer);
            }
            bool hasSelection = _selectedKeyframeIndex >= 0 && layer.Keyframes != null &&
                               _selectedKeyframeIndex < layer.Keyframes.Count;
            GUI.enabled = hasSelection;
            if (GUILayout.Button("複製", GUILayout.Width(50)))
            {
                DuplicateKeyframe(layer, _selectedKeyframeIndex);
            }
            GUI.enabled = hasSelection && layer.Keyframes.Count > 1;
            if (GUILayout.Button("削除", GUILayout.Width(50)))
            {
                layer.Keyframes.RemoveAt(_selectedKeyframeIndex);
                _selectedKeyframeIndex = Mathf.Min(_selectedKeyframeIndex, layer.Keyframes.Count - 1);
                MarkDirty();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (layer.Keyframes == null) return;

            // Keyframe rows
            for (int i = 0; i < layer.Keyframes.Count; i++)
            {
                var kf = layer.Keyframes[i];
                bool selected = i == _selectedKeyframeIndex;

                // Ease display name
                int easeIdx = Array.IndexOf(EaseValues, kf.Ease?.ToLowerInvariant());
                string easeDisplay = easeIdx >= 0 && easeIdx < EaseDisplayNames.Length
                    ? EaseDisplayNames[easeIdx] : (kf.Ease ?? "等速");

                var kfRowRect = EditorGUILayout.BeginHorizontal(selected ? "SelectionRect" : "box");
                {
                    if (GUILayout.Button(selected ? "\u25B6" : " ", GUILayout.Width(20)))
                        _selectedKeyframeIndex = i;

                    EditorGUILayout.LabelField($"t={kf.Time:F2}秒", GUILayout.Width(80));
                    EditorGUILayout.LabelField(easeDisplay, GUILayout.Width(50));

                    // Summary (Japanese)
                    if (i == 0)
                    {
                        EditorGUILayout.LabelField("（基準）", EditorStyles.miniLabel);
                    }
                    else
                    {
                        string summary = GetKeyframeSummary(kf, layer.Type);
                        EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndHorizontal();

                // Right-click context menu on keyframe row
                if (Event.current.type == EventType.ContextClick && kfRowRect.Contains(Event.current.mousePosition))
                {
                    var menu = new GenericMenu();
                    int ki = i;
                    menu.AddItem(new GUIContent("複製"), false, () => DuplicateKeyframe(layer, ki));
                    if (layer.Keyframes.Count > 1)
                    {
                        menu.AddItem(new GUIContent("削除"), false, () =>
                        {
                            if (layer.Keyframes != null && ki < layer.Keyframes.Count && layer.Keyframes.Count > 1)
                            {
                                layer.Keyframes.RemoveAt(ki);
                                _selectedKeyframeIndex = Mathf.Min(_selectedKeyframeIndex, layer.Keyframes.Count - 1);
                                MarkDirty();
                            }
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("削除"));
                    }
                    menu.AddSeparator("");
                    if (_compiledDef != null && _compiledDef.Fps > 0)
                    {
                        menu.AddItem(new GUIContent("現在時刻に移動"), false, () =>
                        {
                            if (layer.Keyframes != null && ki < layer.Keyframes.Count)
                            {
                                layer.Keyframes[ki].Time = _currentFrame / (float)_compiledDef.Fps;
                                MarkDirty();
                            }
                        });
                    }
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }

            EditorGUILayout.Space(4);

            // Selected keyframe detail
            if (_selectedKeyframeIndex >= 0 && _selectedKeyframeIndex < layer.Keyframes.Count)
            {
                DrawKeyframeDetail(layer, layer.Keyframes[_selectedKeyframeIndex], _selectedKeyframeIndex);
            }
        }

        private string GetKeyframeSummary(KfxKeyframe kf, string shapeType)
        {
            var parts = new List<string>();
            if (kf.X.HasValue || kf.Y.HasValue) parts.Add("位置");
            if (kf.Radius.HasValue) parts.Add("半径");
            if (kf.InnerRadius.HasValue) parts.Add("内径");
            if (kf.Rx.HasValue || kf.Ry.HasValue) parts.Add("サイズ");
            if (kf.Width.HasValue || kf.Height.HasValue) parts.Add("サイズ");
            if (kf.Rotation.HasValue) parts.Add("回転");
            if (kf.Size.HasValue) parts.Add("サイズ");
            if (kf.X1.HasValue || kf.Y1.HasValue) parts.Add("始点");
            if (kf.X2.HasValue || kf.Y2.HasValue) parts.Add("終点");
            if (kf.WidthStart.HasValue || kf.WidthEnd.HasValue) parts.Add("太さ");
            if (kf.StartAngle.HasValue || kf.EndAngle.HasValue) parts.Add("角度");
            if (kf.Opacity.HasValue) parts.Add($"不透明度:{kf.Opacity:F1}");
            if (kf.Pen != null) parts.Add("線");
            if (kf.Brush != null) parts.Add("塗り");
            return parts.Count > 0 ? string.Join(", ", parts) : "(空)";
        }

        // =============================================================
        // DrawKeyframeDetail (プロパティグルーピング、日本語)
        // =============================================================
        private void DrawKeyframeDetail(KfxLayer layer, KfxKeyframe kf, int kfIndex)
        {
            bool isFirst = (kfIndex == 0);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.LabelField(
                isFirst ? $"キーフレーム @ {kf.Time:F2}秒（基準）" : $"キーフレーム @ {kf.Time:F2}秒",
                EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            // Time + Ease dropdown + easing curve
            EditorGUILayout.BeginHorizontal();
            kf.Time = EditorGUILayout.FloatField(
                new GUIContent("時刻（秒）"), kf.Time, GUILayout.Width(200));

            int easeIdx = Array.IndexOf(EaseValues, kf.Ease?.ToLowerInvariant());
            if (easeIdx < 0) easeIdx = 0;
            EditorGUILayout.LabelField(
                new GUIContent("補間", "キーフレーム間の変化の仕方"),
                GUILayout.Width(30));
            int newEaseIdx = EditorGUILayout.Popup(easeIdx, EaseDisplayNames, GUILayout.Width(70));
            if (newEaseIdx != easeIdx)
                kf.Ease = EaseValues[newEaseIdx];

            // Small easing curve preview
            var curveRect = GUILayoutUtility.GetRect(36, 18, GUILayout.Width(36));
            curveRect.y += 1;
            DrawEasingCurve(kf.Ease, curveRect);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            string shapeType = layer.Type?.ToLowerInvariant();
            if (!ShapeLayouts.TryGetValue(shapeType ?? "", out PropRow[] rows))
                rows = Array.Empty<PropRow>();

            if (isFirst)
            {
                // First keyframe: show all PropRows with category headers
                bool drawnGeometry = false;
                bool drawnAppearance = false;

                foreach (var row in rows)
                {
                    // Insert category headers
                    if (!row.IsAppearance && !drawnGeometry)
                    {
                        EditorGUILayout.LabelField("位置・形状", EditorStyles.boldLabel);
                        drawnGeometry = true;
                    }
                    else if (row.IsAppearance && !drawnAppearance)
                    {
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("見た目", EditorStyles.boldLabel);
                        drawnAppearance = true;
                    }

                    // Ensure defaults for first KF
                    foreach (var prop in row.Props)
                        EnsurePropertyDefault(kf, prop);

                    DrawPropertyRow(kf, row, canRemove: false);
                }
            }
            else
            {
                // Subsequent keyframes: only show rows that have any prop set
                bool drawnGeometry = false;
                bool drawnAppearance = false;

                foreach (var row in rows)
                {
                    if (!HasAnyPropInRow(kf, row)) continue;

                    if (!row.IsAppearance && !drawnGeometry)
                    {
                        EditorGUILayout.LabelField("位置・形状", EditorStyles.boldLabel);
                        drawnGeometry = true;
                    }
                    else if (row.IsAppearance && !drawnAppearance)
                    {
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("見た目", EditorStyles.boldLabel);
                        drawnAppearance = true;
                    }

                    DrawPropertyRow(kf, row, canRemove: true);
                }

                // "Add change" dropdown for unset rows
                var unsetRows = rows.Where(r => !HasAnyPropInRow(kf, r)).ToArray();
                if (unsetRows.Length > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (EditorGUILayout.DropdownButton(
                        new GUIContent("+ 変化を追加"), FocusType.Keyboard, GUILayout.Width(140)))
                    {
                        var menu = new GenericMenu();
                        foreach (var row in unsetRows)
                        {
                            var r = row;
                            menu.AddItem(new GUIContent(r.Label), false, () =>
                            {
                                foreach (var prop in r.Props)
                                    InitPropertyDefault(kf, prop);
                                MarkDirty();
                            });
                        }
                        menu.ShowAsContext();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (EditorGUI.EndChangeCheck())
                MarkDirty();

            EditorGUILayout.EndVertical();
        }

        // =============================================================
        // DrawPropertyRow
        // =============================================================
        private void DrawPropertyRow(KfxKeyframe kf, PropRow row, bool canRemove)
        {
            // Special handling for pen/brush
            if (row.Props.Length == 1 && row.Props[0] == "pen")
            {
                DrawPenSection(kf, canRemove);
                return;
            }
            if (row.Props.Length == 1 && row.Props[0] == "brush")
            {
                DrawBrushSection(kf, canRemove);
                return;
            }
            if (row.Props.Length == 1 && row.Props[0] == "opacity")
            {
                EditorGUILayout.BeginHorizontal();
                kf.Opacity = EditorGUILayout.Slider(
                    new GUIContent("不透明度", "0=透明、1=不透明。全ての色のアルファに乗算"),
                    kf.Opacity ?? 1f, 0f, 1f);
                if (canRemove)
                {
                    if (GUILayout.Button("x", GUILayout.Width(20), GUILayout.Height(18)))
                    {
                        ClearProperty(kf, "opacity");
                        MarkDirty();
                    }
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (row.Props.Length == 1)
            {
                // Single property: standard FloatField
                string prop = row.Props[0];
                EditorGUILayout.BeginHorizontal();
                float val = GetPropValue(kf, prop);
                float newVal = EditorGUILayout.FloatField(row.Label, val);
                if (!Mathf.Approximately(val, newVal))
                    SetPropValue(kf, prop, newVal);
                if (canRemove)
                {
                    if (GUILayout.Button("x", GUILayout.Width(20), GUILayout.Height(18)))
                    {
                        ClearProperty(kf, prop);
                        MarkDirty();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else if (row.Props.Length == 2)
            {
                // Pair: Label + shortLabelA + FF + shortLabelB + FF on one line
                string propA = row.Props[0];
                string propB = row.Props[1];

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(row.Label, GUILayout.Width(50));

                EditorGUILayout.LabelField(GetPropShortLabel(propA), GUILayout.Width(24));
                float valA = GetPropValue(kf, propA);
                float newA = EditorGUILayout.FloatField(valA, GUILayout.Width(55));
                if (!Mathf.Approximately(valA, newA))
                    SetPropValue(kf, propA, newA);

                EditorGUILayout.LabelField(GetPropShortLabel(propB), GUILayout.Width(24));
                float valB = GetPropValue(kf, propB);
                float newB = EditorGUILayout.FloatField(valB, GUILayout.Width(55));
                if (!Mathf.Approximately(valB, newB))
                    SetPropValue(kf, propB, newB);

                if (canRemove)
                {
                    if (GUILayout.Button("x", GUILayout.Width(20), GUILayout.Height(18)))
                    {
                        foreach (var p in row.Props)
                            ClearProperty(kf, p);
                        MarkDirty();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static string GetPropShortLabel(string prop)
        {
            return prop switch
            {
                "x" => "X",
                "y" => "Y",
                "rx" => "Rx",
                "ry" => "Ry",
                "width" => "幅",
                "height" => "高さ",
                "width_start" => "始",
                "width_end" => "終",
                "startAngle" => "始",
                "endAngle" => "終",
                "x1" => "X1",
                "y1" => "Y1",
                "x2" => "X2",
                "y2" => "Y2",
                _ => prop
            };
        }

        // =============================================================
        // DrawPenSection (日本語: "線（輪郭）")
        // =============================================================
        private void DrawPenSection(KfxKeyframe kf, bool canRemove)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("線（輪郭）", EditorStyles.boldLabel);
            if (canRemove && GUILayout.Button("x", GUILayout.Width(20), GUILayout.Height(18)))
            {
                kf.Pen = null;
                MarkDirty();
                EditorGUILayout.EndHorizontal();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (kf.Pen == null) kf.Pen = new KfxPenKeyframe();
            EditorGUI.indentLevel++;
            kf.Pen.Color = DrawColorField("色", kf.Pen.Color ?? "#FFFFFF");
            kf.Pen.Width = EditorGUILayout.FloatField("太さ", kf.Pen.Width ?? 1f);
            EditorGUI.indentLevel--;
        }

        // =============================================================
        // DrawBrushSection (日本語: "塗り（塗りつぶし）")
        // =============================================================
        private void DrawBrushSection(KfxKeyframe kf, bool canRemove)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("塗り（塗りつぶし）", EditorStyles.boldLabel);
            if (canRemove && GUILayout.Button("x", GUILayout.Width(20), GUILayout.Height(18)))
            {
                kf.Brush = null;
                MarkDirty();
                EditorGUILayout.EndHorizontal();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (kf.Brush == null) kf.Brush = new KfxBrushKeyframe();
            EditorGUI.indentLevel++;

            string curType = kf.Brush.Type?.ToLowerInvariant() ?? "flat";
            int btIdx = Array.IndexOf(BrushTypeValues, curType);
            if (btIdx < 0) btIdx = 0;
            btIdx = EditorGUILayout.Popup("タイプ", btIdx, BrushTypeDisplayNames);
            kf.Brush.Type = BrushTypeValues[btIdx] == "flat" ? null : BrushTypeValues[btIdx];

            switch (BrushTypeValues[btIdx])
            {
                case "flat":
                    kf.Brush.Color = DrawColorField("色", kf.Brush.Color ?? "#FFFFFF80");
                    break;
                case "radial":
                    kf.Brush.Center = DrawColorField("中心", kf.Brush.Center ?? "#FFFFFFCC");
                    kf.Brush.Edge = DrawColorField("端", kf.Brush.Edge ?? "#FFFFFF00");
                    break;
                case "linear":
                    kf.Brush.Start = DrawColorField("開始", kf.Brush.Start ?? "#FFFFFFCC");
                    kf.Brush.End = DrawColorField("終了", kf.Brush.End ?? "#FFFFFF00");
                    kf.Brush.Angle = EditorGUILayout.FloatField("角度", kf.Brush.Angle ?? 0);
                    break;
            }
            EditorGUI.indentLevel--;
        }

        // =============================================================
        // DrawEasingCurve
        // =============================================================
        private void DrawEasingCurve(string ease, Rect rect)
        {
            // Background
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            Handles.BeginGUI();
            Handles.color = new Color(0.6f, 0.8f, 1f, 0.8f);

            int steps = 20;
            Vector3 prev = new Vector3(rect.x, rect.yMax, 0);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float v = KfxEasing.Apply(ease, t);
                Vector3 next = new Vector3(
                    rect.x + t * rect.width,
                    rect.yMax - v * rect.height,
                    0);
                Handles.DrawLine(prev, next);
                prev = next;
            }
            Handles.EndGUI();
        }

        // =============================================================
        // DrawColorField (ラベル幅60)
        // =============================================================
        private string DrawColorField(string label, string hexColor)
        {
            Color color = EffectColorUtility.ParseColor(hexColor);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(60));
            color = EditorGUILayout.ColorField(GUIContent.none, color,
                showEyedropper: true, showAlpha: true, hdr: false, GUILayout.Width(50));
            string newHex = EffectColorUtility.ColorToHex(color);
            EditorGUILayout.SelectableLabel(newHex, EditorStyles.miniLabel,
                GUILayout.Width(80), GUILayout.Height(16));
            EditorGUILayout.EndHorizontal();
            return newHex;
        }

        // =============================================================
        // DrawRangeSlider (MinMaxSlider)
        // =============================================================
        private void DrawRangeSlider(string label, ref float min, ref float max, float sliderMin, float sliderMax)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(60));
            min = EditorGUILayout.FloatField(min, GUILayout.Width(45));
            EditorGUILayout.MinMaxSlider(ref min, ref max, sliderMin, sliderMax);
            max = EditorGUILayout.FloatField(max, GUILayout.Width(45));
            EditorGUILayout.EndHorizontal();
        }

        // =============================================================
        // Property Helpers
        // =============================================================
        private bool HasProperty(KfxKeyframe kf, string prop)
        {
            return prop switch
            {
                "x" => kf.X.HasValue,
                "y" => kf.Y.HasValue,
                "radius" => kf.Radius.HasValue,
                "inner_radius" => kf.InnerRadius.HasValue,
                "rx" => kf.Rx.HasValue,
                "ry" => kf.Ry.HasValue,
                "width" => kf.Width.HasValue,
                "height" => kf.Height.HasValue,
                "rotation" => kf.Rotation.HasValue,
                "size" => kf.Size.HasValue,
                "x1" => kf.X1.HasValue,
                "y1" => kf.Y1.HasValue,
                "x2" => kf.X2.HasValue,
                "y2" => kf.Y2.HasValue,
                "width_start" => kf.WidthStart.HasValue,
                "width_end" => kf.WidthEnd.HasValue,
                "startAngle" => kf.StartAngle.HasValue,
                "endAngle" => kf.EndAngle.HasValue,
                "opacity" => kf.Opacity.HasValue,
                "pen" => kf.Pen != null,
                "brush" => kf.Brush != null,
                _ => false
            };
        }

        private void ClearProperty(KfxKeyframe kf, string prop)
        {
            switch (prop)
            {
                case "x": kf.X = null; break;
                case "y": kf.Y = null; break;
                case "radius": kf.Radius = null; break;
                case "inner_radius": kf.InnerRadius = null; break;
                case "rx": kf.Rx = null; break;
                case "ry": kf.Ry = null; break;
                case "width": kf.Width = null; break;
                case "height": kf.Height = null; break;
                case "rotation": kf.Rotation = null; break;
                case "size": kf.Size = null; break;
                case "x1": kf.X1 = null; break;
                case "y1": kf.Y1 = null; break;
                case "x2": kf.X2 = null; break;
                case "y2": kf.Y2 = null; break;
                case "width_start": kf.WidthStart = null; break;
                case "width_end": kf.WidthEnd = null; break;
                case "startAngle": kf.StartAngle = null; break;
                case "endAngle": kf.EndAngle = null; break;
                case "opacity": kf.Opacity = null; break;
                case "pen": kf.Pen = null; break;
                case "brush": kf.Brush = null; break;
            }
        }

        private void InitPropertyDefault(KfxKeyframe kf, string prop)
        {
            switch (prop)
            {
                case "x": kf.X = 50; break;
                case "y": kf.Y = 50; break;
                case "radius": kf.Radius = 10; break;
                case "inner_radius": kf.InnerRadius = 5; break;
                case "rx": kf.Rx = 20; break;
                case "ry": kf.Ry = 10; break;
                case "width": kf.Width = 20; break;
                case "height": kf.Height = 20; break;
                case "rotation": kf.Rotation = 0; break;
                case "size": kf.Size = 5; break;
                case "x1": kf.X1 = 20; break;
                case "y1": kf.Y1 = 20; break;
                case "x2": kf.X2 = 80; break;
                case "y2": kf.Y2 = 80; break;
                case "width_start": kf.WidthStart = 5; break;
                case "width_end": kf.WidthEnd = 1; break;
                case "startAngle": kf.StartAngle = 0; break;
                case "endAngle": kf.EndAngle = 360; break;
                case "opacity": kf.Opacity = 1f; break;
                case "pen": kf.Pen = new KfxPenKeyframe { Color = "#FFFFFF", Width = 1 }; break;
                case "brush": kf.Brush = new KfxBrushKeyframe { Color = "#FFFFFF80" }; break;
            }
        }

        private void EnsurePropertyDefault(KfxKeyframe kf, string prop)
        {
            if (!HasProperty(kf, prop))
                InitPropertyDefault(kf, prop);
        }

        private bool HasAnyPropInRow(KfxKeyframe kf, PropRow row)
        {
            foreach (var prop in row.Props)
            {
                if (HasProperty(kf, prop))
                    return true;
            }
            return false;
        }

        private float GetPropValue(KfxKeyframe kf, string prop)
        {
            return prop switch
            {
                "x" => kf.X ?? 50,
                "y" => kf.Y ?? 50,
                "radius" => kf.Radius ?? 10,
                "inner_radius" => kf.InnerRadius ?? 5,
                "rx" => kf.Rx ?? 20,
                "ry" => kf.Ry ?? 10,
                "width" => kf.Width ?? 20,
                "height" => kf.Height ?? 20,
                "rotation" => kf.Rotation ?? 0,
                "size" => kf.Size ?? 5,
                "x1" => kf.X1 ?? 20,
                "y1" => kf.Y1 ?? 20,
                "x2" => kf.X2 ?? 80,
                "y2" => kf.Y2 ?? 80,
                "width_start" => kf.WidthStart ?? 5,
                "width_end" => kf.WidthEnd ?? 1,
                "startAngle" => kf.StartAngle ?? 0,
                "endAngle" => kf.EndAngle ?? 360,
                "opacity" => kf.Opacity ?? 1f,
                _ => 0f
            };
        }

        private void SetPropValue(KfxKeyframe kf, string prop, float value)
        {
            switch (prop)
            {
                case "x": kf.X = value; break;
                case "y": kf.Y = value; break;
                case "radius": kf.Radius = value; break;
                case "inner_radius": kf.InnerRadius = value; break;
                case "rx": kf.Rx = value; break;
                case "ry": kf.Ry = value; break;
                case "width": kf.Width = value; break;
                case "height": kf.Height = value; break;
                case "rotation": kf.Rotation = value; break;
                case "size": kf.Size = value; break;
                case "x1": kf.X1 = value; break;
                case "y1": kf.Y1 = value; break;
                case "x2": kf.X2 = value; break;
                case "y2": kf.Y2 = value; break;
                case "width_start": kf.WidthStart = value; break;
                case "width_end": kf.WidthEnd = value; break;
                case "startAngle": kf.StartAngle = value; break;
                case "endAngle": kf.EndAngle = value; break;
                case "opacity": kf.Opacity = value; break;
            }
        }

        // =============================================================
        // File Operations (日本語ログ)
        // =============================================================
        private void SaveFile()
        {
            if (_kfxDef == null) return;

            string path = _filePath;
            if (string.IsNullOrEmpty(path))
            {
                if (!Directory.Exists(EffectsPath))
                    Directory.CreateDirectory(EffectsPath);
                path = EditorUtility.SaveFilePanel("KFXエフェクトを保存", EffectsPath,
                    _kfxDef.Name + ".json", "json");
                if (string.IsNullOrEmpty(path)) return;
            }

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };
            string json = JsonConvert.SerializeObject(_kfxDef, settings);
            File.WriteAllText(path, json);
            _filePath = path;
            _isDirty = false;
            AssetDatabase.Refresh();
            RefreshFileList();
            Debug.Log($"[KfxEditor] 保存しました: {path}");
        }

        private void LoadFile(string effectName)
        {
            string path = Path.Combine(EffectsPath, effectName + ".json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[KfxEditor] ファイルが見つかりません: {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var jobj = Newtonsoft.Json.Linq.JObject.Parse(json);

                if (!KfxCompiler.IsKfxFormat(jobj))
                {
                    Debug.LogWarning($"[KfxEditor] '{effectName}' はKFXフォーマットではありません（フレームベース形式）。Effect Previewerをお使いください。");
                    return;
                }

                _kfxDef = jobj.ToObject<KfxDefinition>();
                _filePath = path;
                _isDirty = false;
                _selectedLayerIndex = _kfxDef.Layers.Count > 0 ? 0 : -1;
                _selectedKeyframeIndex = -1;
                _hiddenLayers.Clear();
                _undoStack.Clear();
                _redoStack.Clear();
                LoadSe();
                _needsRecompile = true;
                Debug.Log($"[KfxEditor] 読み込みました: {effectName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[KfxEditor] 読み込みに失敗しました: {e.Message}");
            }
        }

        private void RefreshFileList()
        {
            var list = new List<string>();
            if (Directory.Exists(EffectsPath))
            {
                foreach (var file in Directory.GetFiles(EffectsPath, "*.json"))
                {
                    list.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            _effectFiles = list.ToArray();
        }

        // =============================================================
        // Layer / Keyframe Operations
        // =============================================================
        private void AddLayer()
        {
            if (_kfxDef == null) return;

            int idx = _kfxDef.Layers.Count;
            _kfxDef.Layers.Add(new KfxLayer
            {
                Id = $"layer_{idx}",
                Type = "circle",
                Keyframes = new List<KfxKeyframe>
                {
                    new KfxKeyframe
                    {
                        Time = 0f,
                        Ease = "linear",
                        X = 50, Y = 50,
                        Radius = 10,
                        Brush = new KfxBrushKeyframe { Color = "#FFFFFF80" }
                    }
                }
            });
            _selectedLayerIndex = idx;
            _selectedKeyframeIndex = 0;
            MarkDirty();
        }

        private void AddKeyframe(KfxLayer layer)
        {
            if (layer.Keyframes == null)
                layer.Keyframes = new List<KfxKeyframe>();

            float newTime;
            if (_compiledDef != null && _compiledDef.Fps > 0)
            {
                newTime = _currentFrame / (float)_compiledDef.Fps;
            }
            else
            {
                newTime = layer.Keyframes.Count > 0
                    ? layer.Keyframes[layer.Keyframes.Count - 1].Time + 0.1f
                    : 0f;
            }

            var newKf = new KfxKeyframe { Time = newTime, Ease = "linear" };

            int insertIdx = layer.Keyframes.Count;
            for (int i = 0; i < layer.Keyframes.Count; i++)
            {
                if (layer.Keyframes[i].Time > newTime)
                {
                    insertIdx = i;
                    break;
                }
            }
            layer.Keyframes.Insert(insertIdx, newKf);
            _selectedKeyframeIndex = insertIdx;
            MarkDirty();
        }

        // =============================================================
        // Compilation & Preview
        // =============================================================

        private void Recompile()
        {
            if (_kfxDef == null)
            {
                _compiledDef = null;
                _compileError = null;
                CleanupTexture();
                return;
            }

            try
            {
                var error = _kfxDef.Validate();
                if (error != null)
                {
                    _compileError = $"バリデーション: {error}";
                    return;
                }

                _compiledDef = KfxCompiler.Compile(_kfxDef);
                _compileError = null;

                CleanupTexture();
                _renderer = new EffectRenderer(_compiledDef.Canvas);
                _previewTexture = _renderer.CreateTexture();
                _currentFrame = Mathf.Clamp(_currentFrame, 0,
                    Mathf.Max(0, _compiledDef.Frames.Count - 1));
                RenderCurrentFrame();
            }
            catch (Exception e)
            {
                _compileError = $"コンパイルエラー: {e.Message}";
                Debug.LogError($"[KfxEditor] {e}");
            }
        }

        private void RenderCurrentFrame()
        {
            if (_compiledDef == null || _renderer == null || _previewTexture == null) return;
            if (_currentFrame < 0 || _currentFrame >= _compiledDef.Frames.Count) return;

            var frame = _compiledDef.Frames[_currentFrame];
            _renderer.RenderFrame(_previewTexture, frame, _currentFrame);
        }

        private void GoToFrame(int frame)
        {
            if (_compiledDef == null) return;
            _currentFrame = Mathf.Clamp(frame, 0, _compiledDef.Frames.Count - 1);
            RenderCurrentFrame();
            Repaint();
        }

        private void CleanupTexture()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        // =============================================================
        // SE
        // =============================================================
        private void LoadSe()
        {
            string seName = _kfxDef?.Se;
            if (string.IsNullOrEmpty(seName)) { _seClip = null; _loadedSeName = null; return; }
            if (seName == _loadedSeName && _seClip != null) return;

            _seClip = Resources.Load<AudioClip>($"{EffectConstants.AudioResourcePath}{seName}");
            _loadedSeName = seName;
        }

        private void PlaySe()
        {
            LoadSe();
            if (_seClip == null) return;
            PlayClipInEditor(_seClip);
        }

        private void StopSe()
        {
            StopClipInEditor();
        }

        private static void PlayClipInEditor(AudioClip clip)
        {
            if (clip == null) return;
            var audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilType == null) return;

            StopClipInEditor();

            var playMethod = audioUtilType.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public, null,
                new Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            if (playMethod != null) { playMethod.Invoke(null, new object[] { clip, 0, false }); return; }

            playMethod = audioUtilType.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public, null,
                new Type[] { typeof(AudioClip) }, null);
            if (playMethod != null) { playMethod.Invoke(null, new object[] { clip }); return; }

            playMethod = audioUtilType.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (playMethod != null)
            {
                var ps = playMethod.GetParameters();
                if (ps.Length == 1) playMethod.Invoke(null, new object[] { clip });
                else if (ps.Length == 2) playMethod.Invoke(null, new object[] { clip, 0 });
                else if (ps.Length == 3) playMethod.Invoke(null, new object[] { clip, 0, false });
            }
        }

        private static void StopClipInEditor()
        {
            var audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilType == null) return;
            var stopMethod = audioUtilType.GetMethod("StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            stopMethod?.Invoke(null, null);
        }

        // =============================================================
        // Templates
        // =============================================================
        private static class KfxTemplates
        {
            public static KfxDefinition Empty()
            {
                return new KfxDefinition
                {
                    Format = "kfx", Name = "new_effect", Canvas = 100, Fps = 30, Duration = 1.0f,
                    Layers = new List<KfxLayer>
                    {
                        new KfxLayer
                        {
                            Id = "layer_0", Type = "circle",
                            Keyframes = new List<KfxKeyframe>
                            {
                                new KfxKeyframe { Time = 0f, Ease = "linear", X = 50, Y = 50, Radius = 10,
                                    Brush = new KfxBrushKeyframe { Color = "#FFFFFF80" } }
                            }
                        }
                    }
                };
            }

            public static KfxDefinition Pulse()
            {
                return new KfxDefinition
                {
                    Format = "kfx", Name = "pulse", Canvas = 100, Fps = 30, Duration = 1.0f,
                    Layers = new List<KfxLayer>
                    {
                        new KfxLayer
                        {
                            Id = "pulse", Type = "circle", Blend = "additive",
                            Keyframes = new List<KfxKeyframe>
                            {
                                new KfxKeyframe { Time = 0f, Ease = "easeOut", X = 50, Y = 50, Radius = 5, Opacity = 0.3f,
                                    Brush = new KfxBrushKeyframe { Type = "radial", Center = "#FFFFFF80", Edge = "#FFFFFF00" } },
                                new KfxKeyframe { Time = 0.5f, Radius = 25, Opacity = 1f },
                                new KfxKeyframe { Time = 1.0f, Radius = 5, Opacity = 0.3f }
                            }
                        }
                    }
                };
            }

            public static KfxDefinition Shockwave()
            {
                return new KfxDefinition
                {
                    Format = "kfx", Name = "shockwave", Canvas = 100, Fps = 30, Duration = 0.5f,
                    Layers = new List<KfxLayer>
                    {
                        new KfxLayer
                        {
                            Id = "flash", Type = "circle", Blend = "additive",
                            Visible = new List<float> { 0f, 0.15f },
                            Keyframes = new List<KfxKeyframe>
                            {
                                new KfxKeyframe { Time = 0f, Ease = "easeOut", X = 50, Y = 50, Radius = 3,
                                    Brush = new KfxBrushKeyframe { Type = "radial", Center = "#FFFFFFCC", Edge = "#FFFFFF00" } },
                                new KfxKeyframe { Time = 0.15f, Radius = 15, Opacity = 0f }
                            }
                        },
                        new KfxLayer
                        {
                            Id = "ring", Type = "ring", Blend = "additive",
                            Keyframes = new List<KfxKeyframe>
                            {
                                new KfxKeyframe { Time = 0f, Ease = "easeOut", X = 50, Y = 50, Radius = 5, InnerRadius = 3,
                                    Brush = new KfxBrushKeyframe { Color = "#FFAA4499" } },
                                new KfxKeyframe { Time = 0.5f, Radius = 40, InnerRadius = 37, Opacity = 0f }
                            }
                        }
                    }
                };
            }

            public static KfxDefinition Flash()
            {
                return new KfxDefinition
                {
                    Format = "kfx", Name = "flash", Canvas = 100, Fps = 30, Duration = 0.3f,
                    Layers = new List<KfxLayer>
                    {
                        new KfxLayer
                        {
                            Id = "flash", Type = "circle", Blend = "additive",
                            Keyframes = new List<KfxKeyframe>
                            {
                                new KfxKeyframe { Time = 0f, Ease = "easeIn", X = 50, Y = 50, Radius = 45, Opacity = 1f,
                                    Brush = new KfxBrushKeyframe { Type = "radial", Center = "#FFFFFFEE", Edge = "#FFFFFF00" } },
                                new KfxKeyframe { Time = 0.3f, Opacity = 0f }
                            }
                        }
                    }
                };
            }

            public static KfxDefinition FadeInOut()
            {
                return new KfxDefinition
                {
                    Format = "kfx", Name = "fade_inout", Canvas = 100, Fps = 30, Duration = 1.0f,
                    Layers = new List<KfxLayer>
                    {
                        new KfxLayer
                        {
                            Id = "glow", Type = "circle", Blend = "additive",
                            Keyframes = new List<KfxKeyframe>
                            {
                                new KfxKeyframe { Time = 0f, Ease = "easeOut", X = 50, Y = 50, Radius = 15, Opacity = 0f,
                                    Brush = new KfxBrushKeyframe { Type = "radial", Center = "#FFFFFF99", Edge = "#FFFFFF00" } },
                                new KfxKeyframe { Time = 0.3f, Ease = "linear", Opacity = 1f },
                                new KfxKeyframe { Time = 0.7f, Ease = "easeIn", Opacity = 1f },
                                new KfxKeyframe { Time = 1.0f, Opacity = 0f }
                            }
                        }
                    }
                };
            }

            public static KfxDefinition ParticleBurst()
            {
                return new KfxDefinition
                {
                    Format = "kfx", Name = "particle_burst", Canvas = 100, Fps = 30, Duration = 1.5f,
                    Layers = new List<KfxLayer>
                    {
                        new KfxLayer
                        {
                            Id = "particles", Type = "emitter", Blend = "additive",
                            X = 50, Y = 50, Count = 20,
                            AngleRange = new List<float> { 0, 360 },
                            SpeedRange = new List<float> { 1.5f, 4f },
                            Gravity = 0.1f, Drag = 0.95f, Lifetime = 1.2f,
                            SizeRange = new List<float> { 1f, 3f },
                            ColorStart = "#FFCC44CC", ColorEnd = "#FF440000",
                            Seed = 42
                        }
                    }
                };
            }

            public static KfxDefinition Slash()
            {
                return new KfxDefinition
                {
                    Format = "kfx", Name = "slash", Canvas = 100, Fps = 30, Duration = 0.5f,
                    Layers = new List<KfxLayer>
                    {
                        new KfxLayer
                        {
                            Id = "slash_line", Type = "tapered_line",
                            Visible = new List<float> { 0f, 0.25f },
                            Keyframes = new List<KfxKeyframe>
                            {
                                new KfxKeyframe { Time = 0f, Ease = "easeOut",
                                    X1 = 75, Y1 = 25, X2 = 75, Y2 = 25, WidthStart = 4, WidthEnd = 0.5f,
                                    Pen = new KfxPenKeyframe { Color = "#FFFFFFEE" } },
                                new KfxKeyframe { Time = 0.15f, X1 = 25, Y1 = 75, Opacity = 0.8f },
                                new KfxKeyframe { Time = 0.25f, Opacity = 0f }
                            }
                        },
                        new KfxLayer
                        {
                            Id = "impact_ring", Type = "ring", Blend = "additive",
                            Visible = new List<float> { 0.1f, 0.5f },
                            Keyframes = new List<KfxKeyframe>
                            {
                                new KfxKeyframe { Time = 0.1f, Ease = "easeOut", X = 50, Y = 50, Radius = 5, InnerRadius = 3,
                                    Brush = new KfxBrushKeyframe { Color = "#FFFFFF66" } },
                                new KfxKeyframe { Time = 0.5f, Radius = 35, InnerRadius = 32, Opacity = 0f }
                            }
                        }
                    }
                };
            }
        }

        // =============================================================
        // ShortcutReferenceWindow 内部クラス
        // =============================================================
        private class ShortcutReferenceWindow : EditorWindow
        {
            private Vector2 _scroll;

            public static void Show()
            {
                var w = GetWindow<ShortcutReferenceWindow>(
                    utility: true, title: "キーボードショートカット一覧");
                w.minSize = new Vector2(380, 480);
                w.maxSize = new Vector2(420, 600);
                w.ShowUtility();
            }

            private void OnGUI()
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                DrawCategory("ファイル・編集", new[] {
                    ("Ctrl+S",          "保存"),
                    ("Ctrl+Z",          "元に戻す"),
                    ("Ctrl+Y",          "やり直し"),
                });

                DrawCategory("再生制御", new[] {
                    ("Space",           "再生 / 一時停止"),
                    ("Shift+Space",     "先頭から再生"),
                    ("Backspace",       "停止して先頭に戻る"),
                    ("Ctrl+L",          "ループ切替"),
                    ("Ctrl+= / Ctrl+-", "再生速度 変更"),
                });

                DrawCategory("タイムライン移動", new[] {
                    ("\u2190 / \u2192",             "1フレーム移動"),
                    ("Shift+\u2190 / \u2192",       "10フレーム移動"),
                    ("Ctrl+\u2190 / \u2192",        "前/次のキーフレームへジャンプ"),
                    ("Home / End",      "先頭 / 末尾"),
                });

                DrawCategory("レイヤー操作", new[] {
                    ("Alt+\u2191 / \u2193",         "レイヤー選択移動"),
                    ("Ctrl+Shift+\u2191/\u2193",    "レイヤー並べ替え"),
                    ("K",               "現在時刻にキーフレーム追加"),
                    ("F2",              "レイヤーID名を編集"),
                });

                DrawCategory("キーフレーム操作", new[] {
                    ("\u2191 / \u2193",             "キーフレーム選択移動"),
                    ("Ctrl+D",          "複製"),
                    ("Delete",          "削除"),
                    ("[ / ]",           "時刻を微調整（\u00B10.01秒）"),
                    ("Shift+[ / ]",     "時刻を大調整（\u00B10.1秒）"),
                    ("Escape",          "選択解除"),
                });

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(
                    "このウィンドウ: ? または F1 で開く",
                    EditorStyles.centeredGreyMiniLabel);

                EditorGUILayout.EndScrollView();
            }

            private void DrawCategory(string title, (string key, string desc)[] entries)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                foreach (var (key, desc) in entries)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(key, EditorStyles.miniLabel,
                        GUILayout.Width(140));
                    EditorGUILayout.LabelField(desc);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

    } // end partial class KfxEditorWindow
} // end namespace EffectsEditor
