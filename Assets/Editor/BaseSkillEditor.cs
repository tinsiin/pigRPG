using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// BaseSkill および派生クラス（AllySkill等）の PropertyDrawer。
/// [Serializable] クラスとして WeaponManager, AllyClass 等のフィールドに埋め込まれている BaseSkill を
/// 10セクションFoldout + スキル概要パネル + テンプレート + バリデーション で表示する。
/// </summary>
[CustomPropertyDrawer(typeof(BaseSkill), true)]
public class BaseSkillDrawer : PropertyDrawer
{
    // ─── 定数 ───
    static readonly float LINE = EditorGUIUtility.singleLineHeight;
    const float PAD = 2f;
    const float HELPBOX_H = 38f;
    const float SEPARATOR_H = 14f;
    const int SECTION_COUNT = 9;

    // ─── テンプレート定義 ───
    struct SkillTemplate
    {
        public string name;
        public SkillZoneTrait zoneTrait;
        public float skillPower;
    }

    static readonly SkillTemplate[] s_templates = new[]
    {
        new SkillTemplate { name = "テンプレート選択...", zoneTrait = 0, skillPower = 0 },
        new SkillTemplate { name = "単体攻撃（選択）",
            zoneTrait = SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.ControlByThisSituation | SkillZoneTrait.RandomSingleTarget,
            skillPower = 10f },
        new SkillTemplate { name = "単体攻撃（ランダム）",
            zoneTrait = SkillZoneTrait.RandomSingleTarget,
            skillPower = 10f },
        new SkillTemplate { name = "範囲攻撃（選択）",
            zoneTrait = SkillZoneTrait.CanSelectMultiTarget | SkillZoneTrait.ControlByThisSituation | SkillZoneTrait.RandomMultiTarget,
            skillPower = 7f },
        new SkillTemplate { name = "全体攻撃",
            zoneTrait = SkillZoneTrait.AllTarget,
            skillPower = 5f },
        new SkillTemplate { name = "回復（単体味方）",
            zoneTrait = SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.SelectOnlyAlly,
            skillPower = 10f },
        new SkillTemplate { name = "回復（全体味方）",
            zoneTrait = SkillZoneTrait.AllTarget | SkillZoneTrait.SelectOnlyAlly,
            skillPower = 5f },
        new SkillTemplate { name = "自己スキル",
            zoneTrait = SkillZoneTrait.SelfSkill,
            skillPower = 0f },
    };

    static string[] s_templateNames;

    // ─── セクション定義 ───
    static readonly string[] s_sectionLabels = new[]
    {
        "\u2460 基本情報 \u2014 このスキルは何者か",
        "\u2461 スキル性質 \u2014 どういう攻撃か、誰に当たるか",
        "\u2462 威力・命中・ダメージ \u2014 どのくらい痛いか",
        "\u2463 コスト・補正 \u2014 使うのに何が要るか",
        "\u2464 連撃・ストック・トリガー \u2014 実行の仕組み",
        "\u2465 前のめり \u2014 いつ前のめりになるか",
        "\u2466 ムーブセット \u2014 連撃時の動作パターン",
        "\u2467 スキルレベル \u2014 レベルごとの成長データ",
        "\u2468 エフェクト・パッシブ付与 \u2014 何を付与/除去するか",
    };

    static readonly string[][] s_sectionFields = new[]
    {
        // ① 基本情報
        new[] { "SkillName", "SkillSpiritual", "SkillPhysical", "Impression", "MotionFlavor", "SpecialFlags" },
        // ② スキル性質
        new[] { "_baseSkillType", "ConsecutiveType", "ZoneTrait", "DistributionType", "PowerRangePercentageDictionary", "HitRangePercentageDictionary" },
        // ③ 威力・命中・ダメージ
        new[] { "_skillHitPer", "_mentalDamageRatio", "_defAtk", "_powerSpread", "Cantkill" },
        // ④ コスト・補正
        new[] { "RequiredNormalP", "RequiredAttrP", "RequiredRemainingHPPercent", "EvasionModifier", "AttackModifier", "AttackMentalHealPercent", "SKillDidWaitCount" },
        // ⑤ 連撃・ストック・トリガー
        new[] { "_RandomConsecutivePer", "_defaultStockCount", "_stockPower", "_stockForgetPower", "_triggerCountMax", "CanCancelTrigger", "_triggerRollBackCount" },
        // ⑥ 前のめり
        new[] { "AggressiveOnExecute", "AggressiveOnTrigger", "AggressiveOnStock" },
        // ⑦ ムーブセット
        new[] { "_a_moveset", "_b_moveset" },
        // ⑧ スキルレベル
        new[] { "FixedSkillLevelData", "_infiniteSkillPowerUnit", "_infiniteSkillTenDaysUnit" },
        // ⑨ エフェクト・パッシブ付与
        new[] { "subEffects", "subVitalLayers", "canEraceEffectIDs", "CanEraceEffectCount", "canEraceVitalLayerIDs", "CanEraceVitalLayerCount",
                "ReactiveSkillPassiveList", "AggressiveSkillPassiveList", "TargetSelection", "ReactionCharaAndSkillList", "SkillPassiveEffectCount", "_skillPassiveGibeSkill_SkillFilter" },
    };

