using System.Collections.Generic;
using Effects.Core;
using UnityEngine;

namespace Effects.Rendering
{
    /// <summary>
    /// エフェクト定義からTexture2Dを生成するレンダラー
    /// </summary>
    public class EffectRenderer
    {
        private readonly int _canvasSize;
        private readonly Color[] _clearColors;

        public EffectRenderer(int canvasSize)
        {
            _canvasSize = canvasSize;

            // クリア用の色配列を事前作成
            _clearColors = new Color[canvasSize * canvasSize];
            for (int i = 0; i < _clearColors.Length; i++)
            {
                _clearColors[i] = Color.clear;
            }
        }

        /// <summary>
        /// 新しいTexture2Dを作成
        /// </summary>
        public Texture2D CreateTexture()
        {
            var texture = new Texture2D(_canvasSize, _canvasSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            ClearTexture(texture);
            return texture;
        }

        /// <summary>
        /// Texture2Dを透明でクリア
        /// </summary>
        public void ClearTexture(Texture2D texture)
        {
            texture.SetPixels(_clearColors);
        }

        /// <summary>
        /// フレームを描画
        /// </summary>
        public void RenderFrame(Texture2D texture, FrameDefinition frame, int frameIndex)
        {
            ClearTexture(texture);

            if (frame.Shapes != null)
            {
                foreach (var shape in frame.Shapes)
                {
                    RenderShape(texture, shape, frameIndex);
                }
            }

            texture.Apply();
        }

        /// <summary>
        /// 個々の図形を描画
        /// </summary>
        private void RenderShape(Texture2D texture, ShapeDefinition shape, int frameIndex)
        {
            var shapeType = shape.GetShapeType();

            // ブレンドモード設定
            BlendMode blend = BlendMode.Normal;
            if (!string.IsNullOrEmpty(shape.Blend) &&
                shape.Blend.ToLowerInvariant() == "additive")
            {
                blend = BlendMode.Additive;
            }
            ShapeDrawer.CurrentBlendMode = blend;

            try
            {
                // エミッタは独自のスタイル処理
                if (shapeType == ShapeType.Emitter)
                {
                    RenderEmitter(texture, shape, frameIndex);
                    return;
                }

                var penColor = EffectColorUtility.GetPenColor(shape.Pen);

                // ブラシの準備（flat/radial/linear全てBrushContext経由）
                BrushContext? brushCtx = null;
                if (shape.Brush != null)
                {
                    float boundsSize = GetShapeBoundsSize(shape, shapeType);
                    brushCtx = EffectColorUtility.CreateBrushContext(
                        shape.Brush, shape.X, shape.Y, boundsSize);
                }

                // penとbrushが全てnullなら無効
                bool hasVisual = penColor.HasValue || brushCtx.HasValue;
                if (!hasVisual && shapeType != ShapeType.TaperedLine)
                {
                    Debug.LogWarning($"[EffectRenderer] Shape has no pen or brush, skipping");
                    return;
                }

                float penWidth = shape.Pen?.Width ?? EffectConstants.PenWidthDefault;

                switch (shapeType)
                {
                    case ShapeType.Point:
                        RenderPoint(texture, shape, penColor);
                        break;
                    case ShapeType.Line:
                        RenderLine(texture, shape, penColor, penWidth);
                        break;
                    case ShapeType.Circle:
                        RenderCircle(texture, shape, penColor, brushCtx, penWidth);
                        break;
                    case ShapeType.Ellipse:
                        RenderEllipse(texture, shape, penColor, brushCtx, penWidth);
                        break;
                    case ShapeType.Arc:
                        RenderArc(texture, shape, penColor, penWidth);
                        break;
                    case ShapeType.Rect:
                        RenderRect(texture, shape, penColor, brushCtx, penWidth);
                        break;
                    case ShapeType.Polygon:
                        RenderPolygon(texture, shape, penColor, brushCtx, penWidth);
                        break;
                    case ShapeType.Bezier:
                        RenderBezier(texture, shape, penColor, penWidth);
                        break;
                    case ShapeType.TaperedLine:
                        RenderTaperedLine(texture, shape, penColor);
                        break;
                    case ShapeType.Ring:
                        RenderRing(texture, shape, penColor, brushCtx, penWidth);
                        break;
                    default:
                        Debug.LogWarning($"[EffectRenderer] Unknown shape type: {shape.Type}");
                        break;
                }
            }
            finally
            {
                // ブレンドモードをリセット
                ShapeDrawer.CurrentBlendMode = BlendMode.Normal;
            }
        }

        /// <summary>
        /// シェイプのバウンディングサイズを取得（グラデーション範囲用）
        /// </summary>
        private float GetShapeBoundsSize(ShapeDefinition shape, ShapeType shapeType)
        {
            switch (shapeType)
            {
                case ShapeType.Circle:
                    return shape.Radius;
                case ShapeType.Ellipse:
                    return Mathf.Max(shape.Rx, shape.Ry);
                case ShapeType.Rect:
                    return Mathf.Max(shape.Width, shape.Height) / 2f;
                case ShapeType.Ring:
                    return shape.Radius; // outer_radius として Radius を使用
                case ShapeType.Polygon:
                    if (shape.Points == null || shape.Points.Count == 0) return 10f;
                    float maxDist = 0f;
                    foreach (var p in shape.Points)
                    {
                        float dx = p.X - shape.X;
                        float dy = p.Y - shape.Y;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist > maxDist) maxDist = dist;
                    }
                    return Mathf.Max(1f, maxDist);
                default:
                    return 10f;
            }
        }

