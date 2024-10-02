using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(ToggleButton))]
public class ToggleButtonEditor : SelectableEditor //https://qiita.com/mikuri8/items/a546adb2451140167ce5
{
    private SerializedProperty _defaultObject;
    private SerializedProperty _myButtonRole;
    private SerializedProperty _selectedObject;

    protected override void OnEnable()
    {
        base.OnEnable();
        _defaultObject = serializedObject.FindProperty("_defaultObject");
        //_selectedObject = serializedObject.FindProperty("_selectedObject");
        _myButtonRole = serializedObject.FindProperty("MyButtonRole");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI(); //selectableEditorの標準guiを描画
        EditorGUILayout.Space(); //スペースを追加して、標準機能とカスタム機能の間を分ける、見やすくなる。

        serializedObject.Update(); //シリアライズしてるのを更新する
        EditorGUILayout.PropertyField(_myButtonRole);
        EditorGUILayout.PropertyField(_defaultObject);
        //EditorGUILayout.PropertyField(_selectedObject);//インスペクタにフィールドを表示
        serializedObject.ApplyModifiedProperties(); //変更された値シリアライズのプロパティの値を適用し実際のインスタンスに反映する。
    }
}