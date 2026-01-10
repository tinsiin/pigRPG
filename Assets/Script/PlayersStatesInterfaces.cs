using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface IPlayersProgress
{
    int NowProgress { get; }
    int NowStageID { get; }
    int NowAreaID { get; }
    void AddProgress(int addPoint);
    void ProgressReset();
    void SetArea(int id);
}

public interface IPlayersParty
{
    BattleGroup GetParty();
    void PlayersOnWin();
    void PlayersOnLost();
    void PlayersOnRunOut();
    void PlayersOnWalks(int walkCount);
    void RequestStopFreezeConsecutive(int index);
}

public interface IPlayersUIControl
{
    void AllyAlliesUISetActive(bool isActive);
}

public interface IPlayersSkillUI
{
    void OnlySelectActs(SkillZoneTrait trait, SkillType type, int index);
    void OnSkillSelectionScreenTransition(int index);
    UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea(List<BaseSkill> skills, int selectCount);
    void ReturnSelectSkillPassiveTargetSkillButtonsArea();
    void OpenEmotionalAttachmentSkillSelectUIArea(int index);
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
    bool TryGetAllyIndex(BaseStates actor, out int index);
    BaseStates GetAllyByIndex(int index);
}
