using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 立ち絵の表示を制御するPresenter。
/// 左右2枚の立ち絵を管理し、各種トランジションに対応。
/// </summary>
public sealed class PortraitPresenter : MonoBehaviour
{
    [Header("立ち絵表示")]
    [SerializeField] private Image leftImage;
    [SerializeField] private Image rightImage;
    [SerializeField] private RectTransform leftTransform;
    [SerializeField] private RectTransform rightTransform;

    [Header("ロックマン演出用")]
    [SerializeField] private Image leftRainBar;
    [SerializeField] private Image rightRainBar;

    [Header("設定")]
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private float rainBarDuration = 0.35f;
    [SerializeField] private float exitSlideDistance = 500f;
    [SerializeField] private float slideOffset = 200f;

    private PortraitDatabase portraitDatabase;
    private PortraitState currentLeftState;
    private PortraitState currentRightState;
    private Vector2 leftOriginalPos;
    private Vector2 rightOriginalPos;
    private bool hasTemporaryLeft;
    private bool hasTemporaryRight;

    public PortraitState CurrentLeftState => currentLeftState;
    public PortraitState CurrentRightState => currentRightState;

    public void SetPortraitDatabase(PortraitDatabase db)
    {
        portraitDatabase = db;
    }

    public void Initialize()
    {
        // 元の位置を記録
        if (leftTransform != null) leftOriginalPos = leftTransform.anchoredPosition;
        if (rightTransform != null) rightOriginalPos = rightTransform.anchoredPosition;

        HideImmediate(PortraitPosition.Left);
        HideImmediate(PortraitPosition.Right);
    }

    public async UniTask Show(PortraitState left, PortraitState right)
    {
        var leftTask = UpdatePortrait(left, PortraitPosition.Left);
        var rightTask = UpdatePortrait(right, PortraitPosition.Right);
        await UniTask.WhenAll(leftTask, rightTask);
    }

    public async UniTask Hide(PortraitPosition position)
    {
        if (position == PortraitPosition.Left)
        {
            currentLeftState = null;
            await FadeOut(leftImage);
        }
        else
        {
            currentRightState = null;
            await FadeOut(rightImage);
        }
    }

    public async UniTask Exit(PortraitPosition position)
    {
        // 横にスライドアウト（捌ける）
        // 立ち絵が表示されていない場合は何もしない
        // （非アクティブなGameObjectでLitMotionを走らせるとハングする）
        if (position == PortraitPosition.Left)
        {
            if (currentLeftState == null) return;
            currentLeftState = null;
            await SlideOut(leftImage, leftTransform, -exitSlideDistance, leftOriginalPos);
        }
        else
        {
            if (currentRightState == null) return;
            currentRightState = null;
            await SlideOut(rightImage, rightTransform, exitSlideDistance, rightOriginalPos);
        }
    }

    public void HideImmediate(PortraitPosition position)
    {
        if (position == PortraitPosition.Left)
        {
            currentLeftState = null;
            if (leftImage != null) leftImage.gameObject.SetActive(false);
            if (leftRainBar != null) leftRainBar.gameObject.SetActive(false);
        }
        else
        {
            currentRightState = null;
            if (rightImage != null) rightImage.gameObject.SetActive(false);
            if (rightRainBar != null) rightRainBar.gameObject.SetActive(false);
        }
    }

    public void ClearAll()
    {
        HideImmediate(PortraitPosition.Left);
        HideImmediate(PortraitPosition.Right);
        hasTemporaryLeft = false;
        hasTemporaryRight = false;
    }

    /// <summary>
    /// 雑音連動: スプライトのみ一時的に差し替える（currentStateは変更しない）。
    /// 次のステップ開始時にClearTemporaryExpressionsで元に戻る。
    /// </summary>
    public void SetTemporaryExpression(PortraitPosition position, string expression)
    {
        var state = position == PortraitPosition.Left ? currentLeftState : currentRightState;
        if (state == null) return;

        var sprite = portraitDatabase?.GetPortrait(state.CharacterId, expression);
        if (sprite == null) return;

        var image = position == PortraitPosition.Left ? leftImage : rightImage;
        if (image != null) image.sprite = sprite;

        if (position == PortraitPosition.Left) hasTemporaryLeft = true;
        else hasTemporaryRight = true;
    }

