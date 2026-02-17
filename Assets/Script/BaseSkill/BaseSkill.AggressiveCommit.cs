using System;
using UnityEngine;


/// <summary>
/// フェーズ別の前のめり設定。
/// デフォルト状態とプレイヤー選択権を1フェーズ分保持する。
/// classなのでランタイムで直接フィールドを書き換え可能。
/// </summary>
[Serializable]
public class PhaseAggressiveSetting
{
    /// <summary>
    /// このフェーズでデフォルトで前のめりするか。
    /// canSelect=trueの場合、プレイヤーがトグルで変更できるランタイム状態も兼ねる。
    /// </summary>
    public bool isAggressiveCommit;

    /// <summary>
    /// このフェーズの前のめりをプレイヤーが切り替えられるか。
    /// trueならUIにトグルボタンが表示される。
    /// </summary>
    public bool canSelect;

    public PhaseAggressiveSetting() { }

    public PhaseAggressiveSetting(bool isAggressiveCommit, bool canSelect)
    {
        this.isAggressiveCommit = isAggressiveCommit;
        this.canSelect = canSelect;
    }

    /// <summary>
    /// ディープコピーを作成する。フィールド追加時にここも更新すること。
    /// </summary>
    public PhaseAggressiveSetting Clone()
    {
        return new PhaseAggressiveSetting(isAggressiveCommit, canSelect);
    }
}

public partial class BaseSkill
{
    /// <summary>
    /// スキル実行時の前のめり設定
    /// </summary>
    public PhaseAggressiveSetting AggressiveOnExecute => FixedSkillLevelData[_levelIndex].AggressiveOnExecute;

    /// <summary>
    /// トリガーカウント中の前のめり設定
    /// </summary>
    public PhaseAggressiveSetting AggressiveOnTrigger => FixedSkillLevelData[_levelIndex].AggressiveOnTrigger;

    /// <summary>
    /// ストック中の前のめり設定
    /// </summary>
    public PhaseAggressiveSetting AggressiveOnStock => FixedSkillLevelData[_levelIndex].AggressiveOnStock;
}
