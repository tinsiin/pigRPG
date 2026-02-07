using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Effects.Core
{
    /// <summary>
    /// エフェクト定義（JSONルート）
    /// </summary>
    [Serializable]
    public class EffectDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("canvas")]
        public int Canvas { get; set; } = EffectConstants.CanvasDefault;

        [JsonProperty("fps")]
        public int Fps { get; set; } = EffectConstants.FpsDefault;

        [JsonProperty("se")]
        public string Se { get; set; }

        // ===== 配置メタデータ =====

        /// <summary>
        /// 配置モード: "icon"（アイコン相対）/ "field"（フィールド全体）
        /// </summary>
        [JsonProperty("target")]
        public string Target { get; set; } = "icon";

        /// <summary>
        /// アイコン参照矩形（target="icon" 時のみ）。
        /// キャンバス内のどの領域が実際のアイコンに対応するかを定義する。
        /// null の場合はキャンバス全体がアイコン領域として扱われる。
        /// </summary>
        [JsonProperty("icon_rect")]
        public IconRectDefinition IconRect { get; set; }

        /// <summary>
        /// フィールド参照矩形（target="field" 時のみ）。
        /// キャンバス内のどの領域がビューポートに対応するかを定義する。
        /// null の場合はキャンバス全体がビューポートにフィットする。
        /// </summary>
        [JsonProperty("field_rect")]
        public IconRectDefinition FieldRect { get; set; }

        /// <summary>
        /// フィールドエフェクトの描画レイヤー（target="field" 時のみ）: "back" / "middle" / "front"
        /// </summary>
        [JsonProperty("field_layer")]
        public string FieldLayer { get; set; } = "middle";

        [JsonProperty("frames")]
        public List<FrameDefinition> Frames { get; set; } = new List<FrameDefinition>();

        /// <summary>
        /// 定義を検証してエラーメッセージを返す（正常ならnull）
        /// </summary>
        public string Validate()
        {
            if (string.IsNullOrEmpty(Name))
                return "Effect name is required";

            if (!EffectConstants.ValidateCanvas(Canvas, out var canvasError))
                return canvasError;

            if (Frames == null || Frames.Count == 0)
                return "Effect must have at least one frame";

            return null;
        }

        /// <summary>
        /// FPS/Duration等の値を正規化する
        /// </summary>
        public void Normalize()
        {
            Fps = EffectConstants.ClampFps(Fps);
            float defaultDuration = 1f / Fps;

            foreach (var frame in Frames)
            {
                frame.Duration = EffectConstants.ClampDuration(frame.Duration, defaultDuration);
            }
        }
    }

    /// <summary>
    /// フレーム定義
    /// </summary>
    [Serializable]
    public class FrameDefinition
    {
        [JsonProperty("duration")]
        public float Duration { get; set; } = -1; // -1 = use 1/fps

        [JsonProperty("shapes")]
        public List<ShapeDefinition> Shapes { get; set; } = new List<ShapeDefinition>();
    }

    /// <summary>
    /// 図形定義
    /// </summary>
    [Serializable]
    public class ShapeDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        // Point
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("size")]
        public float Size { get; set; }

        // Line
        [JsonProperty("x1")]
        public float X1 { get; set; }

        [JsonProperty("y1")]
        public float Y1 { get; set; }

        [JsonProperty("x2")]
        public float X2 { get; set; }

        [JsonProperty("y2")]
        public float Y2 { get; set; }

        // Circle, Arc
        [JsonProperty("radius")]
        public float Radius { get; set; }

        // Ellipse
        [JsonProperty("rx")]
        public float Rx { get; set; }

        [JsonProperty("ry")]
        public float Ry { get; set; }

        // Arc
        [JsonProperty("startAngle")]
        public float StartAngle { get; set; }

        [JsonProperty("endAngle")]
        public float EndAngle { get; set; }

        // Rect
        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }

        // Rotation (Ellipse, Rect)
        [JsonProperty("rotation")]
        public float Rotation { get; set; }

        // Polygon, Bezier
        [JsonProperty("points")]
        public List<PointDefinition> Points { get; set; }

        // Style
        [JsonProperty("pen")]
        public PenDefinition Pen { get; set; }

        [JsonProperty("brush")]
        public BrushDefinition Brush { get; set; }

        // === Blend mode ===
        [JsonProperty("blend")]
        public string Blend { get; set; }

        // === TaperedLine ===
        [JsonProperty("width_start")]
        public float WidthStart { get; set; }

        [JsonProperty("width_end")]
        public float WidthEnd { get; set; }

        // === Ring ===
        [JsonProperty("inner_radius")]
        public float InnerRadius { get; set; }

        // === Emitter ===
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("angle_range")]
        public List<float> AngleRange { get; set; }

        [JsonProperty("speed_range")]
        public List<float> SpeedRange { get; set; }

        [JsonProperty("gravity")]
        public float Gravity { get; set; }

        [JsonProperty("drag")]
        public float Drag { get; set; } = 1f;

        [JsonProperty("lifetime")]
        public int Lifetime { get; set; } = 30;

        [JsonProperty("size_range")]
        public List<float> SizeRange { get; set; }

        [JsonProperty("color_start")]
        public string ColorStart { get; set; }

        [JsonProperty("color_end")]
        public string ColorEnd { get; set; }

        [JsonProperty("seed")]
        public int Seed { get; set; }

        /// <summary>
        /// KfxCompilerが設定するエミッタのローカルフレーム番号（-1=グローバルframeIndexを使用）
        /// </summary>
        [JsonIgnore]
        public int EmitterLocalFrame { get; set; } = -1;

        /// <summary>
        /// 図形タイプを列挙型で取得
        /// </summary>
        public ShapeType GetShapeType()
        {
            return Type?.ToLowerInvariant() switch
            {
                "point" => ShapeType.Point,
                "line" => ShapeType.Line,
                "circle" => ShapeType.Circle,
                "ellipse" => ShapeType.Ellipse,
                "arc" => ShapeType.Arc,
                "rect" => ShapeType.Rect,
                "polygon" => ShapeType.Polygon,
                "bezier" => ShapeType.Bezier,
                "tapered_line" => ShapeType.TaperedLine,
                "ring" => ShapeType.Ring,
                "emitter" => ShapeType.Emitter,
                _ => ShapeType.Unknown
            };
        }
    }

    /// <summary>
    /// 座標点定義（polygon, bezier用）
    /// </summary>
    [Serializable]
    public class PointDefinition
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        public Vector2 ToVector2() => new Vector2(X, Y);
    }

    /// <summary>
    /// ペン定義（輪郭線）
    /// </summary>
    [Serializable]
    public class PenDefinition
    {
        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; } = EffectConstants.PenWidthDefault;
    }

    /// <summary>
    /// ブラシ定義（塗りつぶし）
    /// "color" のみ指定 → 単色ベタ塗り
    /// "type":"radial" + "center"/"edge" → 放射グラデーション
    /// "type":"linear" + "start"/"end"/"angle" → 線形グラデーション
    /// </summary>
    [Serializable]
    public class BrushDefinition
    {
        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        // Radial gradient
        [JsonProperty("center")]
        public string Center { get; set; }

        [JsonProperty("edge")]
        public string Edge { get; set; }

        // Linear gradient
        [JsonProperty("start")]
        public string Start { get; set; }

        [JsonProperty("end")]
        public string End { get; set; }

        [JsonProperty("angle")]
        public float Angle { get; set; }
    }

    /// <summary>
    /// アイコン参照矩形の定義（キャンバス座標系）
    /// </summary>
    [Serializable]
    public class IconRectDefinition
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }
    }

    /// <summary>
    /// 図形タイプ
    /// </summary>
    public enum ShapeType
    {
        Unknown,
        Point,
        Line,
        Circle,
        Ellipse,
        Arc,
        Rect,
        Polygon,
        Bezier,
        TaperedLine,
        Ring,
        Emitter
    }

    /// <summary>
    /// ブレンドモード
    /// </summary>
    public enum BlendMode
    {
        Normal,
        Additive
    }

    /// <summary>
    /// ブラシコンテキスト（ピクセルごとの色を計算する）
    /// </summary>
    public struct BrushContext
    {
        public enum BrushType { Flat, Radial, Linear }

        public BrushType Type;
        public Color FlatColor;
        public Color ColorStart;
        public Color ColorEnd;
        public float CenterX, CenterY;
        public float BoundsSize;
        public float Angle;

        public static BrushContext Flat(Color color)
        {
            return new BrushContext { Type = BrushType.Flat, FlatColor = color };
        }

        public static BrushContext Radial(Color center, Color edge, float cx, float cy, float radius)
        {
            return new BrushContext
            {
                Type = BrushType.Radial,
                ColorStart = center,
                ColorEnd = edge,
                CenterX = cx,
                CenterY = cy,
                BoundsSize = Mathf.Max(1f, radius)
            };
        }

        public static BrushContext Linear(Color start, Color end, float cx, float cy, float boundsSize, float angle)
        {
            return new BrushContext
            {
                Type = BrushType.Linear,
                ColorStart = start,
                ColorEnd = end,
                CenterX = cx,
                CenterY = cy,
                BoundsSize = Mathf.Max(1f, boundsSize),
                Angle = angle
            };
        }

        public Color GetColorAt(float px, float py)
        {
            switch (Type)
            {
                case BrushType.Radial:
                    float dx = px - CenterX;
                    float dy = py - CenterY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float rt = Mathf.Clamp01(dist / BoundsSize);
                    return Color.Lerp(ColorStart, ColorEnd, rt);

                case BrushType.Linear:
                    float rad = Angle * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);
                    float proj = (px - CenterX) * cos + (py - CenterY) * sin;
                    float lt = Mathf.Clamp01((proj / BoundsSize + 1f) * 0.5f);
                    return Color.Lerp(ColorStart, ColorEnd, lt);

                default:
                    return FlatColor;
            }
        }
    }
}
