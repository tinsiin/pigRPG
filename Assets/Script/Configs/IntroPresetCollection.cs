using UnityEngine;

[CreateAssetMenu(menuName = "Benchmark/IntroPresetCollection", fileName = "IntroPresetCollection")]
public sealed class IntroPresetCollection : ScriptableObject
{
    [Tooltip("ベンチに使用する導入プリセットの配列")]
    public WatchUIUpdate.IntroPreset[] items = System.Array.Empty<WatchUIUpdate.IntroPreset>();

    [Tooltip("各プリセットの繰り返し回数（平均の安定度）")]
    public int repeatCount = 100;

    [Tooltip("各回の間に挟む待機時間（秒）。0で連続実行")]
    public float interRunDelaySec = 0f;

    [TextArea]
    public string description;
}
