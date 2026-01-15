using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class SideObjectPresenter
{
    private RectTransform root;
    private readonly GameObject[] currentObjects = new GameObject[2];
    private bool hasThemeColors;
    private Color themeColor;
    private Color twoColor;

    public void SetRoot(RectTransform nextRoot)
    {
        root = nextRoot;
    }

    public void SetThemeColors(Color main, Color sub)
    {
        hasThemeColors = true;
        themeColor = main;
        twoColor = sub;
        ApplyThemeColors(currentObjects[0]);
        ApplyThemeColors(currentObjects[1]);
    }

    public void Show(SideObjectEntry[] pair)
    {
        FadeOutCurrent();

        if (root == null || pair == null || pair.Length == 0)
        {
            return;
        }

        if (pair.Length > 0)
        {
            var leftPrefab = GetLeftPrefab(pair[0]);
            currentObjects[0] = InstantiatePrefab(leftPrefab);
        }

        if (pair.Length > 1)
        {
            var rightPrefab = GetRightPrefab(pair[1]);
            currentObjects[1] = InstantiatePrefab(rightPrefab);
        }
    }

    public void ClearImmediate()
    {
        ClearCurrentObjects();
        if (root == null) return;
        var markers = root.GetComponentsInChildren<WalkSpawnedMarker>(true);
        for (var i = 0; i < markers.Length; i++)
        {
            var marker = markers[i];
            if (marker == null) continue;
            DestroyObject(marker.gameObject);
        }
        var existing = root.GetComponentsInChildren<SideObjectMove>(true);
        for (var i = 0; i < existing.Length; i++)
        {
            var obj = existing[i];
            if (obj == null) continue;
            DestroyObject(obj.gameObject);
        }
    }

    private GameObject InstantiatePrefab(GameObject prefab)
    {
        if (prefab == null || root == null) return null;
        var instance = Object.Instantiate(prefab, root);
        if (instance.GetComponent<WalkSpawnedMarker>() == null)
        {
            instance.AddComponent<WalkSpawnedMarker>();
        }
        ApplyThemeColors(instance);
        var mover = instance.GetComponent<SideObjectMove>();
        if (mover != null && mover.boostSpeed <= 0f)
        {
            mover.boostSpeed = 3f;
        }
        return instance;
    }

    private static GameObject GetLeftPrefab(SideObjectEntry entry)
    {
        var obj = entry?.SideObject;
        if (obj == null) return null;
        return obj.PrefabLeft != null ? obj.PrefabLeft : obj.PrefabRight;
    }

    private static GameObject GetRightPrefab(SideObjectEntry entry)
    {
        var obj = entry?.SideObject;
        if (obj == null) return null;
        return obj.PrefabRight != null ? obj.PrefabRight : obj.PrefabLeft;
    }

    private void FadeOutCurrent()
    {
        for (var i = 0; i < currentObjects.Length; i++)
        {
            var obj = currentObjects[i];
            if (obj == null) continue;
            currentObjects[i] = null;
            var mover = obj.GetComponent<SideObjectMove>();
            if (mover != null)
            {
                mover.FadeOut().Forget();
                continue;
            }

            Object.Destroy(obj);
        }
    }

    private void ClearCurrentObjects()
    {
        for (var i = 0; i < currentObjects.Length; i++)
        {
            var obj = currentObjects[i];
            currentObjects[i] = null;
            if (obj == null) continue;
            DestroyObject(obj);
        }
    }

    private static void DestroyObject(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
        {
            Object.Destroy(obj);
        }
        else
        {
            Object.DestroyImmediate(obj);
        }
    }

    private void ApplyThemeColors(GameObject instance)
    {
        if (!hasThemeColors || instance == null) return;
        var lines = instance.GetComponentsInChildren<UILineRenderer>(true);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line == null) continue;
            line.lineColor = themeColor;
            line.two = twoColor;
            line.SetVerticesDirty();
        }
    }
}