    // 日本語ラベル対応表
    static readonly Dictionary<string, string> s_fieldLabels = new()
    {
        { "SkillName", "スキル名" },
        { "SkillSpiritual", "精神属性" },
        { "SkillPhysical", "物理属性" },
        { "Impression", "スキル印象" },
        { "MotionFlavor", "動作的雰囲気" },
        { "SpecialFlags", "特殊判別性質" },
        { "_baseSkillType", "攻撃性質" },
        { "ConsecutiveType", "連撃性質" },
        { "ZoneTrait", "範囲性質" },
        { "DistributionType", "分散性質" },
        { "PowerRangePercentageDictionary", "威力の範囲別割合差分" },
        { "HitRangePercentageDictionary", "命中率の範囲別補正" },
        { "_skillHitPer", "命中補正 (%)" },
        { "_mentalDamageRatio", "精神攻撃率" },
        { "_defAtk", "防御無視率" },
        { "_powerSpread", "分散割合" },
        { "Cantkill", "殺せない (1残る)" },
        { "RequiredNormalP", "必要ノーマルP" },
        { "RequiredAttrP", "必要属性P内訳" },
        { "RequiredRemainingHPPercent", "必要残りHP割合" },
        { "EvasionModifier", "回避補正率" },
        { "AttackModifier", "攻撃補正率" },
        { "AttackMentalHealPercent", "攻撃時精神HP回復%" },
        { "SKillDidWaitCount", "追加硬直値" },
        { "_RandomConsecutivePer", "ランダム連撃継続率 (%)" },
        { "_defaultStockCount", "デフォルトストック数" },
        { "_stockPower", "ストック単位" },
        { "_stockForgetPower", "ストック忘れ単位" },
        { "_triggerCountMax", "トリガー必要カウント数" },
        { "CanCancelTrigger", "トリガー中断可" },
        { "_triggerRollBackCount", "巻き戻りカウント" },
        { "AggressiveOnExecute", "前のめり設定（実行時）" },
        { "AggressiveOnTrigger", "前のめり設定（トリガー時）" },
        { "AggressiveOnStock", "前のめり設定（ストック時）" },
        { "_a_moveset", "戦闘規格A ムーブセット" },
        { "_b_moveset", "戦闘規格B ムーブセット" },
        { "FixedSkillLevelData", "スキルレベルデータ" },
        { "_infiniteSkillPowerUnit", "無限スキル威力単位" },
        { "_infiniteSkillTenDaysUnit", "無限スキル10日能力単位" },
        { "subEffects", "付与パッシブID" },
        { "subVitalLayers", "付与追加HP ID" },
        { "canEraceEffectIDs", "除去可能パッシブID範囲" },
        { "CanEraceEffectCount", "除去可能パッシブ数" },
        { "canEraceVitalLayerIDs", "除去可能追加HP ID範囲" },
        { "CanEraceVitalLayerCount", "除去可能追加HP数" },
        { "ReactiveSkillPassiveList", "掛かってるパッシブ" },
        { "AggressiveSkillPassiveList", "装弾されたパッシブ" },
        { "TargetSelection", "パッシブ付与スキル選択方式" },
        { "ReactionCharaAndSkillList", "反応式対象リスト" },
        { "SkillPassiveEffectCount", "パッシブ付与上限数" },
        { "_skillPassiveGibeSkill_SkillFilter", "パッシブ付与対象フィルター" },
    };

    // ─── EditorPrefsキー生成 ───
    string SectionKey(int i) => "BSE_Sec" + i;

    bool GetSectionFoldout(int i) => EditorPrefs.GetBool(SectionKey(i), i < 3);
    void SetSectionFoldout(int i, bool v) => EditorPrefs.SetBool(SectionKey(i), v);

