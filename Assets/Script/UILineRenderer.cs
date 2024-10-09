using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[ExecuteAlways]
public class UILineRenderer : Graphic
{
    [System.Serializable]
    public class LineData
    {
        public Vector2 startPoint;
        public Vector2 endPoint;
        // 個別の太さと色を削除
        // public float thickness = 5f;
        // public Color color = Color.white;
    }

    public List<LineData> lines = new List<LineData>();

    // 全体で共通の太さと色を追加
    public float thickness = 5f;
    public Color lineColor = Color.white;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        foreach (var line in lines)
        {
            DrawLine(vh, line);
        }
    }

    void DrawLine(VertexHelper vh, LineData line)
    {
        Vector2 startPoint = line.startPoint;
        Vector2 endPoint = line.endPoint;
        // 個別の太さと色を使用しない
        // float thickness = line.thickness;
        // Color color = line.color;

        Vector2 direction = (endPoint - startPoint).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);

        Vector2[] vertices = new Vector2[4];
        vertices[0] = startPoint + normal;
        vertices[1] = startPoint - normal;
        vertices[2] = endPoint - normal;
        vertices[3] = endPoint + normal;

        UIVertex[] uiVertices = new UIVertex[4];
        for (int i = 0; i < 4; i++)
        {
            var vert = UIVertex.simpleVert;
            vert.color = lineColor; // 共通の色を使用
            vert.position = vertices[i];
            uiVertices[i] = vert;
        }

        int startIndex = vh.currentVertCount;
        vh.AddUIVertexQuad(uiVertices);
    }
}