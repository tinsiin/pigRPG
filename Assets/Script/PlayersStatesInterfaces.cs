using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface IPlayersParty
{
    BattleGroup GetParty();
    void PlayersOnWin();
    void PlayersOnLost();
    void PlayersOnRunOut();
    void PlayersOnWalks(int walkCount);
    void RequestStopFreezeConsecutive(AllyId allyId);
}

public interface IPlayersUIControl
{
    void AllyAlliesUISetActive(bool isActive);
}

public interface IPlayersSkillUI
{
    void OnlySelectActs(SkillZoneTrait trait, SkillType type, AllyId allyId);
    void OnSkillSelectionScreenTransition(AllyId allyId);
    UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea(List<BaseSkill> skills, int selectCount);
    void ReturnSelectSkillPassiveTargetSkillButtonsArea();
    void OpenEmotionalAttachmentSkillSelectUIArea(AllyId allyId);
    void OnBattleStart();
}

public interface IPlayersTuning
{
    float ExplosionVoidValue { get; }
    int HpToMaxPConversionFactor { get; }
    int MentalHpToPRecoveryConversionFactor { get; }
    BaseSkillPassive EmotionalAttachmentSkillWeakeningPassiveRef { get; }
}

public interface IPlayersRoster
{
    int AllyCount { get; }
    bool TryGetAllyId(BaseStates actor, out AllyId id);
    BaseStates GetAllyById(AllyId id);

    // === CharacterId 対応（新規） ===

    /// <summary>キャラクターを取得</summary>
    AllyClass GetAlly(CharacterId id);

    /// <summary>全解放済みキャラクター</summary>
    IEnumerable<AllyClass> AllAllies { get; }

    /// <summary>解放済みキャラクターを固定順序で取得（インデックスアクセス用）</summary>
    IReadOnlyList<AllyClass> OrderedAllies { get; }

    /// <summary>キャラクターが解放済みか</summary>
    bool IsUnlocked(CharacterId id);

    /// <summary>キャラクターIDを逆引き</summary>
    bool TryGetCharacterId(BaseStates actor, out CharacterId id);
}
