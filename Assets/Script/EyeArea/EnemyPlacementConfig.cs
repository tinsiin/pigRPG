using UnityEngine;

/// <summary>
/// 敵配置の設定を保持する構造体。
/// WatchUIUpdateのSerializeFieldから値を受け取る。
/// Phase 3c: WatchUIUpdateから敵配置機能を分離。
/// </summary>
[System.Serializable]
public class EnemyPlacementConfig
{
    /// <summary>敵配置レイヤー（背景と一緒にズーム）</summary>
    public Transform BattleLayer;

    /// <summary>敵UIプレハブ</summary>
    public BattleIconUI EnemyUIPrefab;

    /// <summary>敵ランダム配置エリア（ズーム後座標系）</summary>
    public RectTransform SpawnArea;

    /// <summary>HPバーサイズ比 x: バー幅/アイコン幅, y: バー高/アイコン幅</summary>
    public Vector2 HpBarSizeRatio = new Vector2(1.0f, 0.15f);

    /// <summary>敵ランダム配置時の余白（ピクセル）</summary>
    public float Margin = 10f;

    /// <summary>敵UI生成をフレームに分散する</summary>
    public bool ThrottleSpawns = true;

    /// <summary>1フレームあたりに生成する敵UIの数</summary>
    public int SpawnBatchSize = 2;

    /// <summary>バッチ間で待機するフレーム数</summary>
    public int SpawnInterBatchFrames = 1;

    /// <summary>敵配置周りの詳細ログを出す</summary>
    public bool EnableVerboseLogs = false;
}
