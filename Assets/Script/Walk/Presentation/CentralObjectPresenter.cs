using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
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

    private Image backImage;
    private TMP_Text labelText;
    private CanvasGroup canvasGroup;
    private Button button;
    private CentralDisplayMode currentMode;
    private IWalkSFXPlayer sfxPlayer;

    public void SetRoot(RectTransform nextRoot)
    {
        root = nextRoot;
    }

    public void SetSFXPlayer(IWalkSFXPlayer player)
    {
        sfxPlayer = player;
    }

    /// <summary>
    /// 現在表示中の中央オブジェクトのRectTransformを取得する。
    /// ズームシステムで使用。
    /// </summary>
    public RectTransform GetCurrentRectTransform()
    {
        if (viewObject == null || !viewObject.activeSelf)
        {
            return null;
        }
        return rectTransform;
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
        if (backImage != null)
        {
            backImage.gameObject.SetActive(false);
        }
        if (labelText != null)
        {
            labelText.gameObject.SetActive(false);
        }
        currentMode = CentralDisplayMode.Hidden;
    }

    public void ShowGate(GateVisual visual)
    {
        EnsureViewObject();
        if (image == null || rectTransform == null) return;

        currentMode = CentralDisplayMode.Visible;

        // Play SFX on appear
        sfxPlayer?.Play(visual.SfxOnAppear);

        image.sprite = visual.HasSprite ? visual.Sprite : GetFallbackSprite();
        image.color = visual.Tint;

        var size = visual.Size;
        if (size.x <= 0f || size.y <= 0f)
        {
            size = new Vector2(160f, 160f);
        }

        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = visual.Offset;

        if (visual.HasBackSprite)
        {
            EnsureBackImage();
            backImage.sprite = visual.BackSprite;
            backImage.color = visual.BackTint;

            // Set back image size and position
            var backRect = backImage.rectTransform;
            var backSize = visual.BackSize;
            if (backSize.x <= 0f || backSize.y <= 0f)
            {
                backSize = size; // Fallback: same size as main image
            }
            backRect.sizeDelta = backSize;
            backRect.anchoredPosition = visual.BackOffset;
        }
        else if (backImage != null)
        {
            backImage.gameObject.SetActive(false);
        }

        if (!string.IsNullOrEmpty(visual.Label))
        {
            EnsureLabelText();
            labelText.text = visual.Label;
            labelText.gameObject.SetActive(true);
        }
        else if (labelText != null)
        {
            labelText.gameObject.SetActive(false);
        }

        // 常にボタン有効化（アプローチ/スルー選択可能）
        EnsureButton();
        image.raycastTarget = true;

        viewObject.SetActive(true);
    }

    public void ShowExit(ExitVisual visual, bool allGatesCleared)
    {
        if (!allGatesCleared) return;

        // Play exit-specific SFX
        sfxPlayer?.Play(visual.SfxOnAppear);

        ShowGate(visual.ToGateVisual());
    }

    public void PlayGatePassSFX(GateVisual visual)
    {
        sfxPlayer?.Play(visual.SfxOnPass);
    }

    public void PlayGateFailSFX(GateVisual visual)
    {
        sfxPlayer?.Play(visual.SfxOnFail);
    }

    public async UniTask<CentralInteractionResult> WaitForInteraction(IWalkInputProvider walkInput, CancellationToken ct = default)
    {
        if (currentMode == CentralDisplayMode.Hidden)
            return CentralInteractionResult.Skipped;

        EnsureButton();

        var clickTask = button.OnClickAsync(ct);
        var walkTask = walkInput.WaitForWalkButtonAsync(ct);

        var winIndex = await UniTask.WhenAny(clickTask, walkTask);

        return winIndex == 0
            ? CentralInteractionResult.Approached
            : CentralInteractionResult.Skipped;
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

        // Also destroy gate-related objects (BackImage, Label)
        if (backImage != null)
        {
            DestroyObject(backImage.gameObject);
            backImage = null;
        }
        if (labelText != null)
        {
            DestroyObject(labelText.gameObject);
            labelText = null;
        }

        button = null;
        canvasGroup = null;
        currentMode = CentralDisplayMode.Hidden;

        if (root == null) return;

        // Only destroy objects with WalkSpawnedMarker to avoid deleting unrelated UI elements
        var markers = root.GetComponentsInChildren<WalkSpawnedMarker>(true);
        for (var i = 0; i < markers.Length; i++)
        {
            var marker = markers[i];
            if (marker == null) continue;
            DestroyObject(marker.gameObject);
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

    private void EnsureBackImage()
    {
        if (backImage != null)
        {
            backImage.gameObject.SetActive(true);
            return;
        }
        if (root == null) return;

        var backObj = new GameObject("BackImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var backRect = backObj.GetComponent<RectTransform>();
        backRect.SetParent(root, false);
        backRect.anchorMin = new Vector2(0.5f, 0.5f);
        backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.SetSiblingIndex(0);

        backImage = backObj.GetComponent<Image>();
        backImage.raycastTarget = false;
        backObj.AddComponent<WalkSpawnedMarker>();
    }

    private void EnsureLabelText()
    {
        if (labelText != null)
        {
            labelText.gameObject.SetActive(true);
            return;
        }
        if (root == null) return;

        var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.SetParent(root, false);
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, -100f);
        labelRect.sizeDelta = new Vector2(300f, 50f);

        labelText = labelObj.GetComponent<TMP_Text>();
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 24f;
        labelText.color = Color.white;
        labelObj.AddComponent<WalkSpawnedMarker>();
    }

    private void EnsureButton()
    {
        if (button != null) return;
        if (viewObject == null) return;

        button = viewObject.GetComponent<Button>();
        if (button == null)
        {
            button = viewObject.AddComponent<Button>();
        }
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
