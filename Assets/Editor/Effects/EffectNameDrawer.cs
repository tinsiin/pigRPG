using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// [EffectName] 属性用 PropertyDrawer。
/// Assets/Resources/Effects/*.json を走査し、targetフィルタに応じてドロップダウン表示する。
/// </summary>
[CustomPropertyDrawer(typeof(EffectNameAttribute))]
public class EffectNameDrawer : PropertyDrawer
{
    // エフェクト名 → target ("icon" / "field")
    static Dictionary<string, string> s_effectTargets;
    // フィルタキー → (名前配列, 表示名配列)
    static Dictionary<string, (string[] names, string[] display)> s_filtered = new();
    static double s_cacheTime;
    const double CACHE_LIFETIME = 5.0;

    static void RefreshCache()
    {
        if (s_effectTargets != null && EditorApplication.timeSinceStartup - s_cacheTime < CACHE_LIFETIME)
            return;

        s_effectTargets = new Dictionary<string, string>();
        s_filtered.Clear();

        var effectsPath = "Assets/Resources/Effects";
        if (Directory.Exists(effectsPath))
        {
            foreach (var file in Directory.GetFiles(effectsPath, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                s_effectTargets[name] = ParseTarget(file);
            }
        }

        s_cacheTime = EditorApplication.timeSinceStartup;
    }

    static string ParseTarget(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            var m = Regex.Match(json, @"""target""\s*:\s*""(\w+)""");
            return m.Success ? m.Groups[1].Value : "icon";
        }
        catch { return "icon"; }
    }

    static (string[] names, string[] display) GetFiltered(string filter)
    {
        string key = filter ?? "";
        if (s_filtered.TryGetValue(key, out var cached))
            return cached;

        var list = new List<string>();
        foreach (var kvp in s_effectTargets)
        {
            if (string.IsNullOrEmpty(key) || kvp.Value == key)
                list.Add(kvp.Key);
        }
        list.Sort();

        var names = list.ToArray();
        var display = new string[names.Length + 1];
        display[0] = "(なし)";
        for (int i = 0; i < names.Length; i++)
            display[i + 1] = names[i];

        var result = (names, display);
        s_filtered[key] = result;
        return result;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        RefreshCache();

        var attr = (EffectNameAttribute)attribute;
        var (effectNames, displayNames) = GetFiltered(attr.TargetFilter);

        string current = property.stringValue ?? "";
        int selected = 0;
        string[] popupNames = effectNames;
        string[] popupDisplay = displayNames;

        if (!string.IsNullOrEmpty(current))
        {
            int idx = System.Array.IndexOf(effectNames, current);
            if (idx >= 0)
            {
                selected = idx + 1;
            }
            else
            {
                // 現在値がフィルタに合わない → 警告付きで先頭に表示
                popupNames = new string[effectNames.Length + 1];
                popupDisplay = new string[effectNames.Length + 2];

                popupNames[0] = current;
                for (int i = 0; i < effectNames.Length; i++)
                    popupNames[i + 1] = effectNames[i];

                popupDisplay[0] = "(なし)";
                popupDisplay[1] = $"\u26a0 {current} (target\u4e0d\u4e00\u81f4)";
                for (int i = 0; i < effectNames.Length; i++)
                    popupDisplay[i + 2] = effectNames[i];

                selected = 1;
            }
        }

        EditorGUI.BeginProperty(position, label, property);
        int newSelected = EditorGUI.Popup(position, label.text, selected, popupDisplay);
        if (newSelected != selected)
            property.stringValue = newSelected == 0 ? "" : popupNames[newSelected - 1];
        EditorGUI.EndProperty();
    }
}
