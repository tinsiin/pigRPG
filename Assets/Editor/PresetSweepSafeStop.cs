#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// Play 停止時に、Preset Sweep を安全に停止してズーム状態を即時復元するエディタ補助。
/// 実行フロー:
/// - ExitingPlayMode で WatchUIUpdate.CancelPresetSweep() を呼び出し
/// - Orchestrator 経由の即時復元 (animated=false) を要求
/// これにより、復元前に停止してズームTransformが編集状態に見かけ上残る事象を抑止する。
/// </summary>
[InitializeOnLoad]
public static class PresetSweepSafeStop
{
    static PresetSweepSafeStop()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;

        // 可能な限り安全に取り扱う（例外は握りつぶして停止処理を妨げない）
        try
        {
            var wui = global::WatchUIUpdate.Instance ?? Object.FindObjectOfType<global::WatchUIUpdate>();
            if (wui == null) return;

            // 1) スイープ中ならキャンセル要求（キャンセル経路での即時復元も担保）
            try { wui.CancelPresetSweep(); } catch { /* no-op */ }

            // 2) 念のため、Orchestrator 経由の"即時"復元も要求
            //    animated=false の場合、DefaultIntroOrchestrator.RestoreAsync は内部で _zoom.RestoreImmediate() を直ちに呼び出す。
            try { wui.RestoreZoomViaOrchestrator(animated: false, duration: 0f).Forget(); } catch { /* no-op */ }
        }
        catch { /* no-op */ }
    }

    // 任意: 手動のセーフストップ用メニュー（ショートカット: Ctrl+Alt+S）
    [MenuItem("Tools/Benchmark/Safe Stop Preset Sweep %&S")]
    private static void SafeStopMenu()
    {
        try
        {
            var wui = global::WatchUIUpdate.Instance ?? Object.FindObjectOfType<global::WatchUIUpdate>();
            if (wui != null)
            {
                try { wui.CancelPresetSweep(); } catch { /* no-op */ }
                try { wui.RestoreZoomViaOrchestrator(animated: false, duration: 0f).Forget(); } catch { /* no-op */ }
            }
        }
        finally
        {
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
