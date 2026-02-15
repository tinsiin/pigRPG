using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SkillZoneTrait))]
public class SkillZoneTraitDrawer : PropertyDrawer
{
    // ─── 定数 ───
    static readonly float LINE = EditorGUIUtility.singleLineHeight;
    const float PAD = 2f;
    const float HELPBOX_H = 40f;
    const float INDENT = 16f;
    const int COLS = 2;

    // ─── トグル1個のデータ ───
    struct Entry
    {
        public SkillZoneTrait flag;
        public string label;   // 日本語
        public string tooltip; // 英語enum名
        public Entry(SkillZoneTrait f, string l, string t) { flag = f; label = l; tooltip = t; }
    }

    // ─── グループ定義 ───
    struct Group
    {
        public string label;
        public string prefsKey;
        public Entry[] entries;
    }

    static readonly Group[] s_groups = new[]
    {
        new Group
        {
            label = "対象モード",
            prefsKey = "ZTD_TargetMode",
            entries = new[]
            {
                new Entry(SkillZoneTrait.CanPerfectSelectSingleTarget, "完全選択単体",          "CanPerfectSelectSingleTarget"),
                new Entry(SkillZoneTrait.CanSelectSingleTarget,        "選択単体(前衛/後衛)",    "CanSelectSingleTarget"),
                new Entry(SkillZoneTrait.RandomSingleTarget,           "ランダム単体",           "RandomSingleTarget"),
                new Entry(SkillZoneTrait.ControlByThisSituation,       "状況制御(前のめり優先)", "ControlByThisSituation"),
                new Entry(SkillZoneTrait.CanSelectMultiTarget,         "選択範囲(前衛/後衛)",    "CanSelectMultiTarget"),
                new Entry(SkillZoneTrait.RandomSelectMultiTarget,      "ランダム範囲選択",       "RandomSelectMultiTarget"),
                new Entry(SkillZoneTrait.RandomMultiTarget,            "ランダム範囲",           "RandomMultiTarget"),
                new Entry(SkillZoneTrait.AllTarget,                    "全体",                   "AllTarget"),
            }
        },
        new Group
        {
            label = "ランダム分岐",
            prefsKey = "ZTD_RandomBranch",
            entries = new[]
            {
                new Entry(SkillZoneTrait.RandomRange,                  "ランダム分岐有効",       "RandomRange"),
                new Entry(SkillZoneTrait.RandomTargetALLSituation,     "全状況",                 "RandomTargetALLSituation"),
                new Entry(SkillZoneTrait.RandomTargetMultiOrSingle,    "範囲or単体",             "RandomTargetMultiOrSingle"),
                new Entry(SkillZoneTrait.RandomTargetALLorSingle,      "全体or単体",             "RandomTargetALLorSingle"),
                new Entry(SkillZoneTrait.RandomTargetALLorMulti,       "全体or範囲",             "RandomTargetALLorMulti"),
            }
        },
        new Group
        {
            label = "追加オプション",
            prefsKey = "ZTD_SubOptions",
            entries = new[]
            {
                new Entry(SkillZoneTrait.CanSelectAlly,    "対立+味方選択可", "CanSelectAlly"),
                new Entry(SkillZoneTrait.SelectOnlyAlly,   "味方のみ",        "SelectOnlyAlly"),
                new Entry(SkillZoneTrait.CanSelectDeath,   "死亡者選択可",    "CanSelectDeath"),
                new Entry(SkillZoneTrait.CanSelectMyself,  "自分選択可",      "CanSelectMyself"),
                new Entry(SkillZoneTrait.CanSelectRange,   "範囲選択可",      "CanSelectRange"),
            }
        },
        new Group
        {
            label = "特殊",
            prefsKey = "ZTD_Special",
            entries = new[]
            {
                new Entry(SkillZoneTrait.SelfSkill, "自分自身スキル", "SelfSkill"),
            }
        },
    };

