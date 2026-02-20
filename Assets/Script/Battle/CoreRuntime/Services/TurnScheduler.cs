using System.Collections.Generic;
using System.Linq;
using static CommonCalc;

public sealed class TurnScheduler
{
    private readonly BattleGroup allyGroup;
    private readonly BattleGroup enemyGroup;
    private readonly ActionQueue actionQueue;
    private readonly BattleState battleState;
    private readonly IBattleRandom _random;

    public TurnScheduler(BattleGroup allyGroup, BattleGroup enemyGroup, ActionQueue actionQueue, BattleState battleState, IBattleRandom random)
    {
        this.allyGroup = allyGroup;
        this.enemyGroup = enemyGroup;
        this.actionQueue = actionQueue;
        this.battleState = battleState;
        _random = random ?? new SystemBattleRandom();
    }

    public void RemoveDeadReservations()
    {
        actionQueue.RemoveDeathCharacters();
    }

    public BaseStates SelectRandomActer(out Faction acterFaction)
    {
        BaseStates chara;

        List<BaseStates> charas;
        List<BaseStates> primary;
        List<BaseStates> secondary;
        Faction primaryFaction;
        Faction secondaryFaction;

        if (_random.NextBool())
        {
            primary = allyGroup.Ours;
            primaryFaction = Faction.Ally;
            secondary = enemyGroup.Ours;
            secondaryFaction = Faction.Enemy;
        }
        else
        {
            primary = enemyGroup.Ours;
            primaryFaction = Faction.Enemy;
            secondary = allyGroup.Ours;
            secondaryFaction = Faction.Ally;
        }

        var primaryCandidates = RetainActionableCharacters(RemoveDeathCharacters(primary));
        if (primaryCandidates.Count > 0)
        {
            charas = primaryCandidates;
            acterFaction = primaryFaction;
        }
        else
        {
            var secondaryCandidates = RetainActionableCharacters(RemoveDeathCharacters(secondary));
            if (secondaryCandidates.Count == 0)
            {
                acterFaction = Faction.Ally;
                return null;
            }
            charas = secondaryCandidates;
            acterFaction = secondaryFaction;
        }

        chara = _random.GetItem(charas);
        chara.RecovelyWaitStart();

        return chara;
    }

    private List<BaseStates> RetainActionableCharacters(List<BaseStates> charas)
    {
        return charas.Where(chara => chara.RecovelyBattleField(battleState.TurnCount)).ToList();
    }
}
