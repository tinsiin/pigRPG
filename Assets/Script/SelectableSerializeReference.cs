using System;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine.Assertions;
#endif
//https://light11.hatenadiary.com/entry/2021/11/30/190034
//こちらからコピペ　
[AttributeUsage(AttributeTargets.Field)]
public sealed class SelectableSerializeReferenceAttribute : PropertyAttribute
{
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SelectableSerializeReferenceAttribute))]
public sealed class SelectableSerializeReferenceAttributeDrawer : PropertyDrawer
{
    private readonly Dictionary<string, PropertyData> _dataPerPath =
        new Dictionary<string, PropertyData>();

    private PropertyData _data;

    private int _selectedIndex;

    private void Init(SerializedProperty property)
    {
        if (_dataPerPath.TryGetValue(property.propertyPath, out _data))
        {
            return;
        }

        _data = new PropertyData(property);
        _dataPerPath.Add(property.propertyPath, _data);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Assert.IsTrue(property.propertyType == SerializedPropertyType.ManagedReference);

        Init(property);

        var fullTypeName = property.managedReferenceFullTypename.Split(' ').Last();
        _selectedIndex = Array.IndexOf(_data.DerivedFullTypeNames, fullTypeName);

        using (var ccs = new EditorGUI.ChangeCheckScope())
        {
            var selectorPosition = position;

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            selectorPosition.width -= EditorGUIUtility.labelWidth;
            selectorPosition.x += EditorGUIUtility.labelWidth;
            selectorPosition.height = EditorGUIUtility.singleLineHeight;

            // Type selection popup only (inline Make Unique button removed; use context menu instead)
            var selectedTypeIndex = EditorGUI.Popup(selectorPosition, _selectedIndex, _data.DerivedTypeNames);
            if (ccs.changed)
            {
                _selectedIndex = selectedTypeIndex;
                var selectedType = _data.DerivedTypes[selectedTypeIndex];
                property.managedReferenceValue =
                    selectedType == null ? null : Activator.CreateInstance(selectedType);
            }


            // Context menu (Right-Click) with description and Make Unique action
            if (Event.current.type == EventType.ContextClick && position.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                // Explanation (disabled items)
                menu.AddDisabledItem(new GUIContent("Make Unique が必要な理由"));
                menu.AddDisabledItem(new GUIContent("Duplicate は SerializeReference の参照を複製します"));
                menu.AddDisabledItem(new GUIContent("=> 編集が相互反映。独立複製で共有参照を解消"));
                menu.AddSeparator("");
                // Action
                menu.AddItem(new GUIContent("Make Unique"), false, () =>
                {
                    var src = property.managedReferenceValue;
                    if (src == null) return;
                    object dst = null;
                    try
                    {
                        var srcType = src.GetType();
                        var m = srcType.GetMethod("InitAllyDeepCopy", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                        if (m != null)
                        {
                            dst = m.Invoke(src, null);
                        }
                        if (dst == null)
                        {
                            dst = Activator.CreateInstance(srcType);
                            var json = JsonUtility.ToJson(src);
                            JsonUtility.FromJsonOverwrite(json, dst);
                        }
                        Undo.RecordObject(property.serializedObject.targetObject, "Make Unique SerializeReference");
                        property.managedReferenceValue = dst;
                        property.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(property.serializedObject.targetObject);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Make Unique failed: {e.Message}\n{e}");
                    }
                });
                menu.ShowAsContext();
                Event.current.Use();
            }

            EditorGUI.indentLevel = indent;
        }

        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        Init(property);

        if (string.IsNullOrEmpty(property.managedReferenceFullTypename))
        {
            return EditorGUIUtility.singleLineHeight;
        }

        return EditorGUI.GetPropertyHeight(property, true);
    }

    private class PropertyData
    {
        /*public PropertyData(SerializedProperty property)
        {
            var managedReferenceFieldTypenameSplit = property.managedReferenceFieldTypename.Split(' ').ToArray();
            var assemblyName = managedReferenceFieldTypenameSplit[0];
            var fieldTypeName = managedReferenceFieldTypenameSplit[1];
            var fieldType = GetAssembly(assemblyName).GetType(fieldTypeName);
            DerivedTypes = TypeCache.GetTypesDerivedFrom(fieldType).Where(x => !x.IsAbstract && !x.IsInterface)
                .ToArray();
            DerivedTypeNames = new string[DerivedTypes.Length];
            DerivedFullTypeNames = new string[DerivedTypes.Length];
            for (var i = 0; i < DerivedTypes.Length; i++)
            {
                var type = DerivedTypes[i];
                DerivedTypeNames[i] = ObjectNames.NicifyVariableName(type.Name);
                DerivedFullTypeNames[i] = type.FullName;
            }
        }*/

        //chatGPT o1によって書き換えられた　非抽象のベースクラスも選択可能にしたもの
        public PropertyData(SerializedProperty property)
        {
            var managedReferenceFieldTypenameSplit = property.managedReferenceFieldTypename.Split(' ').ToArray();
            var assemblyName = managedReferenceFieldTypenameSplit[0];
            var fieldTypeName = managedReferenceFieldTypenameSplit[1];
            var fieldType = GetAssembly(assemblyName).GetType(fieldTypeName);

            // まず派生クラスを列挙
            var derived = TypeCache.GetTypesDerivedFrom(fieldType)
                .Where(x => !x.IsAbstract && !x.IsInterface)
                .ToList();

            // もしフィールド型 itself が実装クラスなら、それも候補に入れる
            if (!fieldType.IsAbstract && !fieldType.IsInterface)
            {
                // 先頭に挿入
                derived.Insert(0, fieldType);
            }

            DerivedTypes = derived.ToArray();
            DerivedTypeNames = new string[DerivedTypes.Length];
            DerivedFullTypeNames = new string[DerivedTypes.Length];

            for (var i = 0; i < DerivedTypes.Length; i++)
            {
                var type = DerivedTypes[i];
                DerivedTypeNames[i] = ObjectNames.NicifyVariableName(type.Name);
                DerivedFullTypeNames[i] = type.FullName;
            }
        }


        public Type[] DerivedTypes { get; }

        public string[] DerivedTypeNames { get; }

        public string[] DerivedFullTypeNames { get; }

        private static Assembly GetAssembly(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name == name);
        }
    }
}
#endif