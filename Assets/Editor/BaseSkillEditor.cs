using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// BaseSkill および派生クラス（AllySkill等）の PropertyDrawer。
/// Phase 2: SkillLevelDataが全値を持つため、BaseSkillDrawerは
/// スキル概要パネル + スキルレベルリスト管理 + バリデーション のみ担当。
/// 各レベルエントリの詳細は SkillLevelDataDrawer が描画する。
/// </summary>
[CustomPropertyDrawer(typeof(BaseSkill), true)]
public class BaseSkillDrawer : PropertyDrawer
{
    // ─── 定数 ───
    static readonly float LINE = EditorGUIUtility.singleLineHeight;
    const float PAD = 2f;
    const float HELPBOX_H = 38f;
    const float SEPARATOR_H = 14f;

    // ─── テンプレート定義 ───
    struct SkillTemplate
    {
        public string name;
        public SkillZoneTrait zoneTrait;
        public float skillPower;
        public SkillType skillType;
    }

    static readonly SkillTemplate[] s_templates = new[]
    {
        new SkillTemplate { name = "テンプレート選択...", zoneTrait = 0, skillPower = 0, skillType = 0 },
        new SkillTemplate { name = "単体攻撃（選択）",
            zoneTrait = SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.ControlByThisSituation | SkillZoneTrait.RandomSingleTarget,
            skillPower = 10f, skillType = SkillType.Attack },
        new SkillTemplate { name = "単体攻撃（ランダム）",
            zoneTrait = SkillZoneTrait.RandomSingleTarget,
            skillPower = 10f, skillType = SkillType.Attack },
        new SkillTemplate { name = "範囲攻撃（選択）",
            zoneTrait = SkillZoneTrait.CanSelectMultiTarget | SkillZoneTrait.ControlByThisSituation | SkillZoneTrait.RandomMultiTarget,
            skillPower = 7f, skillType = SkillType.Attack },
        new SkillTemplate { name = "全体攻撃",
            zoneTrait = SkillZoneTrait.AllTarget,
            skillPower = 5f, skillType = SkillType.Attack },
        new SkillTemplate { name = "回復（単体味方）",
            zoneTrait = SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.SelectOnlyAlly,
            skillPower = 10f, skillType = SkillType.Heal },
        new SkillTemplate { name = "回復（全体味方）",
            zoneTrait = SkillZoneTrait.AllTarget | SkillZoneTrait.SelectOnlyAlly,
            skillPower = 5f, skillType = SkillType.Heal },
        new SkillTemplate { name = "自己スキル",
            zoneTrait = SkillZoneTrait.SelfSkill,
            skillPower = 0f, skillType = 0 },
    };

    static string[] s_templateNames;

    // ─── バリデーション ───
    int ValidationWarningCount(SerializedProperty property)
    {
        int n = 0;
        var levelList = property.FindPropertyRelative("FixedSkillLevelData");
        if (levelList != null && levelList.arraySize == 0) n++;

        if (levelList != null && levelList.arraySize > 0)
        {
            var lv0 = levelList.GetArrayElementAtIndex(0);
            var zt = lv0.FindPropertyRelative("ZoneTrait");
            if (zt != null && zt.intValue == 0) n++;

            var name = lv0.FindPropertyRelative("SkillName");
            if (name != null && (string.IsNullOrEmpty(name.stringValue) || name.stringValue == "ここに名前を入れてください")) n++;

            var st = lv0.FindPropertyRelative("BaseSkillType");
            if (st != null && st.intValue == 0) n++;

            if (zt != null && zt.intValue != 0)
            {
                var ztVal = (SkillZoneTrait)zt.intValue;
                if ((ztVal & SkillZoneTrait.ControlByThisSituation) != 0)
                {
                    var accidentFlags = SkillZoneTrait.RandomSingleTarget | SkillZoneTrait.AllTarget | SkillZoneTrait.RandomMultiTarget;
                    if ((ztVal & accidentFlags) == 0) n++;
                }
                if (!SkillZoneTraitNormalizer.Validate(ztVal, out _)) n++;
            }
        }
        return n;
    }

