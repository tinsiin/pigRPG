using UnityEngine;

public sealed class WalkLoopService
{
    private readonly PlayersRoster roster;

    public WalkLoopService(PlayersRoster roster)
    {
        this.roster = roster;
    }

    public void PlayersOnWalks(int walkCount)
    {
        int steps = Mathf.Max(1, walkCount);
        for (int s = 0; s < steps; s++)
        {
            foreach (var ally in roster.AllAllies)
            {
                ally?.OnWalkStepCallBack();
            }
        }
    }
}
