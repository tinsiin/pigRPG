using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 雑音（弾幕コメント風テキスト）の表示を制御するPresenter。
/// </summary>
public sealed class NoisePresenter : MonoBehaviour
{
    [Header("表示設定")]
    [SerializeField] private RectTransform noiseContainer;
    [SerializeField] private GameObject noisePrefab;

    [Header("再生設定")]
    [SerializeField] private float baseSpeed = 300f;
    [SerializeField] private float scatterRange = 50f;
    [SerializeField] private float accelerateMultiplier = 5f;
    [SerializeField] private float accelerateDuration = 0.3f;
    [SerializeField] private float baseY = 0f;
    [SerializeField] private float minVerticalGap = 42f;

    private readonly List<NoiseInstance> activeNoises = new();
    private readonly List<float> occupiedYsBuffer = new();
    private bool isAccelerated;
    private System.Action<NoiseEntry> onNoiseSpawned;
    private PortraitDatabase portraitDatabase;

    public int ActiveNoiseCount => activeNoises.Count;

    public void Initialize()
    {
        ClearAll();
    }

    public void SetPortraitDatabase(PortraitDatabase db)
    {
        portraitDatabase = db;
    }

    public void Play(NoiseEntry[] entries, System.Action<NoiseEntry> onSpawned = null)
    {
        if (entries == null || entries.Length == 0) return;

        isAccelerated = false;
        onNoiseSpawned = onSpawned;

        foreach (var entry in entries)
        {
            if (entry == null) continue;
            StartCoroutine(SpawnAfterDelay(entry));
        }
    }

    public void Accelerate()
    {
        isAccelerated = true;
        foreach (var noise in activeNoises)
        {
            noise.BaseSpeed = noise.Speed;
            noise.TargetSpeed = noise.Speed * accelerateMultiplier;
            noise.AccelElapsed = 0f;
            noise.IsAccelerating = true;
        }
    }

    public void ClearAll()
    {
        StopAllCoroutines();

        foreach (var noise in activeNoises)
        {
            if (noise.GameObject != null)
            {
                Destroy(noise.GameObject);
            }
        }

        activeNoises.Clear();
        isAccelerated = false;
    }

    private IEnumerator SpawnAfterDelay(NoiseEntry entry)
    {
        if (entry.DelaySeconds > 0)
        {
            yield return new WaitForSeconds(entry.DelaySeconds);
        }

        SpawnNoise(entry);
    }

    private void SpawnNoise(NoiseEntry entry)
    {
        if (noiseContainer == null) return;

        if (noisePrefab == null)
        {
            Debug.LogWarning("[NoisePresenter] noisePrefab is not assigned. Noise will not be displayed.");
            return;
        }

        var instance = Instantiate(noisePrefab, noiseContainer);
        var textComponent = instance.GetComponentInChildren<TMP_Text>();
        SetupIconInInstance(instance, entry);

        if (textComponent != null)
        {
            textComponent.text = FormatNoiseText(entry);
        }

        // 幅を先に計算（位置設定に必要）
        var rectTransform = instance.GetComponent<RectTransform>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        var textWidth = rectTransform.rect.width;
        if (textWidth <= 0f)
        {
            textWidth = textComponent != null ? textComponent.preferredWidth : 200f;
        }

        // 位置設定: テキストの右端がコンテナの右端に揃う位置から開始
        var containerWidth = noiseContainer.rect.width;
        var preferredY = baseY + entry.VerticalOffset + Random.Range(-scatterRange, scatterRange);
        var y = FindNonOverlappingY(preferredY);
        var startX = containerWidth / 2f - textWidth / 2f;
        rectTransform.anchoredPosition = new Vector2(startX, y);

        // 速度計算
        var speed = baseSpeed * entry.SpeedMultiplier;
        if (isAccelerated) speed *= accelerateMultiplier;

        var noise = new NoiseInstance
        {
            GameObject = instance,
            RectTransform = rectTransform,
            Speed = speed,
            Width = textWidth
        };

        activeNoises.Add(noise);

        // 雑音→立ち絵表情連動: スポーン時にコールバック発火
        onNoiseSpawned?.Invoke(entry);
    }