        #region Shape Renderers

        private void RenderPoint(Texture2D texture, ShapeDefinition shape, Color? penColor)
        {
            if (!penColor.HasValue) return;

            int size = ShapeDrawer.RoundSize(shape.Size);
            if (size <= 0) return;

            ShapeDrawer.DrawPoint(texture, shape.X, shape.Y, shape.Size, penColor.Value, _canvasSize);
        }

        private void RenderLine(Texture2D texture, ShapeDefinition shape, Color? penColor, float penWidth)
        {
            if (!penColor.HasValue) return;

            int x1 = ShapeDrawer.RoundCoord(shape.X1);
            int y1 = ShapeDrawer.RoundCoord(shape.Y1);
            int x2 = ShapeDrawer.RoundCoord(shape.X2);
            int y2 = ShapeDrawer.RoundCoord(shape.Y2);
            if (x1 == x2 && y1 == y2) return;

            ShapeDrawer.DrawLine(texture, shape.X1, shape.Y1, shape.X2, shape.Y2, penColor.Value, penWidth, _canvasSize);
        }

        private void RenderCircle(Texture2D texture, ShapeDefinition shape,
            Color? penColor, BrushContext? brushCtx, float penWidth)
        {
            int radius = ShapeDrawer.RoundSize(shape.Radius);
            if (radius <= 0) return;

            // 塗り → 輪郭の順
            if (brushCtx.HasValue)
            {
                ShapeDrawer.FillCircleWithBrush(texture, shape.X, shape.Y, shape.Radius, brushCtx.Value, _canvasSize);
            }
            if (penColor.HasValue)
            {
                ShapeDrawer.DrawCircle(texture, shape.X, shape.Y, shape.Radius, penColor.Value, penWidth, _canvasSize);
            }
        }

        private void RenderEllipse(Texture2D texture, ShapeDefinition shape,
            Color? penColor, BrushContext? brushCtx, float penWidth)
        {
            int rx = ShapeDrawer.RoundSize(shape.Rx);
            int ry = ShapeDrawer.RoundSize(shape.Ry);
            if (rx <= 0 || ry <= 0) return;

            if (brushCtx.HasValue)
            {
                ShapeDrawer.FillEllipseWithBrush(texture, shape.X, shape.Y, shape.Rx, shape.Ry, shape.Rotation, brushCtx.Value, _canvasSize);
            }
            if (penColor.HasValue)
            {
                ShapeDrawer.DrawEllipse(texture, shape.X, shape.Y, shape.Rx, shape.Ry, shape.Rotation, penColor.Value, penWidth, _canvasSize);
            }
        }

        private void RenderArc(Texture2D texture, ShapeDefinition shape, Color? penColor, float penWidth)
        {
            if (!penColor.HasValue) return;

            int radius = ShapeDrawer.RoundSize(shape.Radius);
            if (radius <= 0) return;

            ShapeDrawer.DrawArc(texture, shape.X, shape.Y, shape.Radius, shape.StartAngle, shape.EndAngle, penColor.Value, penWidth, _canvasSize);
        }

