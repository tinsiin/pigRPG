using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(UILineRenderer))]
    public class UILineRendererEditor : UnityEditor.Editor
    {
        void OnSceneGUI()
        {
            UILineRenderer lineRenderer = (UILineRenderer)target;

            if (lineRenderer.lines == null || lineRenderer.lines.Count == 0)
                return;

            var rectTransform = lineRenderer.rectTransform;
            var sizeDelta = rectTransform.sizeDelta;
            var mat = rectTransform.localToWorldMatrix;
            var inv = rectTransform.worldToLocalMatrix;

            for (int i = 0; i < lineRenderer.lines.Count; i++)
            {
                UILineRenderer.LineData line = lineRenderer.lines[i];

                EditorGUI.BeginChangeCheck();

                // 始点のハンドル操作
                Vector3 startWorldPos = mat.MultiplyPoint3x4(line.startPoint);
                var fmh_27_78_638638635078363493 = Quaternion.identity; Vector3 newStartWorldPos = Handles.FreeMoveHandle(startWorldPos, HandleUtility.GetHandleSize(startWorldPos) * 0.1f, Vector3.zero, Handles.DotHandleCap);

                // 終点のハンドル操作
                Vector3 endWorldPos = mat.MultiplyPoint3x4(line.endPoint);
                var fmh_31_74_638638635078393833 = Quaternion.identity; Vector3 newEndWorldPos = Handles.FreeMoveHandle(endWorldPos, HandleUtility.GetHandleSize(endWorldPos) * 0.1f, Vector3.zero, Handles.DotHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(lineRenderer, "Move Line Point");

                    // ワールド座標からローカル座標に変換
                    Vector3 newStartLocalPos = inv.MultiplyPoint3x4(newStartWorldPos);
                    Vector3 newEndLocalPos = inv.MultiplyPoint3x4(newEndWorldPos);

                    line.startPoint = newStartLocalPos;
                    line.endPoint = newEndLocalPos;

                    EditorUtility.SetDirty(lineRenderer);
                    lineRenderer.SetAllDirty();
                }

                // ラベルを表示
                Handles.Label(startWorldPos, $"Line {i} Start");
                Handles.Label(endWorldPos, $"Line {i} End");
            }
        }
    }
}