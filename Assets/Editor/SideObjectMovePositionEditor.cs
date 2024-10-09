using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(SideObjectMove))]
    public class SideObjectMovePositionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 対象のオブジェクトを取得
            SideObjectMove sideObjectMove = (SideObjectMove)target;

            // デフォルトのインスペクターを描画
            DrawDefaultInspector();

            // RectTransformを取得
            RectTransform rectTransform = sideObjectMove.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                EditorGUILayout.HelpBox("このGameObjectにはRectTransformコンポーネントがありません。", MessageType.Warning);
                return;
            }

            // スペースを追加
            EditorGUILayout.Space();

            // bornのpositionとscaleに記録するボタン
            if (GUILayout.Button("bornのpositionとscaleに記録する"))
            {
                Undo.RecordObject(sideObjectMove, "Record born position and scale");
                sideObjectMove.bornPos = rectTransform.anchoredPosition;
                sideObjectMove.bornScaleXY = rectTransform.localScale;
                EditorUtility.SetDirty(sideObjectMove);
                Debug.Log("bornPosとbornScaleに現在の値を記録しました。");
            }

            // 標準のpositionとscaleに記録するボタン
            if (GUILayout.Button("標準のpositionとscaleに記録する"))
            {
                Undo.RecordObject(sideObjectMove, "Record standard position and scale");
                sideObjectMove.pos = rectTransform.anchoredPosition;
                sideObjectMove.scaleXY = rectTransform.localScale;
                EditorUtility.SetDirty(sideObjectMove);
                Debug.Log("posとscaleに現在の値を記録しました。");
            }
        }
    }
}