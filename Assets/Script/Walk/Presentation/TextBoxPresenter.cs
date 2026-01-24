using Cysharp.Threading.Tasks;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// テキストボックスの表示を制御するPresenter。
/// Dinoidモード（アイコン+テキスト）とPortraitモード（話者名+テキスト）を切り替える。
/// </summary>
public sealed class TextBoxPresenter : MonoBehaviour
{
    [Header("Dinoidモード")]
    [SerializeField] private GameObject dinoidTextBox;
    [SerializeField] private CanvasGroup dinoidCanvasGroup;
    [SerializeField] private Image dinoidIcon;
    [SerializeField] private TMP_Text dinoidText;

    [Header("Portraitモード")]
    [SerializeField] private GameObject portraitTextBox;
    [SerializeField] private CanvasGroup portraitCanvasGroup;
    [SerializeField] private TMP_Text portraitSpeakerName;
    [SerializeField] private TMP_Text portraitText;

    [Header("設定")]
    [SerializeField] private float switchDuration = 0.3f;
    [SerializeField] private float appearOffset = 50f;
    [SerializeField] private float appearRotation = 5f;

    private DisplayMode currentMode = DisplayMode.Dinoid;
    private PortraitDatabase portraitDatabase;
    private RectTransform dinoidRect;
    private RectTransform portraitRect;
    private Vector2 dinoidOriginalPos;
    private Vector2 portraitOriginalPos;

    public DisplayMode CurrentMode => currentMode;

    private void Awake()
    {
        // RectTransformを取得
        if (dinoidTextBox != null)
        {
            dinoidRect = dinoidTextBox.GetComponent<RectTransform>();
            dinoidOriginalPos = dinoidRect != null ? dinoidRect.anchoredPosition : Vector2.zero;
        }
        if (portraitTextBox != null)
        {
            portraitRect = portraitTextBox.GetComponent<RectTransform>();
            portraitOriginalPos = portraitRect != null ? portraitRect.anchoredPosition : Vector2.zero;
        }
    }

    public void SetPortraitDatabase(PortraitDatabase db)
    {
        portraitDatabase = db;
    }

    public void Initialize(DisplayMode initialMode)
    {
        currentMode = initialMode;
        UpdateVisibilityImmediate();
    }

    public async UniTask SwitchMode(DisplayMode mode)
    {
        if (currentMode == mode) return;

        var oldMode = currentMode;
        currentMode = mode;

        // 旧テキストボックスをフェードアウト
        if (oldMode == DisplayMode.Dinoid)
        {
            await FadeOutTextBox(dinoidTextBox, dinoidCanvasGroup, dinoidRect, dinoidOriginalPos);
        }
        else
        {
            await FadeOutTextBox(portraitTextBox, portraitCanvasGroup, portraitRect, portraitOriginalPos);
        }

        // 新テキストボックスをフェードイン（ダンロン風：右斜め下からシュッと出現）
        if (mode == DisplayMode.Dinoid)
        {
            await FadeInTextBox(dinoidTextBox, dinoidCanvasGroup, dinoidRect, dinoidOriginalPos);
        }
        else
        {
            await FadeInTextBox(portraitTextBox, portraitCanvasGroup, portraitRect, portraitOriginalPos);
        }
    }

    public void SetText(string speaker, string text)
    {
        if (currentMode == DisplayMode.Dinoid)
        {
            SetDinoidText(speaker, text);
        }
        else
        {
            SetPortraitText(speaker, text);
        }
    }

    /// <summary>
    /// リッチテキストを直接設定する（リアクションシステム用）。
    /// TMPタグがそのまま適用される。
    /// </summary>
    public void SetRichText(string richText)
    {
        if (currentMode == DisplayMode.Dinoid)
        {
            if (dinoidText != null)
            {
                dinoidText.text = richText;
            }
        }
        else
        {
            if (portraitText != null)
            {
                portraitText.text = richText;
            }
        }
    }

    /// <summary>
    /// 現在のモードで使用中のTMP_Textコンポーネントを取得する。
    /// ReactionTextHandlerがクリック検出に使用。
    /// </summary>
    public TMP_Text GetCurrentTextComponent()
    {
        return currentMode == DisplayMode.Dinoid ? dinoidText : portraitText;
    }

