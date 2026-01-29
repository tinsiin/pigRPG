using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface IPlayersParty
{
    BattleGroup GetParty();
    void PlayersOnWin();
    void PlayersOnLost();
    void PlayersOnRunOut();
    void PlayersOnWalks(int walkCount);

    // === AllyId ベース（互換性用） ===
    void RequestStopFreezeConsecutive(AllyId allyId);

    // === CharacterId ベース（新規） ===
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
    // === AllyId ベース（互換性用） ===
    void OnlySelectActs(SkillZoneTrait trait, SkillType type, AllyId allyId);
    void OnSkillSelectionScreenTransition(AllyId allyId);
    UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea(List<BaseSkill> skills, int selectCount);
    void ReturnSelectSkillPassiveTargetSkillButtonsArea();
    void OpenEmotionalAttachmentSkillSelectUIArea(AllyId allyId);
    void OnBattleStart();

    // === CharacterId ベース（新規） ===
    void OnlySelectActs(SkillZoneTrait trait, SkillType type, CharacterId id);
    void OnSkillSelectionScreenTransition(CharacterId id);
    void OpenEmotionalAttachmentSkillSelectUIArea(CharacterId id);
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