    /// <summary>
    /// 一時表情をクリアし、currentStateの表情に戻す。
    /// ExecuteStep冒頭で呼ぶ。
    /// </summary>
    public void ClearTemporaryExpressions()
    {
        if (hasTemporaryLeft && currentLeftState != null)
        {
            var sprite = GetSpriteForState(currentLeftState);
            if (leftImage != null && sprite != null) leftImage.sprite = sprite;
            hasTemporaryLeft = false;
        }
        if (hasTemporaryRight && currentRightState != null)
        {
            var sprite = GetSpriteForState(currentRightState);
            if (rightImage != null && sprite != null) rightImage.sprite = sprite;
            hasTemporaryRight = false;
        }
    }

    private Sprite GetSpriteForState(PortraitState state)
    {
        return state.PortraitSprite ?? portraitDatabase?.GetPortrait(state.CharacterId, state.Expression);
    }

    /// <summary>
    /// 状態を即座に復元する（トランジションなし）。
    /// 戻る機能で使用。
    /// </summary>
    public void RestoreImmediate(PortraitState left, PortraitState right)
    {
        RestoreSingleImmediate(left, PortraitPosition.Left);
        RestoreSingleImmediate(right, PortraitPosition.Right);
    }

    private void RestoreSingleImmediate(PortraitState state, PortraitPosition position)
    {
        var image = position == PortraitPosition.Left ? leftImage : rightImage;
        var rectTransform = position == PortraitPosition.Left ? leftTransform : rightTransform;
        var originalPos = position == PortraitPosition.Left ? leftOriginalPos : rightOriginalPos;

        if (state == null)
        {
            HideImmediate(position);
            return;
        }

        // 状態更新
        if (position == PortraitPosition.Left)
        {
            currentLeftState = state;
        }
        else
        {
            currentRightState = state;
        }

        // スプライト取得
        var sprite = state.PortraitSprite;
        if (sprite == null && portraitDatabase != null)
        {
            sprite = portraitDatabase.GetPortrait(state.CharacterId, state.Expression);
        }

        if (image == null) return;

        image.sprite = sprite;
        image.gameObject.SetActive(true);
        var c = image.color;
        c.a = 1f;
        image.color = c;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = originalPos;
        }
    }

    private async UniTask UpdatePortrait(PortraitState state, PortraitPosition position)
    {
        var currentState = position == PortraitPosition.Left ? currentLeftState : currentRightState;
        var image = position == PortraitPosition.Left ? leftImage : rightImage;
        var rectTransform = position == PortraitPosition.Left ? leftTransform : rightTransform;

        // stateがnull → 非表示
        if (state == null)
        {
            if (currentState != null)
            {
                await Hide(position);
            }
            return;
        }

        // 同じキャラ・表情なら何もしない
        if (currentState != null &&
            currentState.CharacterId == state.CharacterId &&
            currentState.Expression == state.Expression)
        {
            return;
        }

        // 状態更新
        if (position == PortraitPosition.Left)
        {
            currentLeftState = state;
        }
        else
        {
            currentRightState = state;
        }

        // スプライト取得
        var sprite = state.PortraitSprite;
        if (sprite == null && portraitDatabase != null)
        {
            sprite = portraitDatabase.GetPortrait(state.CharacterId, state.Expression);
        }

        if (image == null) return;

        image.sprite = sprite;

        // トランジション実行
        await PlayTransition(state.TransitionType, position, image, rectTransform);
    }

    private async UniTask PlayTransition(PortraitTransition transition, PortraitPosition position, Image image, RectTransform rectTransform)
    {
        switch (transition)
        {
            case PortraitTransition.Rockman:
                await PlayRockmanTransition(position, image, rectTransform);
                break;
            case PortraitTransition.SlideTop:
                await PlaySlideFromTop(image, rectTransform, position);
                break;
            case PortraitTransition.SlideBottom:
                await PlaySlideFromBottom(image, rectTransform, position);
                break;
            default:
                await PlayFadeIn(image);
                break;
        }
    }

    private async UniTask PlayRockmanTransition(PortraitPosition position, Image image, RectTransform rectTransform)
    {
        var rainBar = position == PortraitPosition.Left ? leftRainBar : rightRainBar;
        var originalPos = position == PortraitPosition.Left ? leftOriginalPos : rightOriginalPos;

        if (rainBar != null)
        {
            var color = Color.white;
            if (portraitDatabase != null)
            {
                var state = position == PortraitPosition.Left ? currentLeftState : currentRightState;
                if (state != null)
                {
                    color = portraitDatabase.GetThemeColor(state.CharacterId);
                }
            }

            // rainBarを立ち絵の位置に合わせる
            var rainRect = rainBar.rectTransform;
            rainRect.anchoredPosition = new Vector2(originalPos.x, originalPos.y);

            rainBar.color = new Color(color.r, color.g, color.b, 0f);
            rainBar.gameObject.SetActive(true);

            var rainStartY = originalPos.y + 400f;
            var rainEndY = originalPos.y;

            var completed = false;

            // 雨バーが降りてくる + フェードイン
            LMotion.Create(0f, 1f, rainBarDuration)
                .WithEase(Ease.OutQuad)
                .WithOnComplete(() => completed = true)
                .Bind(t =>
                {
                    var c = rainBar.color;
                    c.a = t;
                    rainBar.color = c;

                    var pos = rainRect.anchoredPosition;
                    pos.y = Mathf.Lerp(rainStartY, rainEndY, t);
                    rainRect.anchoredPosition = pos;
                })
                .AddTo(rainBar.gameObject);

            await UniTask.WaitUntil(() => completed);

            // 雨バーをフェードアウト
            completed = false;
            LMotion.Create(1f, 0f, 0.15f)
                .WithOnComplete(() => completed = true)
                .Bind(a =>
                {
                    var c = rainBar.color;
                    c.a = a;
                    rainBar.color = c;
                })
                .AddTo(rainBar.gameObject);

            await UniTask.WaitUntil(() => completed);
            rainBar.gameObject.SetActive(false);
        }

        // 立ち絵を「下から生えてくるように」表示
        if (image == null || rectTransform == null)
        {
            await PlayFadeIn(image);
            return;
        }

        var growOffset = 80f;
        var startPos = new Vector2(originalPos.x, originalPos.y - growOffset);

        rectTransform.anchoredPosition = startPos;
        var c2 = image.color;
        c2.a = 0f;
        image.color = c2;
        image.gameObject.SetActive(true);

        var growCompleted = false;

        // 下から定位置へスライド
        LMotion.Create(startPos, originalPos, transitionDuration * 1.5f)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() => growCompleted = true)
            .BindToAnchoredPosition(rectTransform)
            .AddTo(image.gameObject);

        // フェードイン
        LMotion.Create(0f, 1f, transitionDuration * 1.5f)
            .WithEase(Ease.OutQuad)
            .Bind(a =>
            {
                var c = image.color;
                c.a = a;
                image.color = c;
            })
            .AddTo(image.gameObject);

        await UniTask.WaitUntil(() => growCompleted);
    }

    private async UniTask PlaySlideFromTop(Image image, RectTransform rectTransform, PortraitPosition position)
    {
        if (image == null || rectTransform == null) return;

        var originalPos = position == PortraitPosition.Left ? leftOriginalPos : rightOriginalPos;
        var startPos = new Vector2(originalPos.x, originalPos.y + slideOffset);

        rectTransform.anchoredPosition = startPos;
        var color = image.color;
        color.a = 0f;
        image.color = color;
        image.gameObject.SetActive(true);

        var completed = false;

        // 位置アニメーション
        LMotion.Create(startPos, originalPos, transitionDuration)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() => completed = true)
            .BindToAnchoredPosition(rectTransform)
            .AddTo(image.gameObject);

        // フェードイン
        LMotion.Create(0f, 1f, transitionDuration)
            .WithEase(Ease.OutQuad)
            .Bind(a =>
            {
                var c = image.color;
                c.a = a;
                image.color = c;
            })
            .AddTo(image.gameObject);

        await UniTask.WaitUntil(() => completed);
    }

    private async UniTask PlaySlideFromBottom(Image image, RectTransform rectTransform, PortraitPosition position)
    {
        if (image == null || rectTransform == null) return;

        var originalPos = position == PortraitPosition.Left ? leftOriginalPos : rightOriginalPos;
        var startPos = new Vector2(originalPos.x, originalPos.y - slideOffset);

        rectTransform.anchoredPosition = startPos;
        var color = image.color;
        color.a = 0f;
        image.color = color;
        image.gameObject.SetActive(true);

        var completed = false;

        // 位置アニメーション
        LMotion.Create(startPos, originalPos, transitionDuration)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() => completed = true)
            .BindToAnchoredPosition(rectTransform)
            .AddTo(image.gameObject);

        // フェードイン
        LMotion.Create(0f, 1f, transitionDuration)
            .WithEase(Ease.OutQuad)
            .Bind(a =>
            {
                var c = image.color;
                c.a = a;
                image.color = c;
            })
            .AddTo(image.gameObject);

        await UniTask.WaitUntil(() => completed);
    }

    private async UniTask PlayFadeIn(Image image)
    {
        if (image == null) return;

        var color = image.color;
        color.a = 0f;
        image.color = color;
        image.gameObject.SetActive(true);

        var completed = false;

        LMotion.Create(0f, 1f, transitionDuration)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() => completed = true)
            .Bind(a =>
            {
                var c = image.color;
                c.a = a;
                image.color = c;
            })
            .AddTo(image.gameObject);

        await UniTask.WaitUntil(() => completed);
    }

    private async UniTask FadeOut(Image image)
    {
        if (image == null || !image.gameObject.activeSelf) return;

        var startAlpha = image.color.a;
        var completed = false;

        LMotion.Create(startAlpha, 0f, transitionDuration)
            .WithEase(Ease.InQuad)
            .WithOnComplete(() => completed = true)
            .Bind(a =>
            {
                var c = image.color;
                c.a = a;
                image.color = c;
            })
            .AddTo(image.gameObject);

        await UniTask.WaitUntil(() => completed);

        image.gameObject.SetActive(false);
        var c2 = image.color;
        c2.a = 1f;
        image.color = c2;
    }

    private async UniTask SlideOut(Image image, RectTransform rectTransform, float distance, Vector2 originalPos)
    {
        if (image == null || rectTransform == null) return;

        var startPos = rectTransform.anchoredPosition;
        var endPos = new Vector2(startPos.x + distance, startPos.y);

        var completed = false;

        // スライドアウト
        LMotion.Create(startPos, endPos, transitionDuration)
            .WithEase(Ease.InQuad)
            .WithOnComplete(() => completed = true)
            .BindToAnchoredPosition(rectTransform)
            .AddTo(image.gameObject);

        // フェードアウト
        LMotion.Create(image.color.a, 0f, transitionDuration)
            .WithEase(Ease.InQuad)
            .Bind(a =>
            {
                var c = image.color;
                c.a = a;
                image.color = c;
            })
            .AddTo(image.gameObject);

        await UniTask.WaitUntil(() => completed);

        image.gameObject.SetActive(false);
        rectTransform.anchoredPosition = originalPos;
        var c2 = image.color;
        c2.a = 1f;
        image.color = c2;
    }
}