    public void Clear()
    {
        if (dinoidText != null) dinoidText.text = string.Empty;
        if (portraitText != null) portraitText.text = string.Empty;
        if (portraitSpeakerName != null) portraitSpeakerName.text = string.Empty;
        if (dinoidIcon != null) dinoidIcon.sprite = null;
    }

    public void Hide()
    {
        if (dinoidTextBox != null) dinoidTextBox.SetActive(false);
        if (portraitTextBox != null) portraitTextBox.SetActive(false);
    }

    public void Show()
    {
        UpdateVisibilityImmediate();
    }

    /// <summary>
    /// モードを即座に切り替える（トランジションなし）。
    /// 戻る機能で使用。
    /// </summary>
    public void SetModeImmediate(DisplayMode mode)
    {
        currentMode = mode;
        UpdateVisibilityImmediate();
    }

    private void SetDinoidText(string speaker, string text)
    {
        if (dinoidText != null)
        {
            dinoidText.text = text;
        }

        if (dinoidIcon != null && portraitDatabase != null && !string.IsNullOrEmpty(speaker))
        {
            var iconSprite = portraitDatabase.GetIcon(speaker);
            dinoidIcon.sprite = iconSprite;
            dinoidIcon.gameObject.SetActive(iconSprite != null);
        }
    }

    private void SetPortraitText(string speaker, string text)
    {
        if (portraitSpeakerName != null)
        {
            portraitSpeakerName.text = speaker ?? string.Empty;
        }

        if (portraitText != null)
        {
            portraitText.text = text;
        }
    }

    private void UpdateVisibilityImmediate()
    {
        var showDinoid = currentMode == DisplayMode.Dinoid;
        var showPortrait = currentMode == DisplayMode.Portrait;

        if (dinoidTextBox != null)
        {
            dinoidTextBox.SetActive(showDinoid);
            if (dinoidCanvasGroup != null) dinoidCanvasGroup.alpha = showDinoid ? 1f : 0f;
            if (dinoidRect != null)
            {
                dinoidRect.anchoredPosition = dinoidOriginalPos;
                dinoidRect.localEulerAngles = Vector3.zero;
            }
        }

        if (portraitTextBox != null)
        {
            portraitTextBox.SetActive(showPortrait);
            if (portraitCanvasGroup != null) portraitCanvasGroup.alpha = showPortrait ? 1f : 0f;
            if (portraitRect != null)
            {
                portraitRect.anchoredPosition = portraitOriginalPos;
                portraitRect.localEulerAngles = Vector3.zero;
            }
        }
    }

    private async UniTask FadeInTextBox(GameObject textBox, CanvasGroup canvasGroup, RectTransform rect, Vector2 originalPos)
    {
        if (textBox == null) return;

        // 右斜め下からの開始位置
        var startPos = new Vector2(originalPos.x + appearOffset, originalPos.y - appearOffset);
        var startRotation = appearRotation;

        if (rect != null)
        {
            rect.anchoredPosition = startPos;
            rect.localEulerAngles = new Vector3(0f, 0f, startRotation);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        textBox.SetActive(true);

        var completed = false;

        // 位置・回転・透明度を同時にアニメーション
        LMotion.Create(0f, 1f, switchDuration)
            .WithEase(Ease.OutBack)
            .WithOnComplete(() => completed = true)
            .Bind(t =>
            {
                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.Lerp(startPos, originalPos, t);
                    rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(startRotation, 0f, t));
                }
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = t;
                }
            })
            .AddTo(textBox);

        await UniTask.WaitUntil(() => completed);
    }

    private async UniTask FadeOutTextBox(GameObject textBox, CanvasGroup canvasGroup, RectTransform rect, Vector2 originalPos)
    {
        if (textBox == null || !textBox.activeSelf) return;

        var completed = false;

        // フェードアウト
        LMotion.Create(1f, 0f, switchDuration * 0.5f)
            .WithEase(Ease.InQuad)
            .WithOnComplete(() => completed = true)
            .Bind(a =>
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = a;
                }
            })
            .AddTo(textBox);

        await UniTask.WaitUntil(() => completed);

        textBox.SetActive(false);

        // 位置をリセット
        if (rect != null)
        {
            rect.anchoredPosition = originalPos;
            rect.localEulerAngles = Vector3.zero;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }
}
