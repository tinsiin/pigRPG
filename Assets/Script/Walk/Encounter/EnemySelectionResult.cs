using System.Collections.Generic;

/// <summary>
/// EncounterEnemySelector.Selectの結果。
/// BattleGroup（null可）+ パッシブ致死ログ情報を包含する。
/// </summary>
public class EnemySelectionResult
{
    /// <summary>結成された敵グループ。全滅時はnull</summary>
    public BattleGroup Group;

    /// <summary>パッシブ致死ログのリスト（空なら致死なし）</summary>
    public List<PassiveKillEntry> PassiveKills = new();

    public bool HasPassiveKills => PassiveKills.Count > 0;
}
