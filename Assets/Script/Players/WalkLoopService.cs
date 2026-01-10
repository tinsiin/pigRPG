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
        var allies = roster.Allies;
        for (int s = 0; s < steps; s++)
        {
            for (int i = 0; i < allies.Length; i++)
            {
                allies[i].OnWalkStepCallBack();
            }
        }
    }
}
