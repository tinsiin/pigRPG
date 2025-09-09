/// <summary>
/// 敵配置のために必要な最小限の文脈情報。
/// 具体的な敵データ構造は次フェーズで拡張（当面は個数/オプションのみ）。
/// </summary>
public interface IEnemyPlacementContext
{
    int EnemyCount { get; }

    // Activate を一括で行うか（パフォーマンス最適化用のヒント）
    bool BatchActivate { get; }

    // 固定サイズ強制（null のときは既存ロジックに委ねる）
    float? FixedSizeOverride { get; }

    // 実配置に必要な参照
    BattleGroup EnemyGroup { get; }
}
