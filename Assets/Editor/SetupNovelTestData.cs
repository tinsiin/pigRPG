using UnityEditor;
using UnityEngine;

/// <summary>
/// ノベルパートのテスト用データを一括作成するEditorスクリプト。
/// 実行後に削除してください。
/// </summary>
public static class SetupNovelTestData
{
    private const string OutputFolder = "Assets/Data/Novel";

    [MenuItem("Tools/Setup Novel Test Data")]
    public static void Execute()
    {
        // 1. DialogueSO 作成
        var dialogueSO = CreateDialogueSO();

        // 2. EventDefinitionSO 作成（NovelDialogueStep付き）
        var eventDefSO = CreateEventDefinitionSO(dialogueSO);

        // 3. SideObjectSO を更新
        UpdateSideObject(eventDefSO);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SetupNovelTestData] Complete!");
    }

    private static DialogueSO CreateDialogueSO()
    {
        var so = ScriptableObject.CreateInstance<DialogueSO>();

        // SerializedObjectで内部フィールドを設定
        var path = OutputFolder + "/TestDialogue_Novel.asset";
        AssetDatabase.CreateAsset(so, path);

        var serialized = new SerializedObject(so);
        serialized.FindProperty("dialogueId").stringValue = "test_novel_001";
        serialized.FindProperty("defaultMode").enumValueIndex = (int)DisplayMode.Portrait;
        serialized.FindProperty("zoomOnApproach").boolValue = false;

        // DialogueStep[] を構築
        var stepsProp = serialized.FindProperty("steps");
        stepsProp.arraySize = 3;

        // Step 0: 左にロックマン登場
        {
            var step = stepsProp.GetArrayElementAtIndex(0);
            step.FindPropertyRelative("speaker").stringValue = "ゲイノ";
            step.FindPropertyRelative("text").stringValue = "よう、久しぶりだな。\nちょっと話があるんだけど。";
            step.FindPropertyRelative("displayMode").enumValueIndex = (int)DisplayMode.Portrait;
            step.FindPropertyRelative("hasBackground").boolValue = false;

            // 左立ち絵: ロックマントランジション
            var leftPortrait = step.FindPropertyRelative("leftPortrait");
            leftPortrait.FindPropertyRelative("characterId").stringValue = "geino";
            leftPortrait.FindPropertyRelative("expression").stringValue = "normal";
            leftPortrait.FindPropertyRelative("transitionType").enumValueIndex = (int)PortraitTransition.Rockman;
        }

        // Step 1: 右にスライド登場 + 雑音テスト
        {
            var step = stepsProp.GetArrayElementAtIndex(1);
            step.FindPropertyRelative("speaker").stringValue = "サテライト";
            step.FindPropertyRelative("text").stringValue = "あら、わたしも呼ばれたの？\nなんの用かしら。";
            step.FindPropertyRelative("displayMode").enumValueIndex = (int)DisplayMode.Portrait;
            step.FindPropertyRelative("hasBackground").boolValue = false;

            // 左立ち絵を維持
            var leftPortrait = step.FindPropertyRelative("leftPortrait");
            leftPortrait.FindPropertyRelative("characterId").stringValue = "geino";
            leftPortrait.FindPropertyRelative("expression").stringValue = "normal";
            leftPortrait.FindPropertyRelative("transitionType").enumValueIndex = (int)PortraitTransition.None;

            // 右立ち絵: 上からスライドイン
            var rightPortrait = step.FindPropertyRelative("rightPortrait");
            rightPortrait.FindPropertyRelative("characterId").stringValue = "satelite";
            rightPortrait.FindPropertyRelative("expression").stringValue = "normal";
            rightPortrait.FindPropertyRelative("transitionType").enumValueIndex = (int)PortraitTransition.SlideTop;

            // 雑音: アイコン付き / 話者なし / DB未登録話者 の3パターン
            var noises = step.FindPropertyRelative("noises");
            noises.arraySize = 3;

            // 雑音0: geino（アイコン付き）
            var n0 = noises.GetArrayElementAtIndex(0);
            n0.FindPropertyRelative("speaker").stringValue = "geino";
            n0.FindPropertyRelative("text").stringValue = "おっ、来たな";
            n0.FindPropertyRelative("delaySeconds").floatValue = 0.2f;
            n0.FindPropertyRelative("speedMultiplier").floatValue = 1f;
            n0.FindPropertyRelative("verticalOffset").floatValue = 40f;

            // 雑音1: 話者なし（テキストのみ）
            var n1 = noises.GetArrayElementAtIndex(1);
            n1.FindPropertyRelative("speaker").stringValue = "";
            n1.FindPropertyRelative("text").stringValue = "ざわざわ…";
            n1.FindPropertyRelative("delaySeconds").floatValue = 0.5f;
            n1.FindPropertyRelative("speedMultiplier").floatValue = 1.2f;
            n1.FindPropertyRelative("verticalOffset").floatValue = -30f;

            // 雑音2: DB未登録話者（[話者名] テキスト フォールバック）
            var n2 = noises.GetArrayElementAtIndex(2);
            n2.FindPropertyRelative("speaker").stringValue = "通行人";
            n2.FindPropertyRelative("text").stringValue = "なにやってんだあいつら";
            n2.FindPropertyRelative("delaySeconds").floatValue = 0.8f;
            n2.FindPropertyRelative("speedMultiplier").floatValue = 0.9f;
            n2.FindPropertyRelative("verticalOffset").floatValue = 10f;
        }

        // Step 2: テキスト変更（立ち絵維持） + 表情連動付き雑音
        {
            var step = stepsProp.GetArrayElementAtIndex(2);
            step.FindPropertyRelative("speaker").stringValue = "ゲイノ";
            step.FindPropertyRelative("text").stringValue = "まぁいい、また今度な。\nじゃあな。";
            step.FindPropertyRelative("displayMode").enumValueIndex = (int)DisplayMode.Portrait;
            step.FindPropertyRelative("hasBackground").boolValue = false;

            var leftPortrait = step.FindPropertyRelative("leftPortrait");
            leftPortrait.FindPropertyRelative("characterId").stringValue = "geino";
            leftPortrait.FindPropertyRelative("expression").stringValue = "normal";
            leftPortrait.FindPropertyRelative("transitionType").enumValueIndex = (int)PortraitTransition.None;

            var rightPortrait = step.FindPropertyRelative("rightPortrait");
            rightPortrait.FindPropertyRelative("characterId").stringValue = "satelite";
            rightPortrait.FindPropertyRelative("expression").stringValue = "normal";
            rightPortrait.FindPropertyRelative("transitionType").enumValueIndex = (int)PortraitTransition.None;

            // 雑音: 表情連動テスト
            var noises = step.FindPropertyRelative("noises");
            noises.arraySize = 2;

            // 雑音0: satelite + 表情連動（surprised）
            var n0 = noises.GetArrayElementAtIndex(0);
            n0.FindPropertyRelative("speaker").stringValue = "satelite";
            n0.FindPropertyRelative("text").stringValue = "えっ、もう終わり？";
            n0.FindPropertyRelative("delaySeconds").floatValue = 0.3f;
            n0.FindPropertyRelative("speedMultiplier").floatValue = 1f;
            n0.FindPropertyRelative("verticalOffset").floatValue = 20f;
            n0.FindPropertyRelative("expression").stringValue = "surprised";

            // 雑音1: geino（アイコンのみ、表情連動なし）
            var n1 = noises.GetArrayElementAtIndex(1);
            n1.FindPropertyRelative("speaker").stringValue = "geino";
            n1.FindPropertyRelative("text").stringValue = "おつかれ";
            n1.FindPropertyRelative("delaySeconds").floatValue = 0.7f;
            n1.FindPropertyRelative("speedMultiplier").floatValue = 1.1f;
            n1.FindPropertyRelative("verticalOffset").floatValue = -20f;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(so);

        Debug.Log($"[SetupNovelTestData] Created DialogueSO: {path}");
        return so;
    }

    private static EventDefinitionSO CreateEventDefinitionSO(DialogueSO dialogueSO)
    {
        var so = ScriptableObject.CreateInstance<EventDefinitionSO>();
        var path = OutputFolder + "/TestEvent_Novel.asset";
        AssetDatabase.CreateAsset(so, path);

        var serialized = new SerializedObject(so);

        // NovelDialogueStep を SerializeReference で追加
        var stepsProp = serialized.FindProperty("steps");
        stepsProp.arraySize = 1;

        var stepElement = stepsProp.GetArrayElementAtIndex(0);
        // SerializeReference に NovelDialogueStep インスタンスを設定
        stepElement.managedReferenceValue = new NovelDialogueStep(dialogueSO);

        // NovelDialogueStep の詳細設定
        stepElement.FindPropertyRelative("displayMode").enumValueIndex = (int)DisplayMode.Portrait;
        stepElement.FindPropertyRelative("allowSkip").boolValue = true;
        stepElement.FindPropertyRelative("allowBacktrack").boolValue = false;
        stepElement.FindPropertyRelative("showBacklog").boolValue = false;
        stepElement.FindPropertyRelative("overrideZoom").boolValue = true;
        stepElement.FindPropertyRelative("zoomOnApproach").boolValue = false;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(so);

        Debug.Log($"[SetupNovelTestData] Created EventDefinitionSO: {path}");
        return so;
    }

    private static void UpdateSideObject(EventDefinitionSO eventDef)
    {
        // Side_L_Left_RadioToer を検索
        var guids = AssetDatabase.FindAssets("Side_L_Left_RadioToer t:SideObjectSO");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[SetupNovelTestData] Side_L_Left_RadioToer not found. Skipping SideObject update.");
            return;
        }

        var sideObjectPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        var sideObject = AssetDatabase.LoadAssetAtPath<SideObjectSO>(sideObjectPath);
        if (sideObject == null)
        {
            Debug.LogWarning("[SetupNovelTestData] Could not load SideObjectSO.");
            return;
        }

        var serialized = new SerializedObject(sideObject);

        // events 配列を設定
        var eventsProp = serialized.FindProperty("events");
        eventsProp.arraySize = 1;

        var entry = eventsProp.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("entryId").stringValue = "novel_test";
        entry.FindPropertyRelative("eventDefinition").objectReferenceValue = eventDef;
        entry.FindPropertyRelative("consumeOnTrigger").boolValue = false; // 何度でもテスト可能
        entry.FindPropertyRelative("cooldownSteps").intValue = 0;
        entry.FindPropertyRelative("maxTriggerCount").intValue = 0;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sideObject);

        Debug.Log($"[SetupNovelTestData] Updated SideObjectSO: {sideObjectPath}");
    }
}
