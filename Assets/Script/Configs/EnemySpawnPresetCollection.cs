using UnityEngine;

[CreateAssetMenu(menuName = "Benchmark/EnemySpawnPresetCollection", fileName = "EnemySpawnPresetCollection")]
public sealed class EnemySpawnPresetCollection : ScriptableObject
{
    [Tooltip("ベンチに使用する敵UIプリセットの配列")]
    public WatchUIUpdate.EnemySpawnPreset[] items = System.Array.Empty<WatchUIUpdate.EnemySpawnPreset>();

    [Tooltip("各プリセットの繰り返し回数（平均の安定度）")]
    public int repeatCount = 100;

    [Tooltip("各回の間に挟む待機時間（秒）。0で連続実行")]
    public float interRunDelaySec = 0f;

    [TextArea]
    public string description;
}
