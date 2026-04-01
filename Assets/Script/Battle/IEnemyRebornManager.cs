using System.Collections.Generic;

/// <summary>
/// 敵復活管理のインターフェース
/// </summary>
public interface IEnemyRebornManager
{
    /// <summary>
    /// バトル終了時に死亡した敵の復活カウントを開始する
    /// </summary>
    void OnBattleEnd(IReadOnlyList<NormalEnemy> enemies, int globalSteps);

    /// <summary>
    /// 復活歩数をカウント変数にセットする
    /// </summary>
    void PrepareReborn(NormalEnemy enemy, int globalSteps);

    /// <summary>
    /// 敵が復活可能かどうかを判定する
    /// </summary>
    bool CanReborn(NormalEnemy enemy, int globalSteps, int stepMultiplier = 1);

    /// <summary>
    /// 敵の復活情報をクリアする
    /// </summary>
    void Clear(NormalEnemy enemy);

    /// <summary>
    /// 全敵の復活情報をクリアする（ロード時・ノード移動時に使用）
    /// </summary>
    void ClearAll();

    /// <summary>
    /// 敵の復活状態を取得する（セーブ用）。エントリがなければnull
    /// </summary>
    (int RemainingSteps, int LastProgress, EnemyRebornState State)? GetRebornState(NormalEnemy enemy);

    /// <summary>
    /// セーブデータから復活状態を復元する
    /// </summary>
    void RestoreRebornState(NormalEnemy enemy, int remainingSteps, int lastProgress, EnemyRebornState state);
}