    // ─── 高さ計算 ───
    float SummaryPanelHeight(SerializedProperty property)
    {
        float h = LINE + PAD; // "■ スキル概要"
        h += LINE + PAD; // スキル名
        h += SEPARATOR_H + PAD; // ━━ 必須設定 ━━
        h += (LINE + PAD) * 3; // 攻撃性質, 範囲性質, レベル数
        h += SEPARATOR_H + PAD; // ━━ 基本情報 ━━
        h += (LINE + PAD) * 5; // 連撃, 精神, 物理, コスト, 威力

        var levelList = property.FindPropertyRelative("FixedSkillLevelData");
        if (levelList != null && levelList.arraySize > 0)
        {
            var lv0 = levelList.GetArrayElementAtIndex(0);
            var hitPer = lv0.FindPropertyRelative("SkillHitPer");
            if (hitPer != null && hitPer.intValue != 0)
                h += LINE + PAD;
        }
        h += PAD * 2;
        return h;
    }

    /// <summary>
    /// 無限スケーリングセクションの高さ（ヘッダー + 2フィールド）
    /// </summary>
    float InfiniteScalingHeight(SerializedProperty property)
    {
        float h = LINE + PAD; // セクションヘッダー
        var infPower = property.FindPropertyRelative("_infiniteSkillPowerUnit");
        if (infPower != null) h += EditorGUI.GetPropertyHeight(infPower) + PAD;
        var infTen = property.FindPropertyRelative("_infiniteSkillTenDaysUnit");
        if (infTen != null) h += EditorGUI.GetPropertyHeight(infTen) + PAD;
        return h;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return LINE;

        float h = LINE + PAD; // 折りたたみ行
        h += LINE + PAD; // テンプレート行
        h += SummaryPanelHeight(property); // 概要パネル
        h += PAD * 2;

        // バリデーション警告
        h += ValidationWarningCount(property) * (HELPBOX_H + PAD);
        h += PAD * 2;

        // スキルレベルリスト
        var levelList = property.FindPropertyRelative("FixedSkillLevelData");
        if (levelList != null)
            h += EditorGUI.GetPropertyHeight(levelList, true) + PAD;

        // 無限スケーリング
        h += InfiniteScalingHeight(property);

        return h;
    }

    // ─── 描画 ───
    public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
    {
        if (s_templateNames == null)
        {
            s_templateNames = new string[s_templates.Length];
            for (int i = 0; i < s_templates.Length; i++)
                s_templateNames[i] = s_templates[i].name;
        }

        EditorGUI.BeginProperty(pos, label, property);
        float y = pos.y;

        // メインFoldout
        var foldoutRect = new Rect(pos.x, y, pos.width, LINE);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true, EditorStyles.foldoutHeader);
        y += LINE + PAD;

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        // テンプレート行
        DrawTemplateRow(ref y, pos, property);

        // スキル概要パネル
        DrawSummaryPanel(ref y, pos, property);
        y += PAD * 2;

        // バリデーション警告
        DrawValidationWarnings(ref y, pos, property);
        y += PAD * 2;

