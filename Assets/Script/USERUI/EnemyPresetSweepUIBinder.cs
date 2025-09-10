using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class EnemyPresetSweepUIBinder : MonoBehaviour
{
    [Header("参照（未指定なら自動探索）")]
    [SerializeField] private WatchUIUpdate watch;
    [SerializeField] private Button runButton;
    [SerializeField] private TMP_Text statusText;

    [Header("挙動")]
    [Tooltip("enemyPresetConfig が未割り当て/空のときはボタンを自動で非活性化する")] 
    [SerializeField] private bool autoDisableWhenMissingConfig = true;

    private void Reset()
    {
        watch = FindObjectOfType<WatchUIUpdate>();
        runButton = GetComponentInChildren<Button>();
        statusText = GetComponentInChildren<TMP_Text>();
    }

    private void Awake()
    {
        if (watch == null) watch = FindObjectOfType<WatchUIUpdate>();
    }

    private void Update()
    {
        if (watch == null) return;
        bool ready = watch.HasEnemyPresetConfig;
        if (autoDisableWhenMissingConfig && runButton != null)
        {
            runButton.interactable = ready;
        }
        if (statusText != null)
        {
            statusText.text = ready ? "Ready: Enemy presets loaded" : "Config missing: assign EnemySpawnPresetCollection";
        }
    }

    // UIのOnClickから呼ぶ
    public void RunPresetSweep()
    {
        if (watch == null) return;
        if (!watch.HasEnemyPresetConfig)
        {
            Debug.LogWarning("[UI] Cannot run enemy preset sweep: enemyPresetConfig is missing or empty.");
            return;
        }
        watch.StartEnemyPresetSweep();
    }
}
