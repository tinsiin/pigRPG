using System.Collections.Generic;

public interface IBattleContext
{
    BattleGroup AllyGroup { get; }
    BattleGroup EnemyGroup { get; }
    List<BaseStates> AllCharacters { get; }
    BaseStates Acter { get; }
    UnderActersEntryList unders { get; }
    ActionQueue Acts { get; }
    bool SkillStock { get; set; }
    bool DoNothing { get; set; }
    bool PassiveCancel { get; set; }

    int BattleTurnCount { get; }
    allyOrEnemy GetCharacterFaction(BaseStates chara);
    BattleGroup FactionToGroup(allyOrEnemy faction);
    BattleGroup MyGroup(BaseStates chara);
    bool IsFriend(BaseStates chara1, BaseStates chara2);
    bool IsVanguard(BaseStates chara);
    void BeVanguard(BaseStates newVanguard);
    List<BaseStates> GetOtherAlliesAlive(BaseStates chara);
}
