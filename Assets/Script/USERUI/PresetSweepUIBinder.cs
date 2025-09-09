using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class PresetSweepUIBinder : MonoBehaviour
{
    [Header("参照（未指定なら自動探索）")]
    [SerializeField] private WatchUIUpdate watch;
    [SerializeField] private Button runButton;
    [SerializeField] private TMP_Text statusText;

    [Header("挙動")]
    [Tooltip("presetConfig が未割り当て/空のときはボタンを自動で非活性化する")] 
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
        bool ready = watch.HasPresetConfig;
        if (autoDisableWhenMissingConfig && runButton != null)
        {
            runButton.interactable = ready;
        }
        if (statusText != null)
        {
            statusText.text = ready ? "Ready: Presets loaded" : "Config missing: assign IntroPresetCollection";
        }
    }

    // UIのOnClickから呼ぶ
    public void RunPresetSweep()
    {
        if (watch == null) return;
        if (!watch.HasPresetConfig)
        {
            Debug.LogWarning("[UI] Cannot run preset sweep: presetConfig is missing or empty.");
            return;
        }
        watch.StartPresetSweep();
    }
}