        private void RenderRect(Texture2D texture, ShapeDefinition shape,
            Color? penColor, BrushContext? brushCtx, float penWidth)
        {
            int w = ShapeDrawer.RoundSize(shape.Width);
            int h = ShapeDrawer.RoundSize(shape.Height);
            if (w <= 0 || h <= 0) return;

            if (brushCtx.HasValue)
            {
                ShapeDrawer.FillRectWithBrush(texture, shape.X, shape.Y, shape.Width, shape.Height, shape.Rotation, brushCtx.Value, _canvasSize);
            }
            if (penColor.HasValue)
            {
                ShapeDrawer.DrawRect(texture, shape.X, shape.Y, shape.Width, shape.Height, shape.Rotation, penColor.Value, penWidth, _canvasSize);
            }
        }

        private void RenderPolygon(Texture2D texture, ShapeDefinition shape,
            Color? penColor, BrushContext? brushCtx, float penWidth)
        {
            if (shape.Points == null || shape.Points.Count < EffectConstants.PolygonMinPoints)
            {
                Debug.LogWarning($"[EffectRenderer] Polygon requires at least {EffectConstants.PolygonMinPoints} points");
                return;
            }

            Vector2[] points = new Vector2[shape.Points.Count];
            for (int i = 0; i < shape.Points.Count; i++)
            {
                points[i] = shape.Points[i].ToVector2();
            }

            if (brushCtx.HasValue)
            {
                ShapeDrawer.FillPolygonWithBrush(texture, points, brushCtx.Value, _canvasSize);
            }
            if (penColor.HasValue)
            {
                ShapeDrawer.DrawPolygon(texture, points, penColor.Value, penWidth, _canvasSize);
            }
        }

        private void RenderBezier(Texture2D texture, ShapeDefinition shape, Color? penColor, float penWidth)
        {
            if (!penColor.HasValue) return;

            if (shape.Points == null || shape.Points.Count < EffectConstants.BezierMinPoints)
            {
                Debug.LogWarning($"[EffectRenderer] Bezier requires at least {EffectConstants.BezierMinPoints} points");
                return;
            }

            Vector2[] points = new Vector2[shape.Points.Count];
            for (int i = 0; i < shape.Points.Count; i++)
            {
                points[i] = shape.Points[i].ToVector2();
            }

            ShapeDrawer.DrawBezier(texture, points, penColor.Value, penWidth, _canvasSize);
        }

        private void RenderTaperedLine(Texture2D texture, ShapeDefinition shape, Color? penColor)
        {
            if (!penColor.HasValue) return;

            int x1 = ShapeDrawer.RoundCoord(shape.X1);
            int y1 = ShapeDrawer.RoundCoord(shape.Y1);
            int x2 = ShapeDrawer.RoundCoord(shape.X2);
            int y2 = ShapeDrawer.RoundCoord(shape.Y2);
            if (x1 == x2 && y1 == y2) return;

            float widthStart = shape.WidthStart > 0 ? shape.WidthStart : 1f;
            float widthEnd = shape.WidthEnd > 0 ? shape.WidthEnd : 0.5f;

            ShapeDrawer.DrawTaperedLine(texture, shape.X1, shape.Y1, shape.X2, shape.Y2,
                widthStart, widthEnd, penColor.Value, _canvasSize);
        }

        private void RenderRing(Texture2D texture, ShapeDefinition shape,
            Color? penColor, BrushContext? brushCtx, float penWidth)
        {
            float outerRadius = shape.Radius;
            float innerRadius = shape.InnerRadius;

            int iOuter = ShapeDrawer.RoundSize(outerRadius);
            int iInner = ShapeDrawer.RoundSize(innerRadius);
            if (iOuter <= 0 || iInner >= iOuter) return;

            // 塗り → 輪郭の順
            if (brushCtx.HasValue)
            {
                ShapeDrawer.FillRingWithBrush(texture, shape.X, shape.Y, innerRadius, outerRadius, brushCtx.Value, _canvasSize);
            }
            if (penColor.HasValue)
            {
                ShapeDrawer.DrawRing(texture, shape.X, shape.Y, innerRadius, outerRadius, penColor.Value, penWidth, _canvasSize);
            }
        }

        private void RenderEmitter(Texture2D texture, ShapeDefinition shape, int frameIndex)
        {
            ShapeDrawer.DrawEmitter(texture, shape, frameIndex, _canvasSize);
        }

        #endregion
    }
}