    private void SetupIconInInstance(GameObject instance, NoiseEntry entry)
    {
        var iconBgTransform = instance.transform.Find("IconBG");
        if (iconBgTransform == null) return;

        var iconTransform = iconBgTransform.Find("Icon");
        var iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;

        var iconSprite = GetIconForSpeaker(entry.Speaker);
        if (iconSprite != null && iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconBgTransform.gameObject.SetActive(true);
        }
        else
        {
            iconBgTransform.gameObject.SetActive(false);
        }
    }

    private Sprite GetIconForSpeaker(string speaker)
    {
        if (string.IsNullOrEmpty(speaker) || portraitDatabase == null) return null;
        return portraitDatabase.GetIcon(speaker);
    }

    private float FindNonOverlappingY(float preferredY)
    {
        if (activeNoises.Count == 0) return preferredY;

        // 既存雑音のY座標を収集（再利用バッファ）
        occupiedYsBuffer.Clear();
        foreach (var noise in activeNoises)
        {
            if (noise.RectTransform != null)
            {
                occupiedYsBuffer.Add(noise.RectTransform.anchoredPosition.y);
            }
        }

        if (occupiedYsBuffer.Count == 0) return preferredY;

        // preferredYで被らなければそのまま使う
        if (!IsOverlapping(preferredY, occupiedYsBuffer))
        {
            return preferredY;
        }

        // 上下に交互にずらして空きを探す（コンテナ高さの半分を上限とする）
        var maxOffset = noiseContainer != null ? noiseContainer.rect.height / 2f : 500f;
        for (var offset = minVerticalGap; offset < maxOffset; offset += minVerticalGap)
        {
            var upY = preferredY + offset;
            if (!IsOverlapping(upY, occupiedYsBuffer)) return upY;

            var downY = preferredY - offset;
            if (!IsOverlapping(downY, occupiedYsBuffer)) return downY;
        }

        // どうしても見つからなければそのまま（極端なケース）
        return preferredY;
    }

    private bool IsOverlapping(float y, List<float> occupiedYs)
    {
        foreach (var occupied in occupiedYs)
        {
            if (Mathf.Abs(y - occupied) < minVerticalGap)
            {
                return true;
            }
        }
        return false;
    }

    private string FormatNoiseText(NoiseEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Speaker))
        {
            return entry.Text;
        }

        // アイコンが表示できる場合はテキストのみ（アイコンが話者を表す）
        if (GetIconForSpeaker(entry.Speaker) != null)
        {
            return entry.Text;
        }

        // アイコンがない場合は従来通りテキストで話者名表示
        return $"[{entry.Speaker}] {entry.Text}";
    }

    private void Update()
    {
        if (noiseContainer == null) return;

        var containerWidth = noiseContainer.rect.width;
        var halfContainer = containerWidth / 2f;

        for (var i = activeNoises.Count - 1; i >= 0; i--)
        {
            var noise = activeNoises[i];
            if (noise.GameObject == null)
            {
                activeNoises.RemoveAt(i);
                continue;
            }

            // イージング加速（EaseIn: ゆっくり始まり加速していく）
            if (noise.IsAccelerating)
            {
                noise.AccelElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(noise.AccelElapsed / accelerateDuration);
                t *= t; // EaseIn Quad
                noise.Speed = Mathf.Lerp(noise.BaseSpeed, noise.TargetSpeed, t);
                if (t >= 1f) noise.IsAccelerating = false;
            }

            // 左に移動
            var pos = noise.RectTransform.anchoredPosition;
            pos.x -= noise.Speed * Time.deltaTime;
            noise.RectTransform.anchoredPosition = pos;

            // テキスト全体が左端を通過しきったら削除
            // （右端 = pos.x + width/2 が、コンテナ左端 = -halfContainer を超えたら）
            if (pos.x + noise.Width / 2f < -halfContainer)
            {
                Destroy(noise.GameObject);
                activeNoises.RemoveAt(i);
            }
        }
    }

    private void OnDisable()
    {
        ClearAll();
    }
}

/// <summary>
/// 雑音インスタンスの管理用クラス。
/// </summary>
public sealed class NoiseInstance
{
    public GameObject GameObject { get; set; }
    public RectTransform RectTransform { get; set; }
    public float Speed { get; set; }
    public float Width { get; set; }

    // イージング加速用
    public float BaseSpeed { get; set; }
    public float TargetSpeed { get; set; }
    public float AccelElapsed { get; set; }
    public bool IsAccelerating { get; set; }
}
