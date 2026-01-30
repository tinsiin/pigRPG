using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using RandomExtensions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 中央オブジェクトのアニメーション制御（SideObjectMoveと同じ構造）。
/// Start()でフェードイン、FadeOut()でフェードアウト→Destroy。
/// </summary>
public class CentralObjectMove : MonoBehaviour
{
    [Header("フェードイン")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeInStartScale = 0.8f;
    [SerializeField] private Ease fadeInEase = Ease.OutBack;

    [Header("フェードアウト（2段階移動、左右ランダム）")]
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float fadeOutMidX = 200f;
    [SerializeField] private float fadeOutMidY = 50f;
    [SerializeField] private float fadeOutEndX = 200f;
    [SerializeField] private float fadeOutEndY = 50f;
    [SerializeField] private Ease fadeOutEase = Ease.InQuad;

    private Image image;
    private RectTransform rect;
    private int fadeInEndCount = 0;
    private int direction; // 1 = 右, -1 = 左

    /// <summary>
    /// 外部から設定を注入する（Instantiate後、Start前に呼ぶ）。
    /// </summary>
    public void Configure(CentralAnimConfig config)
    {
        fadeInDuration = config.FadeInDuration;
        fadeInStartScale = config.FadeInStartScale;
        fadeInEase = config.FadeInEase;
        fadeOutDuration = config.FadeOutDuration;
        fadeOutMidX = config.FadeOutMidX;
        fadeOutMidY = config.FadeOutMidY;
        fadeOutEndX = config.FadeOutEndX;
        fadeOutEndY = config.FadeOutEndY;
        fadeOutEase = config.FadeOutEase;
    }

    private void Start()
    {
        image = GetComponent<Image>();
        rect = GetComponent<RectTransform>();

        if (image == null || rect == null) return;

        // 方向をランダムで決定（SideObjectMoveは左右固定、CentralObjectMoveはランダム）
        direction = RandomEx.Shared.NextBool() ? 1 : -1;

        // 初期状態：透明、小さく
        SetAlpha(0f);
        rect.localScale = Vector3.one * fadeInStartScale;

        // フェードインアニメーション（透明度）
        LMotion.Create(0f, 1f, fadeInDuration)
            .WithEase(fadeInEase)
            .WithOnComplete(() => fadeInEndCount++)
            .Bind(SetAlpha)
            .AddTo(this);

        // フェードインアニメーション（スケール）
        LMotion.Create(fadeInStartScale, 1f, fadeInDuration)
            .WithEase(fadeInEase)
            .WithOnComplete(() => fadeInEndCount++)
            .Bind(s => rect.localScale = Vector3.one * s)
            .AddTo(this);
    }

    /// <summary>
    /// フェードアウトして自身をDestroy。
    /// 中央オブジェクトは少しだけ右下/左下に動いて早めにフェードアウト。
    /// </summary>
    public async UniTask FadeOut()
    {
        // フェードインが終わるまで待つ（SideObjectMoveは5、こちらは2）
        await UniTask.WaitUntil(() => fadeInEndCount >= 2);

        if (rect == null)
        {
            Destroy(gameObject);
            return;
        }

        var startPos = rect.anchoredPosition;

        // 終了位置を計算（方向に応じて右下/左下へ少し移動）
        var endPos = startPos + new Vector2(
            (fadeOutMidX + fadeOutEndX) * direction,
            fadeOutMidY + fadeOutEndY
        );

        // 総移動時間
        var totalDuration = fadeOutDuration * 2f;

        var end = false;

        // 位置アニメーション（右下/左下へ）
        LMotion.Create(startPos, endPos, totalDuration)
            .WithEase(fadeOutEase)
            .WithOnComplete(() => end = true)
            .BindToAnchoredPosition(rect)
            .AddTo(this);

        // フェードアウト（透明度）- 最初から開始、移動より短い時間で完了
        LMotion.Create(1f, 0f, fadeOutDuration)
            .WithEase(fadeOutEase)
            .Bind(SetAlpha)
            .AddTo(this);

        await UniTask.WaitUntil(() => end);

        Destroy(gameObject);
    }

    private void SetAlpha(float alpha)
    {
        if (image == null) return;
        var c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
