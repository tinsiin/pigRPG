using System;

public sealed class PlayersRoster : IPlayersRoster
{
    private AllyClass[] allies;

    public AllyClass[] Allies => allies;

    public int AllyCount => allies?.Length ?? 0;

    public void SetAllies(AllyClass[] value)
    {
        allies = value;
    }

    public bool TryGetAllyIndex(BaseStates actor, out int index)
    {
        index = -1;
        if (actor == null || allies == null) return false;
        for (int i = 0; i < allies.Length; i++)
        {
            if (ReferenceEquals(allies[i], actor))
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    public BaseStates GetAllyByIndex(int index)
    {
        if (allies == null || index < 0 || index >= allies.Length) return null;
        return allies[index];
    }

    public bool TryGetAllyId(BaseStates actor, out PlayersStates.AllyId id)
    {
        id = default;
        if (TryGetAllyIndex(actor, out var idx) && Enum.IsDefined(typeof(PlayersStates.AllyId), idx))
        {
            id = (PlayersStates.AllyId)idx;
            return true;
        }
        return false;
    }
}
