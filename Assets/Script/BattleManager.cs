

/// <summary>
///     バトルを、管理するクラス
/// </summary>
public class BattleManager
{
    /// <summary>
    ///     プレイヤー側のバトルグループ　ここに味方のバトルグループオブジェクトをリンクする？
    /// </summary>
    private BattleGroup AllyGroup;

    /// <summary>
    ///     敵側のバトルグループ　ここに敵グループのバトルグループオブジェクトをリンクする？
    /// </summary>
    private BattleGroup EnemyGroup;

    /// <summary>
    ///     コンストラクタ
    /// </summary>
    public BattleManager(BattleGroup allyGroup, BattleGroup enemyGroup)
    {
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;
    }

    public void BattleTurn()
    {
    }
}