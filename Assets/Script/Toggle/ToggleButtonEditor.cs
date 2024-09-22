using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

[CustomEditor(typeof(ToggleButton))]
public class ToggleButtonEditor : SelectableEditor//https://qiita.com/mikuri8/items/a546adb2451140167ce5
{
    SerializedProperty _defaultObject;
    SerializedProperty _selectedObject;
    SerializedProperty _myButtonRole;

    protected override void OnEnable()
    {
        base.OnEnable();
        _defaultObject = serializedObject.FindProperty("_defaultObject");
        //_selectedObject = serializedObject.FindProperty("_selectedObject");
        _myButtonRole = serializedObject.FindProperty("MyButtonRole");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();//selectableEditor�̕W��gui��`��
        EditorGUILayout.Space();//�X�y�[�X��ǉ����āA�W���@�\�ƃJ�X�^���@�\�̊Ԃ𕪂���A���₷���Ȃ�B

        serializedObject.Update();//�V���A���C�Y���Ă�̂��X�V����
        EditorGUILayout.PropertyField(_myButtonRole);
        EditorGUILayout.PropertyField(_defaultObject);
        //EditorGUILayout.PropertyField(_selectedObject);//�C���X�y�N�^�Ƀt�B�[���h��\��
        serializedObject.ApplyModifiedProperties();//�ύX���ꂽ�l�V���A���C�Y�̃v���p�e�B�̒l��K�p�����ۂ̃C���X�^���X�ɔ��f����B
    }
}