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
            if (GUILayout.Button("bornのpositionとscaleとrotationZに記録する"))
            {
                Undo.RecordObject(sideObjectMove, "Record born position and scale");
                sideObjectMove.bornPos = rectTransform.anchoredPosition;
                sideObjectMove.bornScaleXY = rectTransform.localScale;
                sideObjectMove.bornRotationZ = rectTransform.localEulerAngles.z;
                EditorUtility.SetDirty(sideObjectMove);
                Debug.Log("bornPosとbornScaleとbornRotationZに現在の値を記録しました。");
            }

            // 標準のpositionとscaleに記録するボタン
            if (GUILayout.Button("標準のpositionとscaleとrotationZに記録する"))
            {
                Undo.RecordObject(sideObjectMove, "Record standard position and scale");
                sideObjectMove.pos = rectTransform.anchoredPosition;
                sideObjectMove.scaleXY = rectTransform.localScale;
                sideObjectMove.rotationZ = rectTransform.localEulerAngles.z;
                EditorUtility.SetDirty(sideObjectMove);
                Debug.Log("posとscaleとrotationZに現在の値を記録しました。");
            }

            // **ここから新しいボタンの追加**

            // _midPosと_midScaleXYに記録するボタン
            if (GUILayout.Button("_midPosと_midScaleXYに現在の値を記録する"))
            {
                Undo.RecordObject(sideObjectMove, "Record mid position and scale");
                sideObjectMove._midPos = rectTransform.anchoredPosition;
                sideObjectMove._midScaleXY = rectTransform.localScale;
                EditorUtility.SetDirty(sideObjectMove);
                Debug.Log("_midPosと_midScaleXYに現在の値を記録しました。");
            }

            // _endPosと_endScaleXYに記録するボタン
            if (GUILayout.Button("_endPosと_endScaleXYに現在の値を記録する"))
            {
                Undo.RecordObject(sideObjectMove, "Record end position and scale");
                sideObjectMove._endPos = rectTransform.anchoredPosition;
                sideObjectMove._endScaleXY = rectTransform.localScale;
                EditorUtility.SetDirty(sideObjectMove);
                Debug.Log("_endPosと_endScaleXYに現在の値を記録しました。");
            }

            // スペースを追加
            EditorGUILayout.Space();

            // bornのpositionとscaleを適用するボタン
            if (GUILayout.Button("bornのpositionとscaleとrotationZを現在のエディタ上の値に適用する"))
            {
                Undo.RecordObject(rectTransform, "Apply born position and scale");
                rectTransform.anchoredPosition = sideObjectMove.bornPos;
                rectTransform.localScale = sideObjectMove.bornScaleXY;
                Vector3 currentRotation = rectTransform.localEulerAngles;
                currentRotation.z = sideObjectMove.bornRotationZ;
                rectTransform.localEulerAngles = currentRotation;
                EditorUtility.SetDirty(rectTransform);
                Debug.Log("bornPosとbornScaleとbornRotationZをRectTransformに適用しました。");
            }

            // 標準のpositionとscaleを適用するボタン
            if (GUILayout.Button("標準のpositionとscaleとrotationZを現在のエディタ上の値に適用する"))
            {
                Undo.RecordObject(rectTransform, "Apply standard position and scale");
                rectTransform.anchoredPosition = sideObjectMove.pos;
                rectTransform.localScale = sideObjectMove.scaleXY;
                Vector3 currentRotation = rectTransform.localEulerAngles;
                currentRotation.z = sideObjectMove.rotationZ;
                rectTransform.localEulerAngles = currentRotation;
                EditorUtility.SetDirty(rectTransform);
                Debug.Log("posとscaleとrotationZをRectTransformに適用しました。");
            }

            // _midPosと_midScaleXYを適用するボタン
            if (GUILayout.Button("_midPosと_midScaleXYを現在のエディタ上の値に適用する"))
            {
                Undo.RecordObject(rectTransform, "Apply mid position and scale");
                rectTransform.anchoredPosition = sideObjectMove._midPos;
                rectTransform.localScale = sideObjectMove._midScaleXY;
                EditorUtility.SetDirty(rectTransform);
                Debug.Log("_midPosと_midScaleXYをRectTransformに適用しました。");
            }

            // _endPosと_endScaleXYを適用するボタン
            if (GUILayout.Button("_endPosと_endScaleXYを現在のエディタ上の値に適用する"))
            {
                Undo.RecordObject(rectTransform, "Apply end position and scale");
                rectTransform.anchoredPosition = sideObjectMove._endPos;
                rectTransform.localScale = sideObjectMove._endScaleXY;
                EditorUtility.SetDirty(rectTransform);
                Debug.Log("_endPosと_endScaleXYをRectTransformに適用しました。");
            }

            // スペースを追加
            EditorGUILayout.Space();

            // sizeDeltaをbaseSizeに記録するボタン
            if (GUILayout.Button("現在のsizeDeltaをbaseSizeに記録する"))
            {
                Undo.RecordObject(sideObjectMove, "Record sizeDelta as baseSize");
                sideObjectMove.baseSize = rectTransform.sizeDelta;
                EditorUtility.SetDirty(sideObjectMove);
                Debug.Log("現在のsizeDeltaをbaseSizeに記録しました。");
            }

            // baseSizeをsizeDeltaに適用するボタン
            if (GUILayout.Button("baseSizeを現在のsizeDeltaに適用する"))
            {
                Undo.RecordObject(rectTransform, "Apply baseSize to sizeDelta");
                rectTransform.sizeDelta = sideObjectMove.baseSize;
                EditorUtility.SetDirty(rectTransform);
                Debug.Log("baseSizeをsizeDeltaに適用しました。");
            }
        }
    }
}