        // スキルレベルリスト（SkillLevelDataDrawerが各エントリを描画）
        var levelList = property.FindPropertyRelative("FixedSkillLevelData");
        if (levelList != null)
        {
            float h = EditorGUI.GetPropertyHeight(levelList, true);
            EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, h), levelList, new GUIContent("スキルレベルデータ"), true);
            y += h + PAD;
        }

        // 無限スケーリング
        DrawInfiniteScaling(ref y, pos, property);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    // ─── 無限スケーリングセクション ───
    void DrawInfiniteScaling(ref float y, Rect pos, SerializedProperty property)
    {
        // セクションヘッダー
        float indent = EditorGUI.indentLevel * 15f;
        EditorGUI.LabelField(
            new Rect(pos.x + indent, y, pos.width - indent, LINE),
            "\u2501\u2501 無限スケーリング \u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501",
            EditorStyles.miniLabel);
        y += LINE + PAD;

        var infPower = property.FindPropertyRelative("_infiniteSkillPowerUnit");
        if (infPower != null)
        {
            float h = EditorGUI.GetPropertyHeight(infPower);
            EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, h), infPower, new GUIContent("威力の無限単位"));
            y += h + PAD;
        }
        var infTen = property.FindPropertyRelative("_infiniteSkillTenDaysUnit");
        if (infTen != null)
        {
            float h = EditorGUI.GetPropertyHeight(infTen);
            EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, h), infTen, new GUIContent("10日能力の無限単位"));
            y += h + PAD;
        }
    }

    // ─── テンプレート行 ───
    void DrawTemplateRow(ref float y, Rect pos, SerializedProperty property)
    {
        float x = pos.x + EditorGUI.indentLevel * 15f;
        float w = pos.width - EditorGUI.indentLevel * 15f;
        float labelW = 80f;
        float btnW = 50f;
        float popupW = w - labelW - btnW - 8f;

        EditorGUI.LabelField(new Rect(x, y, labelW, LINE), "テンプレート");
        int selected = EditorGUI.Popup(new Rect(x + labelW + 4f, y, popupW, LINE), 0, s_templateNames);
        if (selected > 0)
        {
            ApplyTemplate(property, s_templates[selected]);
        }
        if (GUI.Button(new Rect(x + labelW + popupW + 8f, y, btnW, LINE), "適用"))
        {
            // ポップアップから選択済みなら既に適用されている
        }
        y += LINE + PAD;
    }

    void ApplyTemplate(SerializedProperty property, SkillTemplate template)
    {
        var listProp = property.FindPropertyRelative("FixedSkillLevelData");
        if (listProp == null) return;

        if (listProp.arraySize == 0)
            listProp.InsertArrayElementAtIndex(0);
        var firstEntry = listProp.GetArrayElementAtIndex(0);

        var zoneProp = firstEntry.FindPropertyRelative("ZoneTrait");
        if (zoneProp != null) zoneProp.intValue = (int)template.zoneTrait;

        var powerProp = firstEntry.FindPropertyRelative("SkillPower");
        if (powerProp != null) powerProp.floatValue = template.skillPower;

        var typeProp = firstEntry.FindPropertyRelative("BaseSkillType");
        if (typeProp != null) typeProp.intValue = (int)template.skillType;
    }

    // ─── スキル概要パネル ───
    void DrawSummaryPanel(ref float y, Rect pos, SerializedProperty property)
    {
        float panelH = SummaryPanelHeight(property);
        var panelRect = new Rect(pos.x + EditorGUI.indentLevel * 15f, y, pos.width - EditorGUI.indentLevel * 15f, panelH);
        GUI.Box(panelRect, GUIContent.none, EditorStyles.helpBox);

        float px = panelRect.x + 8f;
        float pw = panelRect.width - 16f;
        float py = panelRect.y + PAD;

        var levelList = property.FindPropertyRelative("FixedSkillLevelData");
        bool hasLevels = levelList != null && levelList.arraySize > 0;
        SerializedProperty lv0 = hasLevels ? levelList.GetArrayElementAtIndex(0) : null;

        // ■ スキル概要
        EditorGUI.LabelField(new Rect(px, py, pw, LINE), "\u25A0 スキル概要", EditorStyles.boldLabel);
        py += LINE + PAD;

        // スキル名
        string skillName = "???";
        if (lv0 != null)
        {
            var nameProp = lv0.FindPropertyRelative("SkillName");
            if (nameProp != null) skillName = nameProp.stringValue;
        }
        EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  スキル名:  " + skillName);
        py += LINE + PAD;

        // ━━ 必須設定 ━━
        EditorGUI.LabelField(new Rect(px, py, pw, SEPARATOR_H),
            "  \u2501\u2501 必須設定 \u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501",
            EditorStyles.miniLabel);
        py += SEPARATOR_H + PAD;

        // 攻撃性質
        int stVal = 0;
        if (lv0 != null)
        {
            var stProp = lv0.FindPropertyRelative("BaseSkillType");
            if (stProp != null) stVal = stProp.intValue;
        }
        DrawRequiredRow(ref py, px, pw, "攻撃性質", stVal != 0,
            stVal != 0 ? ((SkillType)stVal).ToString() : "未設定");

        // 範囲性質
        int ztVal = 0;
        if (lv0 != null)
        {
            var ztProp = lv0.FindPropertyRelative("ZoneTrait");
            if (ztProp != null) ztVal = ztProp.intValue;
        }
        DrawRequiredRow(ref py, px, pw, "範囲性質", ztVal != 0,
            ztVal != 0 ? FormatZoneTrait((SkillZoneTrait)ztVal) : "未設定");

        // レベル数
        int levelCount = hasLevels ? levelList.arraySize : 0;
        var infProp = property.FindPropertyRelative("_infiniteSkillPowerUnit");
        bool hasInfinite = infProp != null && infProp.floatValue > 0f;
        string levelText = levelCount > 0
            ? levelCount + " (有限)" + (hasInfinite ? " + 無限" : "")
            : "0";
        DrawRequiredRow(ref py, px, pw, "レベル数", levelCount > 0, levelText);

        // ━━ 基本情報 ━━
        EditorGUI.LabelField(new Rect(px, py, pw, SEPARATOR_H),
            "  \u2501\u2501 基本情報 \u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501",
            EditorStyles.miniLabel);
        py += SEPARATOR_H + PAD;

        var style = EditorStyles.miniLabel;

        if (lv0 != null)
        {
            var conProp = lv0.FindPropertyRelative("ConsecutiveType");
            EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  連撃性質:  " + (conProp != null ? ((SkillConsecutiveType)conProp.intValue).ToString() : "?"), style);
            py += LINE + PAD;

            var spirProp = lv0.FindPropertyRelative("SkillSpiritual");
            EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  精神属性:  " + (spirProp != null ? ((SpiritualProperty)spirProp.intValue).ToString() : "?"), style);
            py += LINE + PAD;

            var physProp = lv0.FindPropertyRelative("SkillPhysical");
            EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  物理属性:  " + (physProp != null ? ((PhysicalProperty)physProp.intValue).ToString() : "?"), style);
            py += LINE + PAD;

            var npProp = lv0.FindPropertyRelative("RequiredNormalP");
            EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  コスト:    NP: " + (npProp != null ? npProp.intValue.ToString() : "?"), style);
            py += LINE + PAD;

            var powerProp = lv0.FindPropertyRelative("SkillPower");
            EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  威力:      Lv0 = " + (powerProp != null ? powerProp.floatValue.ToString("F1") : "?"), style);
            py += LINE + PAD;

            var hitProp = lv0.FindPropertyRelative("SkillHitPer");
            if (hitProp != null && hitProp.intValue != 0)
            {
                int hitPer = hitProp.intValue;
                EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  命中補正:  " + (hitPer > 0 ? "+" : "") + hitPer + "%", style);
                py += LINE + PAD;
            }
        }
        else
        {
            for (int i = 0; i < 5; i++)
            {
                EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  (レベルデータなし)", style);
                py += LINE + PAD;
            }
        }

        y += panelH;
    }

    void DrawRequiredRow(ref float py, float px, float pw, string label, bool isSet, string valueText)
    {
        float labelW = 80f;
        EditorGUI.LabelField(new Rect(px, py, labelW, LINE), "  " + label + ":", EditorStyles.boldLabel);

        var prevColor = GUI.color;
        if (isSet)
        {
            GUI.color = new Color(0.2f, 0.85f, 0.3f);
            EditorGUI.LabelField(new Rect(px + labelW, py, pw - labelW, LINE), valueText + "  \u2714", EditorStyles.boldLabel);
        }
        else
        {
            GUI.color = new Color(1f, 0.3f, 0.3f);
            EditorGUI.LabelField(new Rect(px + labelW, py, pw - labelW, LINE), valueText + "  \u26A0", EditorStyles.boldLabel);
        }
        GUI.color = prevColor;
        py += LINE + PAD;
    }

    // ─── バリデーション警告 ───
    void DrawValidationWarnings(ref float y, Rect pos, SerializedProperty property)
    {
        var levelList = property.FindPropertyRelative("FixedSkillLevelData");
        if (levelList != null && levelList.arraySize == 0)
            DrawHelpBox(ref y, pos, "スキルレベルデータが未設定です。最低1つのレベルデータを設定してください。", MessageType.Error);

        if (levelList != null && levelList.arraySize > 0)
        {
            var lv0 = levelList.GetArrayElementAtIndex(0);

            var ztProp = lv0.FindPropertyRelative("ZoneTrait");
            if (ztProp != null && ztProp.intValue == 0)
                DrawHelpBox(ref y, pos, "ZoneTraitが未設定です。スキルの範囲性質を設定してください。", MessageType.Error);

            var nameProp = lv0.FindPropertyRelative("SkillName");
            if (nameProp != null && (string.IsNullOrEmpty(nameProp.stringValue) || nameProp.stringValue == "ここに名前を入れてください"))
                DrawHelpBox(ref y, pos, "スキル名を設定してください。", MessageType.Warning);

            var stProp = lv0.FindPropertyRelative("BaseSkillType");
            if (stProp != null && stProp.intValue == 0)
                DrawHelpBox(ref y, pos, "スキルの攻撃性質が未設定です。攻撃判定がfalseになります。", MessageType.Warning);

            if (ztProp != null && ztProp.intValue != 0)
            {
                var ztVal = (SkillZoneTrait)ztProp.intValue;
                if ((ztVal & SkillZoneTrait.ControlByThisSituation) != 0)
                {
                    var accidentFlags = SkillZoneTrait.RandomSingleTarget | SkillZoneTrait.AllTarget | SkillZoneTrait.RandomMultiTarget;
                    if ((ztVal & accidentFlags) == 0)
                        DrawHelpBox(ref y, pos, "状況制御の事故用フラグ（ランダム単体/全体/ランダム範囲）を設定してください。", MessageType.Warning);
                }
                if (!SkillZoneTraitNormalizer.Validate(ztVal, out var msg))
                    DrawHelpBox(ref y, pos, msg, MessageType.Warning);
            }
        }
    }

    // ─── ユーティリティ ───
    void DrawHelpBox(ref float y, Rect pos, string msg, MessageType type)
    {
        float indent = EditorGUI.indentLevel * 15f;
        EditorGUI.HelpBox(new Rect(pos.x + indent, y, pos.width - indent, HELPBOX_H), msg, type);
        y += HELPBOX_H + PAD;
    }

    static string FormatZoneTrait(SkillZoneTrait trait)
    {
        var parts = new List<string>(8);
        if ((trait & SkillZoneTrait.CanPerfectSelectSingleTarget) != 0) parts.Add("完全選択単体");
        if ((trait & SkillZoneTrait.CanSelectSingleTarget) != 0) parts.Add("選択単体");
        if ((trait & SkillZoneTrait.RandomSingleTarget) != 0) parts.Add("ランダム単体");
        if ((trait & SkillZoneTrait.ControlByThisSituation) != 0) parts.Add("状況制御");
        if ((trait & SkillZoneTrait.CanSelectMultiTarget) != 0) parts.Add("選択範囲");
        if ((trait & SkillZoneTrait.RandomMultiTarget) != 0) parts.Add("ランダム範囲");
        if ((trait & SkillZoneTrait.AllTarget) != 0) parts.Add("全体");
        if ((trait & SkillZoneTrait.SelectOnlyAlly) != 0) parts.Add("味方のみ");
        if ((trait & SkillZoneTrait.SelfSkill) != 0) parts.Add("自己");
        if (parts.Count > 4) return parts[0] + " + " + parts[1] + " + ... (" + parts.Count + "個)";
        return string.Join(" + ", parts);
    }
}
