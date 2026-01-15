#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class WalkPlayModeCleanup
{
    static WalkPlayModeCleanup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;
        Cleanup();
    }

    private static void Cleanup()
    {
        var markers = Resources.FindObjectsOfTypeAll<WalkSpawnedMarker>();
        for (var i = 0; i < markers.Length; i++)
        {
            var marker = markers[i];
            if (marker == null) continue;
            if (EditorUtility.IsPersistent(marker)) continue;
            if (!marker.gameObject.scene.IsValid()) continue;
            Object.DestroyImmediate(marker.gameObject);
        }

        var sideObjects = Resources.FindObjectsOfTypeAll<SideObjectMove>();
        for (var i = 0; i < sideObjects.Length; i++)
        {
            var obj = sideObjects[i];
            if (obj == null) continue;
            if (EditorUtility.IsPersistent(obj)) continue;
            if (!obj.gameObject.scene.IsValid()) continue;
            Object.DestroyImmediate(obj.gameObject);
        }

        var rects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (var i = 0; i < rects.Length; i++)
        {
            var rt = rects[i];
            if (rt == null) continue;
            if (rt.name != "CentralObject") continue;
            if (EditorUtility.IsPersistent(rt)) continue;
            if (!rt.gameObject.scene.IsValid()) continue;
            Object.DestroyImmediate(rt.gameObject);
        }
    }
}
#endif
