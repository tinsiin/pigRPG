#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayersUIRefsMigration
{
    [MenuItem("Tools/Players/Migrate UI Refs")]
    public static void Migrate()
    {
        var playersStatesList = Object.FindObjectsOfType<PlayersStates>(true);
        if (playersStatesList == null || playersStatesList.Length == 0)
        {
            Debug.LogWarning("PlayersStates not found.");
            return;
        }

        foreach (var playersStates in playersStatesList)
        {
            if (playersStates == null) continue;

            var target = playersStates.GetComponent<PlayersUIRefs>();
            if (target == null)
            {
                target = playersStates.gameObject.AddComponent<PlayersUIRefs>();
                Undo.RecordObject(target, "Create PlayersUIRefs");
                EditorUtility.SetDirty(target);
                var scene = playersStates.gameObject.scene;
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                continue;
            }
        }

        Debug.Log("PlayersUIRefs migration completed. No legacy data to copy.");
    }
}
#endif
