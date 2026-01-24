using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背景の表示を制御するPresenter。
/// フェードイン/アウト、スライドイン対応。
/// </summary>
public sealed class BackgroundPresenter : MonoBehaviour
{
    [Header("背景表示")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform backgroundTransform;

    [Header("設定")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float slideDistance = 1920f;

    private BackgroundDatabase backgroundDatabase;
    private string currentBackgroundId;
    private Vector2 originalPosition;

    public bool IsVisible => backgroundImage != null && backgroundImage.gameObject.activeSelf;
    public string CurrentBackgroundId => currentBackgroundId;

    public void SetBackgroundDatabase(BackgroundDatabase db)
    {
        backgroundDatabase = db;
    }

    public void Initialize()
    {
        if (backgroundTransform != null)
        {
            originalPosition = backgroundTransform.anchoredPosition;
        }
        HideImmediate();
    }

    public async UniTask Show(string backgroundId)
    {
        if (string.IsNullOrEmpty(backgroundId))
        {
            await Hide();
            return;
        }

        if (currentBackgroundId == backgroundId) return;

        currentBackgroundId = backgroundId;

        var sprite = GetSprite(backgroundId);
        var tint = GetTint(backgroundId);

        if (backgroundImage == null) return;

        backgroundImage.sprite = sprite;
        backgroundImage.color = new Color(tint.r, tint.g, tint.b, 0f);
        backgroundImage.gameObject.SetActive(true);

        await FadeIn(tint);
    }

    public async UniTask SlideIn(string backgroundId)
    {
        if (string.IsNullOrEmpty(backgroundId))
        {
            await Hide();
            return;
        }

        if (currentBackgroundId == backgroundId) return;

        currentBackgroundId = backgroundId;

        var sprite = GetSprite(backgroundId);
        var tint = GetTint(backgroundId);

        if (backgroundImage == null || backgroundTransform == null) return;

        backgroundImage.sprite = sprite;
        backgroundImage.color = tint;

        var startPos = new Vector2(originalPosition.x + slideDistance, originalPosition.y);
        backgroundTransform.anchoredPosition = startPos;
        backgroundImage.gameObject.SetActive(true);

        var completed = false;

        LMotion.Create(startPos, originalPosition, fadeDuration)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() => completed = true)
            .BindToAnchoredPosition(backgroundTransform)
            .AddTo(backgroundImage.gameObject);

        await UniTask.WaitUntil(() => completed);
    }

    public async UniTask Hide()
    {
        if (backgroundImage == null || !backgroundImage.gameObject.activeSelf) return;

        currentBackgroundId = null;
        await FadeOut();
    }

    public void HideImmediate()
    {
        currentBackgroundId = null;
        if (backgroundImage != null)
        {
            backgroundImage.gameObject.SetActive(false);
        }
        if (backgroundTransform != null)
        {
            backgroundTransform.anchoredPosition = originalPosition;
        }
    }

    /// <summary>
    /// 背景を即座に表示する（トランジションなし）。
    /// 戻る機能で使用。
    /// </summary>
    public void ShowImmediate(string backgroundId)
    {
        if (string.IsNullOrEmpty(backgroundId))
        {
            HideImmediate();
            return;
        }

        currentBackgroundId = backgroundId;

        var sprite = GetSprite(backgroundId);
        var tint = GetTint(backgroundId);

        if (backgroundImage == null) return;

        backgroundImage.sprite = sprite;
        backgroundImage.color = tint;
        backgroundImage.gameObject.SetActive(true);

        if (backgroundTransform != null)
        {
            backgroundTransform.anchoredPosition = originalPosition;
        }
    }

    private Sprite GetSprite(string backgroundId)
    {
        if (backgroundDatabase != null)
        {
            return backgroundDatabase.GetBackground(backgroundId);
        }
        return null;
    }

    private Color GetTint(string backgroundId)
    {
        if (backgroundDatabase != null)
        {
            var data = backgroundDatabase.GetBackgroundData(backgroundId);
            if (data != null) return data.Tint;
        }
        return Color.white;
    }

    private async UniTask FadeIn(Color targetColor)
    {
        if (backgroundImage == null) return;

        var completed = false;

        LMotion.Create(0f, 1f, fadeDuration)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() => completed = true)
            .Bind(a =>
            {
                var c = targetColor;
                c.a = a;
                backgroundImage.color = c;
            })
            .AddTo(backgroundImage.gameObject);

        await UniTask.WaitUntil(() => completed);
    }

    private async UniTask FadeOut()
    {
        if (backgroundImage == null) return;

        var startAlpha = backgroundImage.color.a;
        var completed = false;

        LMotion.Create(startAlpha, 0f, fadeDuration)
            .WithEase(Ease.InQuad)
            .WithOnComplete(() => completed = true)
            .Bind(a =>
            {
                var c = backgroundImage.color;
                c.a = a;
                backgroundImage.color = c;
            })
            .AddTo(backgroundImage.gameObject);

        await UniTask.WaitUntil(() => completed);

        backgroundImage.gameObject.SetActive(false);
        if (backgroundTransform != null)
        {
            backgroundTransform.anchoredPosition = originalPosition;
        }
    }
}
