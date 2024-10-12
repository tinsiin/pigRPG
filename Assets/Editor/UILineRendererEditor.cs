using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(UILineRenderer))]
    public class UILineRendererEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // デフォルトのインスペクターを描画
            DrawDefaultInspector();

            UILineRenderer lineRenderer = (UILineRenderer)target;

            // 横反転ボタン
            if (GUILayout.Button("横反転"))
            {
                FlipHorizontally(lineRenderer);
            }
        }

        void OnSceneGUI()
        {
            UILineRenderer lineRenderer = (UILineRenderer)target;

            if ((lineRenderer.lines == null || lineRenderer.lines.Count == 0) &&
                (lineRenderer.circles == null || lineRenderer.circles.Count == 0))
                return;

            var rectTransform = lineRenderer.rectTransform;
            var sizeDelta = rectTransform.sizeDelta;
            var mat = rectTransform.localToWorldMatrix;
            var inv = rectTransform.worldToLocalMatrix;

            // すべてのラインと円の中心点を計算
            Vector3 totalStart = Vector3.zero;
            Vector3 totalEnd = Vector3.zero;
            int totalCount = 0;

            for (int i = 0; i < lineRenderer.lines.Count; i++)
            {
                totalStart += (Vector3)lineRenderer.lines[i].startPoint;  // Vector2 から Vector3 に変換
                totalEnd += (Vector3)lineRenderer.lines[i].endPoint;      // Vector2 から Vector3 に変換
                totalCount += 2;
            }

            for (int i = 0; i < lineRenderer.circles.Count; i++)
            {
                totalStart += (Vector3)lineRenderer.circles[i].center;  // Vector2 から Vector3 に変換
                totalEnd += (Vector3)lineRenderer.circles[i].center;    // Vector2 から Vector3 に変換
                totalCount += 2;
            }

            Vector3 centerPoint = (totalStart + totalEnd) / totalCount; // 中心点を求める
            Vector3 centerWorldPos = mat.MultiplyPoint3x4(centerPoint); // ワールド座標に変換

            // 全体を操作するハンドルを作成
            EditorGUI.BeginChangeCheck();
            var fmh_42_27_638638635078395856 = Quaternion.identity;
            Vector3 newCenterWorldPos = Handles.FreeMoveHandle(centerWorldPos, HandleUtility.GetHandleSize(centerWorldPos) * 0.2f, Vector3.zero, Handles.CircleHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(lineRenderer, "Move All Lines and Circles");

                // 中心点の移動差分を計算
                Vector3 offset = newCenterWorldPos - centerWorldPos;

                // 各ラインを相対的に移動
                for (int i = 0; i < lineRenderer.lines.Count; i++)
                {
                    UILineRenderer.LineData line = lineRenderer.lines[i];

                    // ワールド座標に変換して移動後、ローカル座標に戻す
                    Vector3 newStartWorldPos = mat.MultiplyPoint3x4(line.startPoint) + offset;
                    Vector3 newEndWorldPos = mat.MultiplyPoint3x4(line.endPoint) + offset;

                    line.startPoint = inv.MultiplyPoint3x4(newStartWorldPos);
                    line.endPoint = inv.MultiplyPoint3x4(newEndWorldPos);
                }

                // 各円を相対的に移動
                for (int i = 0; i < lineRenderer.circles.Count; i++)
                {
                    UILineRenderer.CircleData circle = lineRenderer.circles[i];

                    // ワールド座標に変換して移動後、ローカル座標に戻す
                    Vector3 newCenterWorldPosCircle = mat.MultiplyPoint3x4(circle.center) + offset;
                    circle.center = inv.MultiplyPoint3x4(newCenterWorldPosCircle);
                }

                EditorUtility.SetDirty(lineRenderer);
                lineRenderer.SetAllDirty();
            }

            // 各ラインの個別ハンドルも引き続き操作可能
            for (int i = 0; i < lineRenderer.lines.Count; i++)
            {
                UILineRenderer.LineData line = lineRenderer.lines[i];

                EditorGUI.BeginChangeCheck();

                // 始点のハンドル操作
                Vector3 startWorldPos = mat.MultiplyPoint3x4(line.startPoint);
                var fmh_27_78_638638635078363493 = Quaternion.identity;
                Vector3 newStartWorldPos = Handles.FreeMoveHandle(startWorldPos, HandleUtility.GetHandleSize(startWorldPos) * 0.1f, Vector3.zero, Handles.DotHandleCap);

                // 終点のハンドル操作
                Vector3 endWorldPos = mat.MultiplyPoint3x4(line.endPoint);
                var fmh_31_74_638638635078393833 = Quaternion.identity;
                Vector3 newEndWorldPos = Handles.FreeMoveHandle(endWorldPos, HandleUtility.GetHandleSize(endWorldPos) * 0.1f, Vector3.zero, Handles.DotHandleCap);

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

            // 各円の個別ハンドルも引き続き操作可能
            for (int i = 0; i < lineRenderer.circles.Count; i++)
            {
                UILineRenderer.CircleData circle = lineRenderer.circles[i];

                EditorGUI.BeginChangeCheck();

                // 中心点のハンドル操作
                Vector3 centerWorldPosCircle = mat.MultiplyPoint3x4(circle.center);
                var fmh_27_78_638638635078363493 = Quaternion.identity;
                Vector3 newCenterWorldPosCircle = Handles.FreeMoveHandle(centerWorldPosCircle, HandleUtility.GetHandleSize(centerWorldPosCircle) * 0.1f, Vector3.zero, Handles.DotHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(lineRenderer, "Move Circle Center");

                    // ワールド座標からローカル座標に変換
                    Vector3 newCenterLocalPosCircle = inv.MultiplyPoint3x4(newCenterWorldPosCircle);
                    circle.center = newCenterLocalPosCircle;

                    EditorUtility.SetDirty(lineRenderer);
                    lineRenderer.SetAllDirty();
                }

                // ラベルを表示
                Handles.Label(centerWorldPosCircle, $"Circle {i} Center");
            }
        }

        // 横反転の処理
        private void FlipHorizontally(UILineRenderer lineRenderer)
        {
            Undo.RecordObject(lineRenderer, "Flip Horizontally");

            var rectTransform = lineRenderer.rectTransform;
            var sizeDelta = rectTransform.sizeDelta;

            // 各ラインの反転処理
            for (int i = 0; i < lineRenderer.lines.Count; i++)
            {
                UILineRenderer.LineData line = lineRenderer.lines[i];

                // rectTransform内で横に反転
                line.startPoint.x = sizeDelta.x - line.startPoint.x;
                line.endPoint.x = sizeDelta.x - line.endPoint.x;
            }

            // 各円の反転処理
            for (int i = 0; i < lineRenderer.circles.Count; i++)
            {
                UILineRenderer.CircleData circle = lineRenderer.circles[i];

                // rectTransform内で横に反転
                circle.center.x = sizeDelta.x - circle.center.x;
            }

            // 更新を反映させる
            EditorUtility.SetDirty(lineRenderer);
            lineRenderer.SetAllDirty();
        }
    }
}
