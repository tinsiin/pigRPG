using UnityEngine;

[CreateAssetMenu(menuName = "Benchmark/OutputSettings", fileName = "BenchmarkOutputSettings")]
public sealed class BenchmarkOutputSettings : ScriptableObject
{
    [Header("出力の有効/保存")]
    public bool enableCsv = false;
    public bool enableJson = false;
    public bool writeToFile = false;
    [Tooltip("各Runの明細をCSVに出力する")] public bool enablePerRunCsv = false;
    [Tooltip("各Runの明細をJSONに出力する")] public bool enablePerRunJson = false;

    [Header("保存先/ファイル名（writeToFile が有効なときのみ使用）")]
    [Tooltip("保存先フォルダ。空なら Application.persistentDataPath を使用")] public string outputFolder = "";
    [Tooltip("ファイル名のベース。拡張子は自動付与されます")] public string fileBaseName = "benchmark";

    [Header("ファイル名テンプレート（任意）")]
    [Tooltip("空なら従来の {base}_{scenario}_{presetCol}_{ts} を使用。使用可能トークン: {base},{scenario},{presetCol},{ts},{repeat},{presets}")]
    public string fileNameTemplate = "";
    [Tooltip("テンプレート内のサブフォルダ（/ 含有）を自動生成します。OFFならセパレータは '_' に置換")] public bool createSubfolders = true;

    [Header("per-run 出力（大規模対策）")]
    [Tooltip(">0 のとき、per-run 明細をストリーム書き込みに切替。値はフラッシュ間隔（行数）だが、現実装は毎行追記扱い")] public int perRunFlushEvery = 0;

    [TextArea]
    public string description;
}
