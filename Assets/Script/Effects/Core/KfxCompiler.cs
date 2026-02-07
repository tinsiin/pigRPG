using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Effects.Core
{
    /// <summary>
    /// KFX定義をEffectDefinitionにコンパイルするコンパイラ
    /// </summary>
    public static class KfxCompiler
    {
        /// <summary>
        /// JSON文字列からEffectDefinitionを読み込む（KFX/従来形式を自動判別）
        /// </summary>
        public static EffectDefinition LoadFromJson(string json)
        {
            var jobj = JObject.Parse(json);

            if (IsKfxFormat(jobj))
            {
                var kfx = jobj.ToObject<KfxDefinition>();
                var error = kfx.Validate();
                if (error != null)
                {
                    Debug.LogError($"[KfxCompiler] Validation failed: {error}");
                    return null;
                }
                return Compile(kfx);
            }

            return jobj.ToObject<EffectDefinition>();
        }

        /// <summary>
        /// KFX形式かどうか判定
        /// </summary>
        public static bool IsKfxFormat(JObject jobj)
        {
            var format = jobj["format"]?.Value<string>();
            if (format == "kfx") return true;
            if (jobj["layers"] != null && jobj["frames"] == null) return true;
            return false;
        }

        /// <summary>
        /// KfxDefinition → EffectDefinition
        /// </summary>
        public static EffectDefinition Compile(KfxDefinition kfx)
        {
            int totalFrames = Mathf.CeilToInt(kfx.Duration * kfx.Fps);
            if (totalFrames < 1) totalFrames = 1;

            float frameDuration = 1f / kfx.Fps;

            // レイヤーの前処理
            var layerInfos = new List<LayerInfo>();
            foreach (var layer in kfx.Layers)
            {
                if (layer.IsEmitter)
                {
                    layerInfos.Add(new LayerInfo
                    {
                        Layer = layer,
                        IsEmitter = true,
                        EmitterShape = BuildEmitterShape(layer, kfx.Fps)
                    });
                }
                else
                {
                    layerInfos.Add(new LayerInfo
                    {
                        Layer = layer,
                        IsEmitter = false,
                        ResolvedKeyframes = ResolveKeyframes(layer)
                    });
                }
            }

            // フレーム生成
            var result = new EffectDefinition
            {
                Name = kfx.Name,
                Canvas = kfx.Canvas,
                Fps = kfx.Fps,
                Se = kfx.Se,
                // 配置メタデータのパススルー
                Target = kfx.Target ?? "icon",
                IconRect = kfx.IconRect,
                FieldRect = kfx.FieldRect,
                FieldLayer = kfx.FieldLayer ?? "middle",
                Frames = new List<FrameDefinition>()
            };

            for (int fi = 0; fi < totalFrames; fi++)
            {
                float time = fi * frameDuration;
                var frame = new FrameDefinition
                {
                    Shapes = new List<ShapeDefinition>()
                };

                foreach (var info in layerInfos)
                {
                    // 表示範囲チェック
                    if (!IsVisible(info.Layer, time))
                        continue;

                    if (info.IsEmitter)
                    {
                        // 初回可視フレームを記録
                        if (info.EmitterFirstVisibleFrame < 0)
                            info.EmitterFirstVisibleFrame = fi;

                        int localFrame = fi - info.EmitterFirstVisibleFrame;
                        int lifetimeFrames = info.EmitterShape.Lifetime;
                        if (localFrame < lifetimeFrames)
                        {
                            var clone = CloneEmitterShape(info.EmitterShape);
                            clone.EmitterLocalFrame = localFrame;
                            frame.Shapes.Add(clone);
                        }
                    }
                    else
                    {
                        var shape = InterpolateLayer(info, time);
                        if (shape != null)
                        {
                            frame.Shapes.Add(shape);
                        }
                    }
                }

                result.Frames.Add(frame);
            }

            result.Normalize();
            return result;
        }

        #region Keyframe Resolution

        /// <summary>
        /// 部分キーフレームを完全スナップショットに解決
        /// </summary>
        private static List<ResolvedKeyframe> ResolveKeyframes(KfxLayer layer)
        {
            var result = new List<ResolvedKeyframe>();
            var prev = new ResolvedKeyframe();

            foreach (var kf in layer.Keyframes)
            {
                var r = new ResolvedKeyframe
                {
                    Time = kf.Time,
                    Ease = kf.Ease ?? "linear",
                    X = kf.X ?? prev.X,
                    Y = kf.Y ?? prev.Y,
                    Radius = kf.Radius ?? prev.Radius,
                    Rx = kf.Rx ?? prev.Rx,
                    Ry = kf.Ry ?? prev.Ry,
                    StartAngle = kf.StartAngle ?? prev.StartAngle,
                    EndAngle = kf.EndAngle ?? prev.EndAngle,
                    Width = kf.Width ?? prev.Width,
                    Height = kf.Height ?? prev.Height,
                    Rotation = kf.Rotation ?? prev.Rotation,
                    Size = kf.Size ?? prev.Size,
                    X1 = kf.X1 ?? prev.X1,
                    Y1 = kf.Y1 ?? prev.Y1,
                    X2 = kf.X2 ?? prev.X2,
                    Y2 = kf.Y2 ?? prev.Y2,
                    WidthStart = kf.WidthStart ?? prev.WidthStart,
                    WidthEnd = kf.WidthEnd ?? prev.WidthEnd,
                    InnerRadius = kf.InnerRadius ?? prev.InnerRadius,
                    Opacity = kf.Opacity ?? prev.Opacity
                };

                // Pen: マージ（個別フィールドを継承）
                if (kf.Pen != null)
                {
                    r.PenColor = kf.Pen.Color ?? prev.PenColor;
                    r.PenWidth = kf.Pen.Width ?? prev.PenWidth;
                }
                else
                {
                    r.PenColor = prev.PenColor;
                    r.PenWidth = prev.PenWidth;
                }

                // Brush: マージ
                if (kf.Brush != null)
                {
                    r.BrushType = kf.Brush.Type ?? prev.BrushType;
                    r.BrushColor = kf.Brush.Color ?? prev.BrushColor;
                    r.BrushCenter = kf.Brush.Center ?? prev.BrushCenter;
                    r.BrushEdge = kf.Brush.Edge ?? prev.BrushEdge;
                    r.BrushStart = kf.Brush.Start ?? prev.BrushStart;
                    r.BrushEnd = kf.Brush.End ?? prev.BrushEnd;
                    r.BrushAngle = kf.Brush.Angle ?? prev.BrushAngle;
                }
                else
                {
                    r.BrushType = prev.BrushType;
                    r.BrushColor = prev.BrushColor;
                    r.BrushCenter = prev.BrushCenter;
                    r.BrushEdge = prev.BrushEdge;
                    r.BrushStart = prev.BrushStart;
                    r.BrushEnd = prev.BrushEnd;
                    r.BrushAngle = prev.BrushAngle;
                }

                prev = r;
                result.Add(r);
            }

            return result;
        }

        #endregion

        #region Interpolation

        /// <summary>
        /// 指定時刻でのレイヤーの図形を補間生成
        /// </summary>
        private static ShapeDefinition InterpolateLayer(LayerInfo info, float time)
        {
            var resolved = info.ResolvedKeyframes;
            if (resolved.Count == 0) return null;

            if (resolved.Count == 1)
                return BuildShape(info.Layer, resolved[0]);

            // 時刻が先頭キーフレームより前
            if (time <= resolved[0].Time)
                return BuildShape(info.Layer, resolved[0]);

            // 時刻が末尾キーフレーム以降
            if (time >= resolved[resolved.Count - 1].Time)
                return BuildShape(info.Layer, resolved[resolved.Count - 1]);

            // 前後のキーフレームを探索
            for (int i = 0; i < resolved.Count - 1; i++)
            {
                var kfA = resolved[i];
                var kfB = resolved[i + 1];

                if (time >= kfA.Time && time < kfB.Time)
                {
                    float span = kfB.Time - kfA.Time;
                    float rawT = span > 0 ? (time - kfA.Time) / span : 0f;
                    float t = KfxEasing.Apply(kfA.Ease, rawT);

                    var interpolated = LerpResolved(kfA, kfB, t);
                    return BuildShape(info.Layer, interpolated);
                }
            }

            return BuildShape(info.Layer, resolved[resolved.Count - 1]);
        }

        /// <summary>
        /// 2つの解決済みキーフレーム間を補間
        /// </summary>
        private static ResolvedKeyframe LerpResolved(ResolvedKeyframe a, ResolvedKeyframe b, float t)
        {
            return new ResolvedKeyframe
            {
                X = Mathf.Lerp(a.X, b.X, t),
                Y = Mathf.Lerp(a.Y, b.Y, t),
                Radius = Mathf.Lerp(a.Radius, b.Radius, t),
                Rx = Mathf.Lerp(a.Rx, b.Rx, t),
                Ry = Mathf.Lerp(a.Ry, b.Ry, t),
                StartAngle = Mathf.Lerp(a.StartAngle, b.StartAngle, t),
                EndAngle = Mathf.Lerp(a.EndAngle, b.EndAngle, t),
                Width = Mathf.Lerp(a.Width, b.Width, t),
                Height = Mathf.Lerp(a.Height, b.Height, t),
                Rotation = Mathf.Lerp(a.Rotation, b.Rotation, t),
                Size = Mathf.Lerp(a.Size, b.Size, t),
                X1 = Mathf.Lerp(a.X1, b.X1, t),
                Y1 = Mathf.Lerp(a.Y1, b.Y1, t),
                X2 = Mathf.Lerp(a.X2, b.X2, t),
                Y2 = Mathf.Lerp(a.Y2, b.Y2, t),
                WidthStart = Mathf.Lerp(a.WidthStart, b.WidthStart, t),
                WidthEnd = Mathf.Lerp(a.WidthEnd, b.WidthEnd, t),
                InnerRadius = Mathf.Lerp(a.InnerRadius, b.InnerRadius, t),
                Opacity = Mathf.Lerp(a.Opacity, b.Opacity, t),
                PenColor = LerpColorHex(a.PenColor, b.PenColor, t),
                PenWidth = Mathf.Lerp(a.PenWidth, b.PenWidth, t),
                BrushType = a.BrushType,
                BrushColor = LerpColorHex(a.BrushColor, b.BrushColor, t),
                BrushCenter = LerpColorHex(a.BrushCenter, b.BrushCenter, t),
                BrushEdge = LerpColorHex(a.BrushEdge, b.BrushEdge, t),
                BrushStart = LerpColorHex(a.BrushStart, b.BrushStart, t),
                BrushEnd = LerpColorHex(a.BrushEnd, b.BrushEnd, t),
                BrushAngle = Mathf.Lerp(a.BrushAngle, b.BrushAngle, t)
            };
        }

        #endregion

        #region Shape Building

        /// <summary>
        /// 解決済みキーフレームからShapeDefinitionを構築
        /// </summary>
        private static ShapeDefinition BuildShape(KfxLayer layer, ResolvedKeyframe r)
        {
            var shape = new ShapeDefinition
            {
                Type = layer.Type,
                Blend = layer.Blend
            };

            string shapeType = layer.Type?.ToLowerInvariant();

            switch (shapeType)
            {
                case "point":
                    shape.X = r.X;
                    shape.Y = r.Y;
                    shape.Size = r.Size;
                    break;
                case "line":
                    shape.X1 = r.X1;
                    shape.Y1 = r.Y1;
                    shape.X2 = r.X2;
                    shape.Y2 = r.Y2;
                    break;
                case "circle":
                    shape.X = r.X;
                    shape.Y = r.Y;
                    shape.Radius = r.Radius;
                    break;
                case "ellipse":
                    shape.X = r.X;
                    shape.Y = r.Y;
                    shape.Rx = r.Rx;
                    shape.Ry = r.Ry;
                    shape.Rotation = r.Rotation;
                    break;
                case "arc":
                    shape.X = r.X;
                    shape.Y = r.Y;
                    shape.Radius = r.Radius;
                    shape.StartAngle = r.StartAngle;
                    shape.EndAngle = r.EndAngle;
                    break;
                case "rect":
                    shape.X = r.X;
                    shape.Y = r.Y;
                    shape.Width = r.Width;
                    shape.Height = r.Height;
                    shape.Rotation = r.Rotation;
                    break;
                case "tapered_line":
                    shape.X1 = r.X1;
                    shape.Y1 = r.Y1;
                    shape.X2 = r.X2;
                    shape.Y2 = r.Y2;
                    shape.WidthStart = r.WidthStart;
                    shape.WidthEnd = r.WidthEnd;
                    break;
                case "ring":
                    shape.X = r.X;
                    shape.Y = r.Y;
                    shape.Radius = r.Radius;
                    shape.InnerRadius = r.InnerRadius;
                    break;
            }

            // Pen（opacity適用）
            if (!string.IsNullOrEmpty(r.PenColor))
            {
                shape.Pen = new PenDefinition
                {
                    Color = ApplyOpacity(r.PenColor, r.Opacity),
                    Width = r.PenWidth > 0 ? r.PenWidth : EffectConstants.PenWidthDefault
                };
            }

            // Brush（opacity適用）
            string brushType = r.BrushType?.ToLowerInvariant();
            if (brushType == "radial" && !string.IsNullOrEmpty(r.BrushCenter))
            {
                shape.Brush = new BrushDefinition
                {
                    Type = "radial",
                    Center = ApplyOpacity(r.BrushCenter, r.Opacity),
                    Edge = ApplyOpacity(r.BrushEdge, r.Opacity)
                };
            }
            else if (brushType == "linear" && !string.IsNullOrEmpty(r.BrushStart))
            {
                shape.Brush = new BrushDefinition
                {
                    Type = "linear",
                    Start = ApplyOpacity(r.BrushStart, r.Opacity),
                    End = ApplyOpacity(r.BrushEnd, r.Opacity),
                    Angle = r.BrushAngle
                };
            }
            else if (!string.IsNullOrEmpty(r.BrushColor))
            {
                shape.Brush = new BrushDefinition
                {
                    Color = ApplyOpacity(r.BrushColor, r.Opacity)
                };
            }

            return shape;
        }

        /// <summary>
        /// エミッタレイヤーからShapeDefinitionを構築
        /// </summary>
        private static ShapeDefinition BuildEmitterShape(KfxLayer layer, int fps)
        {
            return new ShapeDefinition
            {
                Type = "emitter",
                X = layer.X ?? 50,
                Y = layer.Y ?? 50,
                Count = layer.Count ?? 10,
                AngleRange = layer.AngleRange,
                SpeedRange = layer.SpeedRange,
                Gravity = layer.Gravity ?? 0,
                Drag = layer.Drag ?? 1f,
                Lifetime = Mathf.RoundToInt((layer.Lifetime ?? 1f) * fps),
                SizeRange = layer.SizeRange,
                ColorStart = layer.ColorStart,
                ColorEnd = layer.ColorEnd,
                Blend = layer.Blend,
                Seed = layer.Seed ?? 0
            };
        }

        private static ShapeDefinition CloneEmitterShape(ShapeDefinition src)
        {
            return new ShapeDefinition
            {
                Type = src.Type,
                X = src.X,
                Y = src.Y,
                Count = src.Count,
                AngleRange = src.AngleRange,
                SpeedRange = src.SpeedRange,
                Gravity = src.Gravity,
                Drag = src.Drag,
                Lifetime = src.Lifetime,
                SizeRange = src.SizeRange,
                ColorStart = src.ColorStart,
                ColorEnd = src.ColorEnd,
                Blend = src.Blend,
                Seed = src.Seed,
                EmitterLocalFrame = src.EmitterLocalFrame
            };
        }

        #endregion

        #region Color Utilities

        private static string LerpColorHex(string hexA, string hexB, float t)
        {
            if (string.IsNullOrEmpty(hexA) && string.IsNullOrEmpty(hexB))
                return null;
            if (string.IsNullOrEmpty(hexA)) return hexB;
            if (string.IsNullOrEmpty(hexB)) return hexA;

            Color a = EffectColorUtility.ParseColor(hexA);
            Color b = EffectColorUtility.ParseColor(hexB);
            Color result = Color.Lerp(a, b, t);
            return EffectColorUtility.ColorToHex(result);
        }

        private static string ApplyOpacity(string hexColor, float opacity)
        {
            if (string.IsNullOrEmpty(hexColor)) return hexColor;
            if (opacity >= 1f) return hexColor;

            Color c = EffectColorUtility.ParseColor(hexColor);
            c.a *= Mathf.Clamp01(opacity);
            return EffectColorUtility.ColorToHex(c);
        }


        #endregion

        #region Helpers

        private static bool IsVisible(KfxLayer layer, float time)
        {
            if (layer.Visible == null || layer.Visible.Count < 2)
                return true;
            return time >= layer.Visible[0] && time <= layer.Visible[1];
        }

        #endregion

        #region Internal Types

        private class LayerInfo
        {
            public KfxLayer Layer;
            public bool IsEmitter;
            public ShapeDefinition EmitterShape;
            public List<ResolvedKeyframe> ResolvedKeyframes;
            public int EmitterFirstVisibleFrame = -1;
        }

        private class ResolvedKeyframe
        {
            public float Time;
            public string Ease = "linear";

            // Geometry
            public float X, Y;
            public float Radius, Rx, Ry;
            public float StartAngle, EndAngle;
            public float Width, Height;
            public float Rotation;
            public float Size;
            public float X1, Y1, X2, Y2;
            public float WidthStart, WidthEnd;
            public float InnerRadius;
            public float Opacity = 1f;

            // Pen (フラット化)
            public string PenColor;
            public float PenWidth;

            // Brush (フラット化)
            public string BrushType;
            public string BrushColor;
            public string BrushCenter, BrushEdge;
            public string BrushStart, BrushEnd;
            public float BrushAngle;
        }

        #endregion
    }
}