    // ─── 事故フラグ判定 ───
    static readonly SkillZoneTrait AccidentFlags =
        SkillZoneTrait.RandomSingleTarget | SkillZoneTrait.AllTarget | SkillZoneTrait.RandomMultiTarget;

    static bool NeedsAccidentWarning(SkillZoneTrait v)
        => (v & SkillZoneTrait.ControlByThisSituation) != 0 && (v & AccidentFlags) == 0;

    // ─── 高さ計算 ───
    int RowsForEntries(int count) => (count + COLS - 1) / COLS;

    float GroupHeight(Group g)
    {
        float h = LINE + PAD; // foldout行
        if (EditorPrefs.GetBool(g.prefsKey, true))
            h += RowsForEntries(g.entries.Length) * (LINE + PAD);
        return h;
    }

    int WarningCount(SkillZoneTrait v)
    {
        int n = 0;
        if (v == 0) n++;
        if (NeedsAccidentWarning(v)) n++;
        if (!SkillZoneTraitNormalizer.Validate(v, out _)) n++;
        return n;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var v = (SkillZoneTrait)property.intValue;
        float h = LINE + PAD; // ラベル行
        foreach (var g in s_groups) h += GroupHeight(g);
        h += WarningCount(v) * (HELPBOX_H + PAD);
        return h;
    }

    // ─── 描画 ───
    public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, property);
        var value = (SkillZoneTrait)property.intValue;
        float y = pos.y;

        // ラベル
        EditorGUI.LabelField(R(ref y, pos, LINE), label, EditorStyles.boldLabel);

        // ゼロ警告（最上部）
        if (value == 0)
            DrawHelpBox(ref y, pos, "ZoneTraitが未設定です！設定してください", MessageType.Error);

        // グループ描画
        foreach (var g in s_groups)
            DrawGroup(ref y, pos, g, ref value);

        // バリデーション警告
        if (NeedsAccidentWarning(value))
            DrawHelpBox(ref y, pos, "状況制御(ControlByThisSituation)の事故用フラグ\n（ランダム単体/全体/ランダム範囲）を設定してください", MessageType.Warning);

        if (!SkillZoneTraitNormalizer.Validate(value, out var msg))
            DrawHelpBox(ref y, pos, msg, MessageType.Warning);

        // 値を書き戻し
        if ((SkillZoneTrait)property.intValue != value)
            property.intValue = (int)value;

        EditorGUI.EndProperty();
    }

    // ─── グループ1個描画 ───
    void DrawGroup(ref float y, Rect pos, Group g, ref SkillZoneTrait value)
    {
        // Foldout
        var foldout = EditorPrefs.GetBool(g.prefsKey, true);
        foldout = EditorGUI.Foldout(R(ref y, pos, LINE), foldout, g.label, true, EditorStyles.foldoutHeader);
        EditorPrefs.SetBool(g.prefsKey, foldout);

        if (!foldout) return;

        // 2列トグル
        float colW = (pos.width - INDENT) / COLS;
        for (int i = 0; i < g.entries.Length; i += COLS)
        {
            float rowY = y;
            for (int c = 0; c < COLS && i + c < g.entries.Length; c++)
            {
                var e = g.entries[i + c];
                bool has = (value & e.flag) != 0;
                var rect = new Rect(pos.x + INDENT + colW * c, rowY, colW, LINE);
                bool next = EditorGUI.ToggleLeft(rect, new GUIContent(e.label, e.tooltip), has);
                if (next != has)
                    value = next ? value | e.flag : value & ~e.flag;
            }
            y += LINE + PAD;
        }
    }

    // ─── ユーティリティ ───
    void DrawHelpBox(ref float y, Rect pos, string msg, MessageType type)
    {
        EditorGUI.HelpBox(new Rect(pos.x, y, pos.width, HELPBOX_H), msg, type);
        y += HELPBOX_H + PAD;
    }

    Rect R(ref float y, Rect pos, float h)
    {
        var r = new Rect(pos.x, y, pos.width, h);
        y += h + PAD;
        return r;
    }
}
