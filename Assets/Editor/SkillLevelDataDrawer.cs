using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// SkillLevelData の PropertyDrawer。
/// Phase 2: BaseSkillDrawerから移管された8セクションfoldout構造で
/// 各レベルエントリの全プロパティを表示する。
/// 差分ハイライト: 前レベルと異なるフィールドをゴールド背景で表示。
/// 一括コピー: フィールド右端の ▼ ボタンで全レベルに値をコピー。
/// </summary>
[CustomPropertyDrawer(typeof(SkillLevelData))]
public class SkillLevelDataDrawer : PropertyDrawer
{
    static readonly float LINE = EditorGUIUtility.singleLineHeight;
    const float PAD = 2f;
    const int SECTION_COUNT = 9;
    const float BTN_W = 20f;

    static readonly Color DIFF_COLOR = new Color(1f, 0.85f, 0.3f, 0.15f);

    // ─── セクション定義 ───
    static readonly string[] s_sectionLabels = new[]
    {
        "\u2460 基本情報",
        "\u2461 スキル性質",
        "\u2462 威力・命中・ダメージ",
        "\u2463 コスト・補正",
        "\u2464 連撃・ストック・トリガー",
        "\u2465 前のめり",
        "\u2466 ムーブセット",
        "\u2467 ビジュアルエフェクト",
        "\u2468 エフェクト・パッシブ付与",
    };

    static readonly string[][] s_sectionFields = new[]
    {
        // ① 基本情報
        new[] { "SkillName", "SkillSpiritual", "SkillPhysical", "Impression", "MotionFlavor", "SpecialFlags" },
        // ② スキル性質
        new[] { "BaseSkillType", "ConsecutiveType", "ZoneTrait", "DistributionType", "PowerRangePercentageDictionary", "HitRangePercentageDictionary" },
        // ③ 威力・命中・ダメージ
        new[] { "SkillPower", "TenDayValues", "SkillHitPer", "MentalDamageRatio", "DefAtk", "PowerSpread", "Cantkill" },
        // ④ コスト・補正
        new[] { "RequiredNormalP", "RequiredAttrP", "RequiredRemainingHPPercent", "EvasionModifier", "AttackModifier", "AttackMentalHealPercent", "SkillDidWaitCount" },
        // ⑤ 連撃・ストック・トリガー
        new[] { "RandomConsecutivePer", "DefaultStockCount", "StockPower", "StockForgetPower", "TriggerCountMax", "CanCancelTrigger", "TriggerRollBackCount" },
        // ⑥ 前のめり
        new[] { "AggressiveOnExecute", "AggressiveOnTrigger", "AggressiveOnStock" },
        // ⑦ ムーブセット
        new[] { "A_MoveSet", "B_MoveSet" },
        // ⑧ ビジュアルエフェクト
        new[] { "CasterEffectName", "TargetEffectName", "FieldEffectName" },
        // ⑨ エフェクト・パッシブ付与
        new[] { "SubEffects", "SubVitalLayers", "CanEraceEffectIDs", "CanEraceEffectCount", "CanEraceVitalLayerIDs", "CanEraceVitalLayerCount",
                "TargetSelection", "ReactionCharaAndSkillList", "SkillPassiveEffectCount", "SkillPassiveGibeSkillFilter" },
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
        { "BaseSkillType", "攻撃性質" },
        { "ConsecutiveType", "連撃性質" },
        { "ZoneTrait", "範囲性質" },
        { "DistributionType", "分散性質" },
        { "PowerRangePercentageDictionary", "威力の範囲別割合差分" },
        { "HitRangePercentageDictionary", "命中率の範囲別補正" },
        { "SkillPower", "スキル威力" },
        { "TenDayValues", "十日能力値" },
        { "SkillHitPer", "命中補正 (%)" },
        { "MentalDamageRatio", "精神攻撃率" },
        { "DefAtk", "防御無視率" },
        { "PowerSpread", "分散割合" },
        { "Cantkill", "殺せない (1残る)" },
        { "RequiredNormalP", "必要ノーマルP" },
        { "RequiredAttrP", "必要属性P内訳" },
        { "RequiredRemainingHPPercent", "必要残りHP割合" },
        { "EvasionModifier", "回避補正率" },
        { "AttackModifier", "攻撃補正率" },
        { "AttackMentalHealPercent", "攻撃時精神HP回復%" },
        { "SkillDidWaitCount", "追加硬直値" },
        { "RandomConsecutivePer", "ランダム連撃継続率 (%)" },
        { "DefaultStockCount", "デフォルトストック数" },
        { "StockPower", "ストック単位" },
        { "StockForgetPower", "ストック忘れ単位" },
        { "TriggerCountMax", "トリガー必要カウント数" },
        { "CanCancelTrigger", "トリガー中断可" },
        { "TriggerRollBackCount", "巻き戻りカウント" },
        { "AggressiveOnExecute", "前のめり設定（実行時）" },
        { "AggressiveOnTrigger", "前のめり設定（トリガー時）" },
        { "AggressiveOnStock", "前のめり設定（ストック時）" },
        { "A_MoveSet", "戦闘規格A ムーブセット" },
        { "B_MoveSet", "戦闘規格B ムーブセット" },
        { "CasterEffectName", "術者エフェクト名" },
        { "TargetEffectName", "対象エフェクト名" },
        { "FieldEffectName", "フィールドエフェクト名" },
        { "SubEffects", "付与パッシブID" },
        { "SubVitalLayers", "付与追加HP ID" },
        { "CanEraceEffectIDs", "除去可能パッシブID範囲" },
        { "CanEraceEffectCount", "除去可能パッシブ数" },
        { "CanEraceVitalLayerIDs", "除去可能追加HP ID範囲" },
        { "CanEraceVitalLayerCount", "除去可能追加HP数" },
        { "TargetSelection", "パッシブ付与スキル選択方式" },
        { "ReactionCharaAndSkillList", "反応式対象リスト" },
        { "SkillPassiveEffectCount", "パッシブ付与上限数" },
        { "SkillPassiveGibeSkillFilter", "パッシブ付与対象フィルター" },
    };

