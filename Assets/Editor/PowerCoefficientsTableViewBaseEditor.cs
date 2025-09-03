using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PowerCoefficientsTableViewBase), true)]
public class PowerCoefficientsTableViewBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 既定のインスペクタ
        DrawDefaultInspector();

        var view = target as PowerCoefficientsTableViewBase;
        if (view == null) return;
        serializedObject.Update();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Static Baked Data Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Bake Data From Current", GUILayout.Height(24)))
            {
                Undo.RecordObject(view, $"Bake {view.GetType().Name} Table Data");
                view.BakeDataFromCurrent();
                EditorUtility.SetDirty(view);
            }
        }
        if (GUILayout.Button("Clear Baked Data", GUILayout.Height(24), GUILayout.Width(160)))
        {
            Undo.RecordObject(view, "Clear Baked Data");
            view.ClearBakedData();
            EditorUtility.SetDirty(view);
        }
        EditorGUILayout.EndHorizontal();

        // 状態ヘルプ
        var propStatic = serializedObject.FindProperty("m_StaticMode");
        bool isStatic = propStatic != null && propStatic.boolValue;
        bool hasBaked = view.HasBakedDataForEditor();

        EditorGUILayout.Space(4);
        if (isStatic)
        {
            if (!hasBaked)
            {
                EditorGUILayout.HelpBox("静的モードですが、ベイク済みデータがありません。実行時に何も表示されません。必要ならベイクしてください。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("静的モード: ベイク済みデータで描画します（レイアウト計算なし）。", MessageType.None);
            }
        }
        else
        {
            if (hasBaked)
            {
                EditorGUILayout.HelpBox("ベイク済みデータがあります。静的モードをONにすると、ランタイム負荷を最小化できます。", MessageType.Info);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