    // ─── バリデーション ───
    int ValidationWarningCount(SerializedProperty property)
    {
        int n = 0;
        var levelList = property.FindPropertyRelative("FixedSkillLevelData");
        if (levelList != null && levelList.arraySize == 0) n++;

        var zt = property.FindPropertyRelative("ZoneTrait");
        if (zt != null && zt.intValue == 0) n++;

        var name = property.FindPropertyRelative("SkillName");
        if (name != null && (string.IsNullOrEmpty(name.stringValue) || name.stringValue == "ここに名前を入れてください")) n++;

        var st = property.FindPropertyRelative("_baseSkillType");
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
        return n;
    }

    // ─── 高さ計算 ───
    float SummaryPanelHeight(SerializedProperty property)
    {
        // スキル名 + 必須設定ヘッダ + 必須3行 + 基本情報ヘッダ + 基本6行 + 命中(条件付き)
        float h = LINE + PAD; // "■ スキル概要"
        h += LINE + PAD; // スキル名
        h += SEPARATOR_H + PAD; // ━━ 必須設定 ━━
        h += (LINE + PAD) * 3; // 攻撃性質, 範囲性質, レベル数
        h += SEPARATOR_H + PAD; // ━━ 基本情報 ━━
        h += (LINE + PAD) * 5; // 連撃, 精神, 物理, コスト, 威力
        var hitPer = property.FindPropertyRelative("_skillHitPer");
        if (hitPer != null && hitPer.intValue != 0)
            h += LINE + PAD; // 命中補正
        h += PAD * 2; // パディング
        return h;
    }

