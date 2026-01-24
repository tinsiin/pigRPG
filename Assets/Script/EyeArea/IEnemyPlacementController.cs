using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 敵UIの配置・管理を担当するコントローラーのインターフェース。
/// Phase 3c: WatchUIUpdateから敵配置機能を分離。
/// </summary>
public interface IEnemyPlacementController
{
    /// <summary>
    /// BattleGroupの敵リストに基づいて敵UIを配置
    /// </summary>
    UniTask PlaceEnemiesAsync(BattleGroup enemyGroup);

    /// <summary>
    /// 配置済み敵UIをすべてクリア
    /// </summary>
    void ClearEnemyUI();

    /// <summary>
    /// 敵配置エリア（ズーム後座標系）
    /// </summary>
    RectTransform SpawnArea { get; }

    /// <summary>
    /// 敵配置レイヤー
    /// </summary>
    Transform BattleLayer { get; }
}
