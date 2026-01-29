using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface IPlayersParty
{
    BattleGroup GetParty();
    void PlayersOnWin();
    void PlayersOnLost();
    void PlayersOnRunOut();
    void PlayersOnWalks(int walkCount);

    /// <summary>FreezeConsecutive の停止リクエスト</summary>
    void RequestStopFreezeConsecutive(CharacterId id);
}

public interface IPlayersUIControl
{
    void AllyAlliesUISetActive(bool isActive);

    /// <summary>
    /// 新キャラクターのUIバインディングを行う。
    /// CharacterUnlockEffect や セーブロード時に呼び出す。
    /// </summary>
    void BindNewCharacter(CharacterId id);
}

public interface IPlayersSkillUI
{
    void OnlySelectActs(SkillZoneTrait trait, SkillType type, CharacterId id);
    void OnSkillSelectionScreenTransition(CharacterId id);
    UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea(List<BaseSkill> skills, int selectCount);
    void ReturnSelectSkillPassiveTargetSkillButtonsArea();
    void OpenEmotionalAttachmentSkillSelectUIArea(CharacterId id);
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
