using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using static CommonCalc;

public sealed class TurnScheduler
{
    private readonly BattleGroup allyGroup;
    private readonly BattleGroup enemyGroup;
    private readonly ActionQueue actionQueue;
    private readonly BattleState battleState;

    public TurnScheduler(BattleGroup allyGroup, BattleGroup enemyGroup, ActionQueue actionQueue, BattleState battleState)
    {
        this.allyGroup = allyGroup;
        this.enemyGroup = enemyGroup;
        this.actionQueue = actionQueue;
        this.battleState = battleState;
    }

    public void RemoveDeadReservations()
    {
        actionQueue.RemoveDeathCharacters();
    }

    public BaseStates SelectRandomActer(out allyOrEnemy acterFaction)
    {
        BaseStates chara;

        List<BaseStates> charas;
        List<BaseStates> primary;
        List<BaseStates> secondary;
        allyOrEnemy primaryFaction;
        allyOrEnemy secondaryFaction;

        if (RandomEx.Shared.NextBool())
        {
            primary = allyGroup.Ours;
            primaryFaction = allyOrEnemy.alliy;
            secondary = enemyGroup.Ours;
            secondaryFaction = allyOrEnemy.Enemyiy;
        }
        else
        {
            primary = enemyGroup.Ours;
            primaryFaction = allyOrEnemy.Enemyiy;
            secondary = allyGroup.Ours;
            secondaryFaction = allyOrEnemy.alliy;
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
                acterFaction = allyOrEnemy.alliy;
                return null;
            }
            charas = secondaryCandidates;
            acterFaction = secondaryFaction;
        }

        chara = RandomEx.Shared.GetItem(charas.ToArray());
        chara.RecovelyWaitStart();

        return chara;
    }

    private List<BaseStates> RetainActionableCharacters(List<BaseStates> charas)
    {
        return charas.Where(chara => chara.RecovelyBattleField(battleState.TurnCount)).ToList();
    }
}