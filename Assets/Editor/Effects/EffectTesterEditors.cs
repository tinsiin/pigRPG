using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EffectsEditor
{
    /// <summary>
    /// EffectSystemTester / FieldEffectTester 共通のエフェクト選択ドロップダウン描画
    /// </summary>
    internal static class EffectTesterEditorHelper
    {
        private static string[] _effectNames;
        private static double _lastRefreshTime;
        private const double RefreshInterval = 2.0; // 秒

        /// <summary>
        /// エフェクト名一覧を取得（キャッシュ付き）
        /// </summary>
        public static string[] GetEffectNames()
        {
            if (_effectNames == null || EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                RefreshEffectList();
            }
            return _effectNames;
        }

        /// <summary>
        /// effectName プロパティをドロップダウンで描画
        /// </summary>
        public static void DrawEffectNameDropdown(SerializedProperty prop)
        {
            var names = GetEffectNames();
            if (names.Length == 0)
            {
                EditorGUILayout.HelpBox("Assets/Resources/Effects/ にJSONファイルが見つかりません", MessageType.Warning);
                EditorGUILayout.PropertyField(prop);
                return;
            }

            string current = prop.stringValue;
            int selectedIndex = System.Array.IndexOf(names, current);

            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup("エフェクト名", Mathf.Max(0, selectedIndex), names);
            if (newIndex >= 0 && newIndex < names.Length)
            {
                prop.stringValue = names[newIndex];
            }

            // 手動リフレッシュボタン
            if (GUILayout.Button("↻", GUILayout.Width(24)))
            {
                RefreshEffectList();
            }
            EditorGUILayout.EndHorizontal();

            // 現在値がリストにない場合は警告表示
            if (selectedIndex < 0 && !string.IsNullOrEmpty(current))
            {
                EditorGUILayout.HelpBox($"'{current}' はEffectsフォルダに存在しません", MessageType.Warning);
            }
        }

        private static void RefreshEffectList()
        {
            var list = new List<string>();
            const string dir = "Assets/Resources/Effects";
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, "*.json"))
                {
                    list.Add(Path.GetFileNameWithoutExtension(f));
                }
            }
            list.Sort();
            _effectNames = list.ToArray();
            _lastRefreshTime = EditorApplication.timeSinceStartup;
        }
    }

    /// <summary>
    /// EffectSystemTester のカスタムインスペクタ
    /// </summary>
    [CustomEditor(typeof(EffectSystemTester))]
    internal class EffectSystemTesterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // テスト対象
            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetIcon"));

            // エフェクト名（ドロップダウン）
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("エフェクト設定", EditorStyles.boldLabel);
            EffectTesterEditorHelper.DrawEffectNameDropdown(serializedObject.FindProperty("effectName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loop"));

            // テスト操作
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("テスト操作", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playOnStart"));

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// FieldEffectTester のカスタムインスペクタ
    /// </summary>
    [CustomEditor(typeof(FieldEffectTester))]
    internal class FieldEffectTesterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // エフェクト名（ドロップダウン）
            EditorGUILayout.LabelField("エフェクト設定", EditorStyles.boldLabel);
            EffectTesterEditorHelper.DrawEffectNameDropdown(serializedObject.FindProperty("effectName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loop"));

            // テスト操作
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("テスト操作", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playOnStart"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
