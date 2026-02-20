using System.Collections.Generic;

public sealed class ActionQueue
{
    private readonly List<ActionEntry> _entries = new();

    public int Count
    {
        get => _entries.Count;
    }

    public bool TryPeek(out ActionEntry entry)
    {
        if (_entries.Count == 0)
        {
            entry = null;
            return false;
        }
        entry = _entries[0];
        return entry != null;
    }

    public void RatherAdd(string mes, List<BaseStates> raterTargets = null, float raterDamage = 0f)
    {
        _entries.Add(new ActionEntry
        {
            Type = ActionType.Rather,
            Actor = null,
            Faction = Faction.Ally,
            Message = mes,
            Modifiers = null,
            Freeze = false,
            SingleTarget = null,
            ExCounterDEFATK = -1f,
            RatherTargets = raterTargets,
            RatherDamage = raterDamage
        });
    }

    public void Add(BaseStates chara, Faction charasFac, string mes = "", List<ModifierPart> modifys = null,
        bool isfreeze = false, BaseStates SingleTarget = null, float ExCounterDEFATK = -1)
    {
        _entries.Add(new ActionEntry
        {
            Type = ActionType.Act,
            Actor = chara,
            Faction = charasFac,
            Message = mes,
            Modifiers = modifys,
            Freeze = isfreeze,
            SingleTarget = SingleTarget,
            ExCounterDEFATK = ExCounterDEFATK,
            RatherTargets = null,
            RatherDamage = 0f
        });
    }

    public void RemoveDeathCharacters()
    {
        _entries.RemoveAll(entry => entry.Actor != null && entry.Actor.Death());
    }

    public void RemoveAt(int index)
    {
        _entries.RemoveAt(index);
    }
}