    // ─── EditorPrefsキー生成（propertyPathベースで各インスタンス独立） ───
    // PropertyDrawerはリスト要素間で同一インスタンスが再利用されるためキャッシュしない
    string GetPrefKeyBase(SerializedProperty property)
        => "SLDD_" + property.propertyPath.GetHashCode().ToString("X8") + "_";
    bool GetSectionFoldout(SerializedProperty property, int i)
        => EditorPrefs.GetBool(GetPrefKeyBase(property) + i, i < 3);
    void SetSectionFoldout(SerializedProperty property, int i, bool v)
        => EditorPrefs.SetBool(GetPrefKeyBase(property) + i, v);

    // ─── 配列インデックス抽出 ───
    static readonly Regex s_indexRegex = new Regex(@"\[(\d+)\]$", RegexOptions.Compiled);

    static int ExtractArrayIndex(string propertyPath)
    {
        var m = s_indexRegex.Match(propertyPath);
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }

    static SerializedProperty GetPreviousLevel(SerializedProperty property)
    {
        int idx = ExtractArrayIndex(property.propertyPath);
        if (idx <= 0) return null;
        string prevPath = s_indexRegex.Replace(property.propertyPath, $"[{idx - 1}]");
        return property.serializedObject.FindProperty(prevPath);
    }

    static SerializedProperty GetLevelArray(SerializedProperty property)
    {
        // "SkillList.Array.data[0].FixedSkillLevelData.Array.data[N]"
        // → 最後の ".Array.data[" を探して "...FixedSkillLevelData" を取得。
        // IndexOf だと外側の配列（SkillList等）にマッチしてしまう。
        string path = property.propertyPath;
        int arrayIdx = path.LastIndexOf(".Array.data[");
        if (arrayIdx < 0) return null;
        string arrayPath = path.Substring(0, arrayIdx);
        return property.serializedObject.FindProperty(arrayPath);
    }

    // ─── 高さ計算 ───
    float SectionHeight(SerializedProperty property, int sectionIndex)
    {
        float h = LINE + PAD; // foldoutヘッダー
        if (!GetSectionFoldout(property, sectionIndex)) return h;

        var fields = s_sectionFields[sectionIndex];
        foreach (var fieldName in fields)
        {
            var prop = property.FindPropertyRelative(fieldName);
            if (prop != null)
                h += EditorGUI.GetPropertyHeight(prop, true) + PAD;
        }
        return h;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return LINE;

        float h = LINE + PAD; // メインfoldout

        for (int i = 0; i < SECTION_COUNT; i++)
            h += SectionHeight(property, i);

        return h;
    }

