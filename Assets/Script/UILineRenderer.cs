using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using RandomExtensions;

public enum SideObject_Type
{
    Chaos,Normal
}

[ExecuteAlways]
public class UILineRenderer : Graphic
{
    [System.Serializable]
    public class LineData
    {
        public Vector2 startPoint;
        public Vector2 endPoint;
    }

    [System.Serializable]
    public class CircleData
    {
        public Vector2 center;
        public float radius;
    }

    public List<LineData> lines = new List<LineData>();
    public List<CircleData> circles = new List<CircleData>();

    public float thickness = 5f;
    public Color lineColor = Color.white;
    public int circleSegments = 36;

    // 振動のパラメータ
    public float shakeAmplitude = 10f;
    public float shakeFrequency = 1f;

    private float timeElapsed = 0f;

    //生成時にランダムにずれる。(サイドオブジェクトのノイズ性を高めるため　ドリームリアリティってやつ)
    [SerializeField] private Vector2 bornPosRange;
    [SerializeField] private SideObject_Type sideObject_Type;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        // 時間に基づく揺れの計算
        float shakeOffset = Mathf.Sin(timeElapsed * shakeFrequency) * shakeAmplitude;

        // 線の描画
        foreach (var line in lines)
        {
            DrawLine(vh, ApplyShakeToLine(line, shakeOffset));
        }

        // 円の描画
        foreach (var circle in circles)
        {
            DrawCircle(vh, ApplyShakeToCircle(circle, shakeOffset));
        }
    }

    protected override void Awake()
    {
        base.Awake();

        if(sideObject_Type == SideObject_Type.Chaos)
        {
            foreach (var line in lines)
            {
                line.startPoint += new Vector2(RandomEx.Shared.NextFloat(-bornPosRange.x, bornPosRange.x), RandomEx.Shared.NextFloat(-bornPosRange.y, bornPosRange.y));
                line.endPoint += new Vector2(RandomEx.Shared.NextFloat(-bornPosRange.x, bornPosRange.x), RandomEx.Shared.NextFloat(-bornPosRange.y, bornPosRange.y));
            }
            foreach (var circle in circles)
            {
                circle.center += new Vector2(RandomEx.Shared.NextFloat(-bornPosRange.x, bornPosRange.x), RandomEx.Shared.NextFloat(-bornPosRange.y, bornPosRange.y));
            }
        }
        else if(sideObject_Type == SideObject_Type.Normal)
        {
            // 一度だけ乱数を生成
            Vector2 randomOffset = new Vector2(
                RandomEx.Shared.NextFloat(-bornPosRange.x, bornPosRange.x),
                RandomEx.Shared.NextFloat(-bornPosRange.y, bornPosRange.y)
            );
            Debug.Log("今回のずれ" + randomOffset);

            // すべてのラインに同じ乱数を適用
            foreach (var line in lines)
            {
                line.startPoint += randomOffset;
                line.endPoint += randomOffset;
            }

            // すべてのサークルに同じ乱数を適用
            foreach (var circle in circles)
            {
                circle.center += randomOffset;
            }

        }


    }

    LineData ApplyShakeToLine(LineData line, float shakeOffset)
    {
        LineData shakenLine = new LineData();
        shakenLine.startPoint = line.startPoint + new Vector2(shakeOffset, shakeOffset);
        shakenLine.endPoint = line.endPoint + new Vector2(-shakeOffset, shakeOffset);
        return shakenLine;
    }

    CircleData ApplyShakeToCircle(CircleData circle, float shakeOffset)
    {
        CircleData shakenCircle = new CircleData();
        shakenCircle.center = circle.center + new Vector2(shakeOffset, shakeOffset);
        shakenCircle.radius = Mathf.Max(circle.radius + shakeOffset, thickness);
        return shakenCircle;
    }

    void DrawLine(VertexHelper vh, LineData line)
    {
        Vector2 direction = (line.endPoint - line.startPoint).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);

        // 頂点の計算（外側と内側）
        Vector2[] outerVertices = new Vector2[2];
        Vector2[] innerVertices = new Vector2[2];

        outerVertices[0] = line.startPoint + normal;
        outerVertices[1] = line.endPoint + normal;
        innerVertices[0] = line.startPoint - normal;
        innerVertices[1] = line.endPoint - normal;

        // 頂点の追加
        AddQuad(vh, outerVertices[0], innerVertices[0], innerVertices[1], outerVertices[1], lineColor);
    }

    void DrawCircle(VertexHelper vh, CircleData circle)
    {
        float angleStep = 360f / circleSegments;
        Vector2 prevOuter = Vector2.zero;
        Vector2 prevInner = Vector2.zero;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            Vector2 outer = dir * (circle.radius + thickness * 0.5f) + circle.center;
            Vector2 inner = dir * (circle.radius - thickness * 0.5f) + circle.center;

            if (i > 0)
            {
                AddQuad(vh, prevOuter, prevInner, inner, outer, lineColor);
            }

            prevOuter = outer;
            prevInner = inner;
        }
    }

    void AddQuad(VertexHelper vh, Vector2 v1, Vector2 v2, Vector2 v3, Vector2 v4, Color color)
    {
        int idx = vh.currentVertCount;

        Vector3[] positions = { v1, v2, v3, v4 };
        for (int i = 0; i < 4; i++)
        {
            vh.AddVert(positions[i], color, Vector2.zero);
        }

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;
        SetVerticesDirty();
    }
}