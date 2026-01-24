using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 雑音（弾幕コメント風テキスト）の表示を制御するPresenter。
/// </summary>
public sealed class NoisePresenter : MonoBehaviour
{
    [Header("表示設定")]
    [SerializeField] private RectTransform noiseContainer;
    [SerializeField] private GameObject noisePrefab;

    [Header("再生設定")]
    [SerializeField] private float baseSpeed = 200f;
    [SerializeField] private float scatterRange = 50f;
    [SerializeField] private float accelerateMultiplier = 3f;
    [SerializeField] private float baseY = 0f;

    private readonly List<NoiseInstance> activeNoises = new();
    private bool isAccelerated;

    public int ActiveNoiseCount => activeNoises.Count;

    public void Initialize()
    {
        ClearAll();
    }

    public void Play(NoiseEntry[] entries)
    {
        if (entries == null || entries.Length == 0) return;

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
            noise.Speed *= accelerateMultiplier;
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

        GameObject instance;
        TMP_Text textComponent;

        if (noisePrefab != null)
        {
            instance = Instantiate(noisePrefab, noiseContainer);
            textComponent = instance.GetComponentInChildren<TMP_Text>();
        }
        else
        {
            // Prefabがない場合は動的生成
            instance = new GameObject("Noise", typeof(RectTransform), typeof(TextMeshProUGUI));
            instance.transform.SetParent(noiseContainer, false);
            textComponent = instance.GetComponent<TMP_Text>();
            textComponent.fontSize = 24f;
            textComponent.alignment = TextAlignmentOptions.Left;
        }

        if (textComponent != null)
        {
            textComponent.text = FormatNoiseText(entry);
        }

        // 位置設定
        var rectTransform = instance.GetComponent<RectTransform>();
        var containerWidth = noiseContainer.rect.width;
        var yOffset = entry.VerticalOffset + Random.Range(-scatterRange, scatterRange);
        rectTransform.anchoredPosition = new Vector2(containerWidth / 2f + 200f, baseY + yOffset);

        // 速度計算
        var speed = baseSpeed * entry.SpeedMultiplier;
        if (isAccelerated) speed *= accelerateMultiplier;

        // テキスト幅を計算
        var textWidth = 200f; // デフォルト幅
        if (textComponent != null)
        {
            textComponent.ForceMeshUpdate();
            textWidth = textComponent.preferredWidth;
        }

        var noise = new NoiseInstance
        {
            GameObject = instance,
            RectTransform = rectTransform,
            Speed = speed,
            Width = textWidth
        };

        activeNoises.Add(noise);
    }

    private static string FormatNoiseText(NoiseEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Speaker))
        {
            return entry.Text;
        }
        return $"[{entry.Speaker}] {entry.Text}";
    }

    private void Update()
    {
        if (noiseContainer == null) return;

        var containerWidth = noiseContainer.rect.width;
        var removeThreshold = -(containerWidth / 2f + 300f);

        for (var i = activeNoises.Count - 1; i >= 0; i--)
        {
            var noise = activeNoises[i];
            if (noise.GameObject == null)
            {
                activeNoises.RemoveAt(i);
                continue;
            }

            // 左に移動
            var pos = noise.RectTransform.anchoredPosition;
            pos.x -= noise.Speed * Time.deltaTime;
            noise.RectTransform.anchoredPosition = pos;

            // 画面外に出たら削除
            if (pos.x < removeThreshold)
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
}