    // ─── 描画 ───
    public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, property);
        float y = pos.y;

        // メインFoldout — スキル名をラベルに含める
        var nameProp = property.FindPropertyRelative("SkillName");
        string displayName = nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue)
            ? $"{label.text} [{nameProp.stringValue}]"
            : label.text;

        var foldoutRect = new Rect(pos.x, y, pos.width, LINE);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayName, true, EditorStyles.foldoutHeader);
        y += LINE + PAD;

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        // 前レベルとレベル配列を事前取得
        var prevLevel = GetPreviousLevel(property);
        var levelArray = GetLevelArray(property);
        int levelCount = levelArray != null ? levelArray.arraySize : 0;
        int myIndex = ExtractArrayIndex(property.propertyPath);

        EditorGUI.indentLevel++;

        for (int i = 0; i < SECTION_COUNT; i++)
            DrawSection(ref y, pos, property, i, prevLevel, levelArray, levelCount, myIndex);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    void DrawSection(ref float y, Rect pos, SerializedProperty property, int sectionIndex,
                     SerializedProperty prevLevel, SerializedProperty levelArray, int levelCount, int myIndex)
    {
        var foldoutRect = new Rect(pos.x, y, pos.width, LINE);
        bool expanded = GetSectionFoldout(property, sectionIndex);
        bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, s_sectionLabels[sectionIndex], true, EditorStyles.foldoutHeader);
        if (newExpanded != expanded) SetSectionFoldout(property, sectionIndex, newExpanded);
        y += LINE + PAD;

        if (!newExpanded) return;

        EditorGUI.indentLevel++;

        var fields = s_sectionFields[sectionIndex];
        foreach (var fieldName in fields)
        {
            var prop = property.FindPropertyRelative(fieldName);
            if (prop == null) continue;

            float h = EditorGUI.GetPropertyHeight(prop, true);
            var fullRect = new Rect(pos.x, y, pos.width, h);

            // ── 差分ハイライト ──
            if (prevLevel != null)
            {
                var prevProp = prevLevel.FindPropertyRelative(fieldName);
                if (prevProp != null && !ArePropertiesEqual(prop, prevProp))
                    EditorGUI.DrawRect(fullRect, DIFF_COLOR);
            }

            // ── フィールド描画（ボタン分だけ幅を縮小） ──
            bool showBtn = levelCount > 1;
            float fieldW = showBtn ? pos.width - BTN_W - 4f : pos.width;
            var fieldRect = new Rect(pos.x, y, fieldW, h);
            string lbl = s_fieldLabels.TryGetValue(fieldName, out var l) ? l : prop.displayName;

            EditorGUI.PropertyField(fieldRect, prop, new GUIContent(lbl), true);

            // ── 一括コピーボタン ──
            if (showBtn)
            {
                var btnRect = new Rect(pos.x + fieldW + 2f, y, BTN_W, LINE);
                var btnContent = new GUIContent("\u25BC", "この値を全レベルにコピー");
                if (GUI.Button(btnRect, btnContent, EditorStyles.miniButton))
                {
                    CopyFieldToAllLevels(prop, fieldName, levelArray, levelCount, myIndex);
                }
            }

            y += h + PAD;
        }

        EditorGUI.indentLevel--;
    }

    // ─── 全レベル一括コピー ───
    void CopyFieldToAllLevels(SerializedProperty srcProp, string fieldName,
                              SerializedProperty levelArray, int levelCount, int srcIndex)
    {
        // Undo.RecordObjectは使わない。ApplyModifiedPropertiesが内部でUndoを処理する。
        // 両方使うとUndoが競合し変更が巻き戻される場合がある。
        for (int i = 0; i < levelCount; i++)
        {
            if (i == srcIndex) continue;
            var dstLevel = levelArray.GetArrayElementAtIndex(i);
            var dstProp = dstLevel.FindPropertyRelative(fieldName);
            if (dstProp != null)
                CopyPropertyValue(srcProp, dstProp);
        }

        srcProp.serializedObject.ApplyModifiedProperties();

        // 次フレームでInspector全体を再描画し、差分ハイライトと表示値を更新する。
        // Unity内部のReorderableListと同じパターン (EditorApplication.delayCall)。
        EditorApplication.delayCall += () =>
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        };

        // 現在のGUIパスを中断して即時再レイアウトも促す
        GUIUtility.ExitGUI();
    }

    // ─── SerializedProperty 値コピー ───
    static void CopyPropertyValue(SerializedProperty src, SerializedProperty dst)
    {
        if (src.propertyType != dst.propertyType) return;

        switch (src.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.ArraySize:
                dst.intValue = src.intValue;
                break;
            case SerializedPropertyType.Boolean:
                dst.boolValue = src.boolValue;
                break;
            case SerializedPropertyType.Float:
                dst.floatValue = src.floatValue;
                break;
            case SerializedPropertyType.String:
                dst.stringValue = src.stringValue;
                break;
            case SerializedPropertyType.Enum:
                dst.enumValueIndex = src.enumValueIndex;
                break;
            case SerializedPropertyType.ObjectReference:
                dst.objectReferenceValue = src.objectReferenceValue;
                break;
            case SerializedPropertyType.Color:
                dst.colorValue = src.colorValue;
                break;
            case SerializedPropertyType.Vector2:
                dst.vector2Value = src.vector2Value;
                break;
            case SerializedPropertyType.Vector3:
                dst.vector3Value = src.vector3Value;
                break;
            case SerializedPropertyType.Vector4:
                dst.vector4Value = src.vector4Value;
                break;
            case SerializedPropertyType.Rect:
                dst.rectValue = src.rectValue;
                break;
            case SerializedPropertyType.AnimationCurve:
                dst.animationCurveValue = src.animationCurveValue;
                break;
            case SerializedPropertyType.Bounds:
                dst.boundsValue = src.boundsValue;
                break;
            case SerializedPropertyType.Vector2Int:
                dst.vector2IntValue = src.vector2IntValue;
                break;
            case SerializedPropertyType.Vector3Int:
                dst.vector3IntValue = src.vector3IntValue;
                break;
            case SerializedPropertyType.RectInt:
                dst.rectIntValue = src.rectIntValue;
                break;
            case SerializedPropertyType.BoundsInt:
                dst.boundsIntValue = src.boundsIntValue;
                break;
            default:
                // Generic（配列・構造体）: 子要素を再帰コピー
                if (src.isArray)
                {
                    dst.arraySize = src.arraySize;
                    for (int i = 0; i < src.arraySize; i++)
                        CopyPropertyValue(src.GetArrayElementAtIndex(i), dst.GetArrayElementAtIndex(i));
                }
                else
                {
                    CopyStructChildren(src, dst);
                }
                break;
        }
    }

    static void CopyStructChildren(SerializedProperty src, SerializedProperty dst)
    {
        var srcIter = src.Copy();
        var dstIter = dst.Copy();
        var endProp = src.GetEndProperty();

        bool srcEnter = srcIter.NextVisible(true);
        bool dstEnter = dstIter.NextVisible(true);

        while (srcEnter && dstEnter)
        {
            if (SerializedProperty.EqualContents(srcIter, endProp)) break;
            CopyPropertyValue(srcIter, dstIter);
            srcEnter = srcIter.NextVisible(false);
            dstEnter = dstIter.NextVisible(false);
        }
    }

    // ─── SerializedProperty 比較 ───
    static bool ArePropertiesEqual(SerializedProperty a, SerializedProperty b)
    {
        if (a.propertyType != b.propertyType) return false;

        switch (a.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.ArraySize:
                return a.intValue == b.intValue;
            case SerializedPropertyType.Boolean:
                return a.boolValue == b.boolValue;
            case SerializedPropertyType.Float:
                return Mathf.Approximately(a.floatValue, b.floatValue);
            case SerializedPropertyType.String:
                return a.stringValue == b.stringValue;
            case SerializedPropertyType.Enum:
                return a.enumValueIndex == b.enumValueIndex;
            case SerializedPropertyType.ObjectReference:
                return a.objectReferenceInstanceIDValue == b.objectReferenceInstanceIDValue;
            case SerializedPropertyType.Color:
                return a.colorValue == b.colorValue;
            case SerializedPropertyType.Vector2:
                return a.vector2Value == b.vector2Value;
            case SerializedPropertyType.Vector3:
                return a.vector3Value == b.vector3Value;
            case SerializedPropertyType.Vector4:
                return a.vector4Value == b.vector4Value;
            case SerializedPropertyType.Rect:
                return a.rectValue == b.rectValue;
            case SerializedPropertyType.AnimationCurve:
                return true; // AnimationCurveの比較は複雑なため省略
            case SerializedPropertyType.Bounds:
                return a.boundsValue == b.boundsValue;
            case SerializedPropertyType.Vector2Int:
                return a.vector2IntValue == b.vector2IntValue;
            case SerializedPropertyType.Vector3Int:
                return a.vector3IntValue == b.vector3IntValue;
            case SerializedPropertyType.RectInt:
                return a.rectIntValue == b.rectIntValue;
            case SerializedPropertyType.BoundsInt:
                return a.boundsIntValue == b.boundsIntValue;
            default:
                // Generic（配列・構造体）: 子要素を再帰比較
                if (a.isArray)
                {
                    if (a.arraySize != b.arraySize) return false;
                    for (int i = 0; i < a.arraySize; i++)
                        if (!ArePropertiesEqual(a.GetArrayElementAtIndex(i), b.GetArrayElementAtIndex(i)))
                            return false;
                    return true;
                }
                return AreStructChildrenEqual(a, b);
        }
    }

    static bool AreStructChildrenEqual(SerializedProperty a, SerializedProperty b)
    {
        var aIter = a.Copy();
        var bIter = b.Copy();
        var endProp = a.GetEndProperty();

        bool aEnter = aIter.NextVisible(true);
        bool bEnter = bIter.NextVisible(true);

        while (aEnter && bEnter)
        {
            if (SerializedProperty.EqualContents(aIter, endProp)) break;
            if (!ArePropertiesEqual(aIter, bIter)) return false;
            aEnter = aIter.NextVisible(false);
            bEnter = bIter.NextVisible(false);
        }
        return true;
    }
}
