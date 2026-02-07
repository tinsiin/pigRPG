using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Effects.Core
{
    /// <summary>
    /// KFXフォーマットのルート定義
    /// </summary>
    [Serializable]
    public class KfxDefinition
    {
        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("canvas")]
        public int Canvas { get; set; } = EffectConstants.CanvasDefault;

        [JsonProperty("fps")]
        public int Fps { get; set; } = 30;

        [JsonProperty("duration")]
        public float Duration { get; set; } = 1.0f;

        [JsonProperty("se")]
        public string Se { get; set; }

        // ===== 配置メタデータ =====

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("icon_rect")]
        public IconRectDefinition IconRect { get; set; }

        [JsonProperty("field_layer")]
        public string FieldLayer { get; set; }

        [JsonProperty("layers")]
        public List<KfxLayer> Layers { get; set; } = new List<KfxLayer>();

        /// <summary>
        /// バリデーション。エラーメッセージを返す (正常時は null)
        /// </summary>
        public string Validate()
        {
            if (string.IsNullOrEmpty(Name))
                return "Name is required";
            if (Canvas < 1 || Canvas > EffectConstants.CanvasMax)
                return $"Canvas must be 1-{EffectConstants.CanvasMax}";
            if (Fps < 1 || Fps > EffectConstants.FpsMax)
                return $"FPS must be 1-{EffectConstants.FpsMax}";
            if (Duration <= 0)
                return "Duration must be > 0";
            if (Layers == null || Layers.Count == 0)
                return "At least one layer is required";

            foreach (var layer in Layers)
            {
                var err = layer.Validate();
                if (err != null) return $"Layer '{layer.Id}': {err}";
            }

            return null;
        }
    }

    /// <summary>
    /// KFXレイヤー（1つのアニメーションされるシェイプ）
    /// </summary>
    [Serializable]
    public class KfxLayer
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("blend")]
        public string Blend { get; set; }

        /// <summary>
        /// 表示範囲 [開始秒, 終了秒]。null = 全期間表示
        /// </summary>
        [JsonProperty("visible")]
        public List<float> Visible { get; set; }

        [JsonProperty("keyframes")]
        public List<KfxKeyframe> Keyframes { get; set; }

        // ===== エミッタ専用フィールド（非キーフレーム）=====
        [JsonProperty("x")]
        public float? X { get; set; }

        [JsonProperty("y")]
        public float? Y { get; set; }

        [JsonProperty("count")]
        public int? Count { get; set; }

        [JsonProperty("angle_range")]
        public List<float> AngleRange { get; set; }

        [JsonProperty("speed_range")]
        public List<float> SpeedRange { get; set; }

        [JsonProperty("gravity")]
        public float? Gravity { get; set; }

        [JsonProperty("drag")]
        public float? Drag { get; set; }

        /// <summary>
        /// エミッタの寿命（秒）。コンパイル時にフレーム数に変換される
        /// </summary>
        [JsonProperty("lifetime")]
        public float? Lifetime { get; set; }

        [JsonProperty("size_range")]
        public List<float> SizeRange { get; set; }

        [JsonProperty("color_start")]
        public string ColorStart { get; set; }

        [JsonProperty("color_end")]
        public string ColorEnd { get; set; }

        [JsonProperty("seed")]
        public int? Seed { get; set; }

        public bool IsEmitter => Type?.ToLowerInvariant() == "emitter";

        public string Validate()
        {
            if (string.IsNullOrEmpty(Type))
                return "Type is required";

            if (IsEmitter)
            {
                if (!Count.HasValue || Count.Value <= 0)
                    return "Emitter requires count > 0";
            }
            else
            {
                if (Keyframes == null || Keyframes.Count == 0)
                    return "Non-emitter layer requires at least one keyframe";

                for (int i = 1; i < Keyframes.Count; i++)
                {
                    if (Keyframes[i].Time <= Keyframes[i - 1].Time)
                        return "Keyframe times must be in ascending order";
                }
            }

            return null;
        }
    }

    /// <summary>
    /// KFXキーフレーム（特定時間でのプロパティスナップショット）
    /// nullのプロパティは前のキーフレームから継承
    /// </summary>
    [Serializable]
    public class KfxKeyframe
    {
        [JsonProperty("time")]
        public float Time { get; set; }

        /// <summary>
        /// このキーフレームから次のキーフレームまでのイージング
        /// </summary>
        [JsonProperty("ease")]
        public string Ease { get; set; }

        // ===== 共通座標 =====
        [JsonProperty("x")]
        public float? X { get; set; }

        [JsonProperty("y")]
        public float? Y { get; set; }

        // ===== Circle / Arc / Ring =====
        [JsonProperty("radius")]
        public float? Radius { get; set; }

        // ===== Ellipse =====
        [JsonProperty("rx")]
        public float? Rx { get; set; }

        [JsonProperty("ry")]
        public float? Ry { get; set; }

        // ===== Arc =====
        [JsonProperty("startAngle")]
        public float? StartAngle { get; set; }

        [JsonProperty("endAngle")]
        public float? EndAngle { get; set; }

        // ===== Rect =====
        [JsonProperty("width")]
        public float? Width { get; set; }

        [JsonProperty("height")]
        public float? Height { get; set; }

        // ===== 回転 (Ellipse, Rect) =====
        [JsonProperty("rotation")]
        public float? Rotation { get; set; }

        // ===== Point =====
        [JsonProperty("size")]
        public float? Size { get; set; }

        // ===== Line / TaperedLine =====
        [JsonProperty("x1")]
        public float? X1 { get; set; }

        [JsonProperty("y1")]
        public float? Y1 { get; set; }

        [JsonProperty("x2")]
        public float? X2 { get; set; }

        [JsonProperty("y2")]
        public float? Y2 { get; set; }

        // ===== TaperedLine =====
        [JsonProperty("width_start")]
        public float? WidthStart { get; set; }

        [JsonProperty("width_end")]
        public float? WidthEnd { get; set; }

        // ===== Ring =====
        [JsonProperty("inner_radius")]
        public float? InnerRadius { get; set; }

        // ===== スタイル =====
        [JsonProperty("pen")]
        public KfxPenKeyframe Pen { get; set; }

        [JsonProperty("brush")]
        public KfxBrushKeyframe Brush { get; set; }

        /// <summary>
        /// 全色のアルファを一括変調 (0-1)。デフォルト1.0
        /// </summary>
        [JsonProperty("opacity")]
        public float? Opacity { get; set; }
    }

    /// <summary>
    /// ペン定義（キーフレーム内）
    /// </summary>
    [Serializable]
    public class KfxPenKeyframe
    {
        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("width")]
        public float? Width { get; set; }
    }

    /// <summary>
    /// ブラシ定義（キーフレーム内）
    /// </summary>
    [Serializable]
    public class KfxBrushKeyframe
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        // Flat
        [JsonProperty("color")]
        public string Color { get; set; }

        // Radial
        [JsonProperty("center")]
        public string Center { get; set; }

        [JsonProperty("edge")]
        public string Edge { get; set; }

        // Linear
        [JsonProperty("start")]
        public string Start { get; set; }

        [JsonProperty("end")]
        public string End { get; set; }

        [JsonProperty("angle")]
        public float? Angle { get; set; }
    }
}
