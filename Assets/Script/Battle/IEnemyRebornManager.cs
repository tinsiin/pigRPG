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
    bool CanReborn(NormalEnemy enemy, int globalSteps);

    /// <summary>
    /// 敵の復活情報をクリアする
    /// </summary>
    void Clear(NormalEnemy enemy);
}