    float SectionHeight(SerializedProperty property, int sectionIndex)
    {
        float h = LINE + PAD; // foldout header
        if (!GetSectionFoldout(sectionIndex)) return h;

        var fields = s_sectionFields[sectionIndex];
        foreach (var fieldName in fields)
        {
            var prop = property.FindPropertyRelative(fieldName);
            if (prop != null)
                h += EditorGUI.GetPropertyHeight(prop, true) + PAD;
        }

        // セクション⑧のスキルレベル空警告
        if (sectionIndex == 7)
        {
            var levelList = property.FindPropertyRelative("FixedSkillLevelData");
            if (levelList != null && levelList.arraySize == 0)
                h += HELPBOX_H + PAD;
        }

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

        // 9セクション
        for (int i = 0; i < SECTION_COUNT; i++)
            h += SectionHeight(property, i);

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

        // 9セクション
        for (int i = 0; i < SECTION_COUNT; i++)
            DrawSection(ref y, pos, property, i);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
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
        var zoneProp = property.FindPropertyRelative("ZoneTrait");
        if (zoneProp != null)
            zoneProp.intValue = (int)template.zoneTrait;

        var listProp = property.FindPropertyRelative("FixedSkillLevelData");
        if (listProp != null)
        {
            if (listProp.arraySize == 0)
                listProp.InsertArrayElementAtIndex(0);
            var firstEntry = listProp.GetArrayElementAtIndex(0);
            var powerProp = firstEntry.FindPropertyRelative("SkillPower");
            if (powerProp != null)
                powerProp.floatValue = template.skillPower;
        }
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

        // ■ スキル概要
        EditorGUI.LabelField(new Rect(px, py, pw, LINE), "\u25A0 スキル概要", EditorStyles.boldLabel);
        py += LINE + PAD;

        // スキル名
        var nameProp = property.FindPropertyRelative("SkillName");
        string skillName = nameProp != null ? nameProp.stringValue : "???";
        EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  スキル名:  " + skillName);
        py += LINE + PAD;

        // ━━ 必須設定 ━━
        EditorGUI.LabelField(new Rect(px, py, pw, SEPARATOR_H),
            "  \u2501\u2501 必須設定 \u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501",
            EditorStyles.miniLabel);
        py += SEPARATOR_H + PAD;

        // 攻撃性質
        var stProp = property.FindPropertyRelative("_baseSkillType");
        int stVal = stProp != null ? stProp.intValue : 0;
        DrawRequiredRow(ref py, px, pw, "攻撃性質", stVal != 0,
            stVal != 0 ? ((SkillType)stVal).ToString() : "未設定");

        // 範囲性質
        var ztProp = property.FindPropertyRelative("ZoneTrait");
        int ztVal = ztProp != null ? ztProp.intValue : 0;
        DrawRequiredRow(ref py, px, pw, "範囲性質", ztVal != 0,
            ztVal != 0 ? FormatZoneTrait((SkillZoneTrait)ztVal) : "未設定");

        // レベル数
        var levelList = property.FindPropertyRelative("FixedSkillLevelData");
        int levelCount = levelList != null ? levelList.arraySize : 0;
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

        var conProp = property.FindPropertyRelative("ConsecutiveType");
        EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  連撃性質:  " + (conProp != null ? ((SkillConsecutiveType)conProp.intValue).ToString() : "?"), style);
        py += LINE + PAD;

        var spirProp = property.FindPropertyRelative("SkillSpiritual");
        EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  精神属性:  " + (spirProp != null ? ((SpiritualProperty)spirProp.intValue).ToString() : "?"), style);
        py += LINE + PAD;

        var physProp = property.FindPropertyRelative("SkillPhysical");
        EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  物理属性:  " + (physProp != null ? ((PhysicalProperty)physProp.intValue).ToString() : "?"), style);
        py += LINE + PAD;

        var npProp = property.FindPropertyRelative("RequiredNormalP");
        EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  コスト:    NP: " + (npProp != null ? npProp.intValue.ToString() : "?"), style);
        py += LINE + PAD;

        if (levelCount > 0)
        {
            var firstLevel = levelList.GetArrayElementAtIndex(0);
            var powerProp = firstLevel.FindPropertyRelative("SkillPower");
            EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  威力:      Lv0 = " + (powerProp != null ? powerProp.floatValue.ToString("F1") : "?"), style);
        }
        else
        {
            EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  威力:      (レベルデータなし)", style);
        }
        py += LINE + PAD;

        var hitProp = property.FindPropertyRelative("_skillHitPer");
        if (hitProp != null && hitProp.intValue != 0)
        {
            int hitPer = hitProp.intValue;
            EditorGUI.LabelField(new Rect(px, py, pw, LINE), "  命中補正:  " + (hitPer > 0 ? "+" : "") + hitPer + "%", style);
            py += LINE + PAD;
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

        var ztProp = property.FindPropertyRelative("ZoneTrait");
        if (ztProp != null && ztProp.intValue == 0)
            DrawHelpBox(ref y, pos, "ZoneTraitが未設定です。スキルの範囲性質を設定してください。", MessageType.Error);

        var nameProp = property.FindPropertyRelative("SkillName");
        if (nameProp != null && (string.IsNullOrEmpty(nameProp.stringValue) || nameProp.stringValue == "ここに名前を入れてください"))
            DrawHelpBox(ref y, pos, "スキル名を設定してください。", MessageType.Warning);

        var stProp = property.FindPropertyRelative("_baseSkillType");
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

    // ─── セクション描画 ───
    void DrawSection(ref float y, Rect pos, SerializedProperty property, int sectionIndex)
    {
        var foldoutRect = new Rect(pos.x, y, pos.width, LINE);
        bool expanded = GetSectionFoldout(sectionIndex);
        bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, s_sectionLabels[sectionIndex], true, EditorStyles.foldoutHeader);
        if (newExpanded != expanded) SetSectionFoldout(sectionIndex, newExpanded);
        y += LINE + PAD;

        if (!newExpanded) return;

        EditorGUI.indentLevel++;

        // セクション⑧のスキルレベル空警告
        if (sectionIndex == 7)
        {
            var levelList = property.FindPropertyRelative("FixedSkillLevelData");
            if (levelList != null && levelList.arraySize == 0)
            {
                EditorGUI.HelpBox(new Rect(pos.x + EditorGUI.indentLevel * 15f, y, pos.width - EditorGUI.indentLevel * 15f, HELPBOX_H),
                    "スキルレベルデータが空です！最低1つ設定してください。", MessageType.Error);
                y += HELPBOX_H + PAD;
            }
        }

        var fields = s_sectionFields[sectionIndex];
        foreach (var fieldName in fields)
        {
            var prop = property.FindPropertyRelative(fieldName);
            if (prop == null) continue;

            float h = EditorGUI.GetPropertyHeight(prop, true);
            var rect = new Rect(pos.x, y, pos.width, h);
            string lbl = s_fieldLabels.TryGetValue(fieldName, out var l) ? l : prop.displayName;
            EditorGUI.PropertyField(rect, prop, new GUIContent(lbl), true);
            y += h + PAD;
        }

        EditorGUI.indentLevel--;
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
