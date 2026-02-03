using System.Collections.Generic;

/// <summary>
/// 戦闘中のキャラクター/グループ情報を照会するサービス。
/// 重複していたIsVanguard, GetGroupForCharacter等を統一する。
/// </summary>
public interface IBattleQueryService
{
    /// <summary>
    /// キャラクターが前のめり状態かどうか
    /// </summary>
    bool IsVanguard(BaseStates chara);

    /// <summary>
    /// キャラクターが属するグループを取得
    /// </summary>
    BattleGroup GetGroupForCharacter(BaseStates chara);

    /// <summary>
    /// キャラクターの陣営を取得
    /// </summary>
    allyOrEnemy GetCharacterFaction(BaseStates chara);

    /// <summary>
    /// 同じグループの生存している他のキャラクターを取得
    /// </summary>
    List<BaseStates> GetOtherAlliesAlive(BaseStates chara);

    /// <summary>
    /// 2つのキャラクターが同じ陣営かどうか
    /// </summary>
    bool IsFriend(BaseStates chara1, BaseStates chara2);

    /// <summary>
    /// 陣営からグループを取得
    /// </summary>
    BattleGroup FactionToGroup(allyOrEnemy faction);
}
