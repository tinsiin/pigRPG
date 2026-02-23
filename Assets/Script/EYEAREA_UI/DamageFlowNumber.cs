using UnityEngine;
using TMPro;
using LitMotion;
using Cysharp.Threading.Tasks;
using NRandom;

/// <summary>
/// フロー数字。ターゲットアイコン付近にポップアップし、
/// 上昇→各文字が穏やかに左右へ流れて消滅する。
/// BattleIconUIの子として生成され、自律的にアニメーション→自壊する。
/// </summary>
public class DamageFlowNumber : MonoBehaviour
{
    /// <summary>
    /// フロー数字のカテゴリ。テキスト構築・色を決定する。
    /// </summary>
    public enum Category { AttackDamage, RatherDamage, Heal }

    [SerializeField] private TextMeshProUGUI _text;

    [Header("アニメーション")]
    [SerializeField] private float _riseDistance = 30f;
    [SerializeField] private float _popDuration = 0.05f;
    [SerializeField] private float _riseDuration = 1f;
    [SerializeField] private float _scatterDuration = 0.5f;
    [SerializeField] private float _scatterSpreadX = 18f;
    [SerializeField] private float _scatterRandomY = 6f;

    [Header("アウトライン")]
    [SerializeField] private float _outlineWidth = 0.2f;
    [SerializeField] private Color _outlineColor = Color.white;

    // 攻撃ダメージ用ふち色
    private static readonly Color OutlineCrit  = new Color(1f, 0.42f, 0.42f, 1f);  // #ff6b6b クリティカル
    private static readonly Color OutlineGraze = new Color(0.67f, 0.73f, 0.80f, 1f); // #aabbcc かすり
    // 回復用文字色
    private static readonly Color ColorHeal = new Color(0.2f, 0.8f, 0.2f, 1f);

    private RectTransform _rect;
    // 散りフェーズ用: 各文字のランダムY方向オフセット（生成時に確定）
    private float[] _charRandomY;

    public void Play(int value, HitResult hitResult = HitResult.Hit, bool isDisturbed = false,
        Category category = Category.AttackDamage)
    {
        _rect = GetComponent<RectTransform>();
        if (_text == null) _text = GetComponentInChildren<TextMeshProUGUI>();
        // カテゴリ別テキスト構築・色設定
        Color textColor;
        Color outline;

        switch (category)
        {
            case Category.RatherDamage:
                _text.text = "\u2020" + value;
                textColor = Color.black;
                outline = _outlineColor;
                break;

            case Category.Heal:
                _text.text = value.ToString();
                textColor = ColorHeal;
                outline = _outlineColor;
                break;

            default: // AttackDamage
                string sym;
                switch (hitResult)
                {
                    case HitResult.Critical: sym = "!"; break;
                    case HitResult.Graze:    sym = "?"; break;
                    default:                 sym = "";  break;
                }
                if (isDisturbed) sym += "^";
                _text.text = value.ToString() + sym;
                textColor = Color.black;
                // ふち色優先度: Critical > Graze > 通常/乱れ
                switch (hitResult)
                {
                    case HitResult.Critical: outline = OutlineCrit;  break;
                    case HitResult.Graze:    outline = OutlineGraze; break;
                    default:                 outline = _outlineColor; break;
                }
                break;
        }

        _text.color = textColor;
        // マテリアルインスタンスを生成して共有マテリアル汚染を防止
        _text.fontMaterial = new Material(_text.fontMaterial);
        _text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, _outlineWidth);
        _text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        _text.ForceMeshUpdate();

        // 散りフェーズ用のランダムY値を事前計算
        int charCount = _text.textInfo.characterCount;
        _charRandomY = new float[charCount];
        for (int i = 0; i < charCount; i++)
            _charRandomY[i] = RandomEx.Shared.NextFloat(-_scatterRandomY, _scatterRandomY);

        RunAnimation().Forget();
    }

    private async UniTaskVoid RunAnimation()
    {
        var startPos = _rect.anchoredPosition;

        // フェーズ1: ポップ (scale 0.8→1.0)
        transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        await LMotion.Create(0.8f, 1f, _popDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(v => transform.localScale = new Vector3(v, v, 1f))
            .ToUniTask();
        if (this == null) return;

        // フェーズ2: 上昇 (easeOut)
        await LMotion.Create(startPos.y, startPos.y + _riseDistance, _riseDuration)
            .WithEase(Ease.OutCubic)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(y =>
            {
                if (_rect == null) return;
                _rect.anchoredPosition = new Vector2(startPos.x, y);
            })
            .ToUniTask();
        if (this == null) return;

        // フェーズ3: 散り — TMP頂点操作で各文字を左右に広げながらフェード
        var textInfo = _text.textInfo;
        int charCount = textInfo.characterCount;
        if (charCount == 0)
        {
            Destroy(gameObject);
            return;
        }

        // 元の頂点位置を保存
        var originalVertices = new Vector3[textInfo.meshInfo.Length][];
        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            originalVertices[m] = (Vector3[])textInfo.meshInfo[m].vertices.Clone();
        }

        float midIndex = (charCount - 1) / 2f;

        await LMotion.Create(0f, 1f, _scatterDuration)
            .WithEase(Ease.OutCubic)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(t =>
            {
                if (_text == null) return;
                for (int i = 0; i < charCount; i++)
                {
                    var charInfo = textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;

                    int matIdx = charInfo.materialReferenceIndex;
                    int vertIdx = charInfo.vertexIndex;
                    var verts = textInfo.meshInfo[matIdx].vertices;
                    var colors = textInfo.meshInfo[matIdx].colors32;
                    var origVerts = originalVertices[matIdx];

                    // X: 中央からの距離に比例して左右に広がる
                    float distFromCenter = i - midIndex;
                    float dx = distFromCenter * _scatterSpreadX * t;
                    // Y: 事前計算したランダム値で微小に揺れる
                    float dy = _charRandomY[i] * t;

                    for (int v = 0; v < 4; v++)
                    {
                        verts[vertIdx + v] = origVerts[vertIdx + v] + new Vector3(dx, dy, 0);
                    }

                    // Alpha フェード
                    byte a = (byte)(255 * (1f - t));
                    for (int v = 0; v < 4; v++)
                    {
                        var c = colors[vertIdx + v];
                        c.a = a;
                        colors[vertIdx + v] = c;
                    }
                }
                _text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
            })
            .ToUniTask();
        if (this == null) return;

        Destroy(gameObject);
    }
}
