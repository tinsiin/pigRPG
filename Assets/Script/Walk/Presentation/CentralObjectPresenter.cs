using UnityEngine;
using UnityEngine.UI;

public sealed class CentralObjectPresenter
{
    private RectTransform root;
    private GameObject viewObject;
    private Image image;
    private RectTransform rectTransform;
    private Sprite fallbackSprite;
    private Texture2D fallbackTexture;

    public void SetRoot(RectTransform nextRoot)
    {
        root = nextRoot;
    }

    public void Show(CentralObjectVisual visual, bool forceShow)
    {
        if (!forceShow)
        {
            Hide();
            return;
        }

        EnsureViewObject();
        if (image == null || rectTransform == null) return;

        image.sprite = visual.HasSprite ? visual.Sprite : GetFallbackSprite();
        var tint = visual.Tint;
        if (tint.a <= 0f) tint = Color.white;
        image.color = tint;

        var size = visual.Size;
        if (size.x <= 0f || size.y <= 0f)
        {
            size = new Vector2(160f, 160f);
        }

        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = visual.Offset;

        viewObject.SetActive(true);
    }

    public void Hide()
    {
        if (viewObject != null)
        {
            viewObject.SetActive(false);
        }
    }

    public void ClearImmediate()
    {
        if (viewObject != null)
        {
            DestroyObject(viewObject);
            viewObject = null;
            image = null;
            rectTransform = null;
        }
        if (root == null) return;
        var rects = root.GetComponentsInChildren<RectTransform>(true);
        for (var i = 0; i < rects.Length; i++)
        {
            var rt = rects[i];
            if (rt == null) continue;
            if (rt.name != "CentralObject") continue;
            DestroyObject(rt.gameObject);
        }
    }

    private void EnsureViewObject()
    {
        if (root == null) return;
        if (viewObject != null) return;

        viewObject = new GameObject("CentralObject", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rectTransform = viewObject.GetComponent<RectTransform>();
        rectTransform.SetParent(root, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        image = viewObject.GetComponent<Image>();
        image.raycastTarget = false;
        viewObject.AddComponent<WalkSpawnedMarker>();
    }

    private Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null) return fallbackSprite;
        fallbackTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        fallbackTexture.SetPixel(0, 0, Color.white);
        fallbackTexture.Apply();
        fallbackSprite = Sprite.Create(
            fallbackTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        return fallbackSprite;
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
}
