using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using LitMotion;

/// <summary>
/// 単一オーブの見た目とサイズ/演出を管理。
/// </summary>
public class AttrOrbUI : MonoBehaviour
{
    RectTransform _rt;
    Image _img;

    MotionHandle _scaleHandle; // LitMotion ハンドル（スケールアニメ管理）

    public Color Color => _img != null ? _img.color : Color.white;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (_img == null) _img = GetComponent<Image>();
    }

    void OnDisable()
    {
        // ライフサイクルに伴うクリーンアップ
        if (_scaleHandle.IsActive()) _scaleHandle.Cancel();
    }

    public void SetImage(Image img)
    {
        _img = img;
    }

    public void SetColor(Color c)
    {
        if (_img != null) _img.color = c;
    }

    public void SetSize(float sizePx)
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        _rt.sizeDelta = new Vector2(sizePx, sizePx);
    }

    public void PlaySpawn(float duration)
    {
        // 0 -> 1 スケール（SmoothStepの見た目は維持、駆動のみLitMotionに）
        var tr = this.transform;
        tr.localScale = Vector3.zero;
        if (_scaleHandle.IsActive()) _scaleHandle.Cancel();

        float d = Mathf.Max(0.01f, duration);
        // u: 0..1 をリニアに補間し、出力は SmoothStep(from,to,u)
        _scaleHandle = LMotion.Create(0f, 1f, d)
            .WithEase(Ease.Linear)
            .Bind(u =>
            {
                float s = Mathf.SmoothStep(0f, 1f, u);
                tr.localScale = new Vector3(s, s, 1f);
            });
    }

    public async UniTask PlayDespawnAndDestroyAsync(float duration)
    {
        var tr = this.transform;
        if (_scaleHandle.IsActive()) _scaleHandle.Cancel();

        float d = Mathf.Max(0.01f, duration);
        _scaleHandle = LMotion.Create(0f, 1f, d)
            .WithEase(Ease.Linear)
            .Bind(u =>
            {
                // 1->0 をSmoothStepで生成
                float s = Mathf.SmoothStep(1f, 0f, u);
                tr.localScale = new Vector3(s, s, 1f);
            });

        // モーション完了を待つ（オブジェクト破棄でキャンセル）
        var ct = this.GetCancellationTokenOnDestroy();
        try
        {
            await UniTask.WaitUntil(() => !_scaleHandle.IsActive(), cancellationToken: ct);
        }
        catch (System.OperationCanceledException)
        {
            // 破棄に伴うキャンセルは無視して正常終了
            return;
        }

        if (this != null && this.gameObject != null)
        {
            GameObject.Destroy(this.gameObject);
        }
    }
}
