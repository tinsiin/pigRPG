using System.Collections.Generic;

public sealed class ActionQueue
{
    private readonly List<ActionEntry> _entries = new();

    public int Count
    {
        get => _entries.Count;
    }

    public void RatherAdd(string mes, List<BaseStates> raterTargets = null, float raterDamage = 0f)
    {
        _entries.Add(new ActionEntry
        {
            Type = ActionType.Rather,
            Actor = null,
            Faction = allyOrEnemy.alliy,
            Message = mes,
            Modifiers = null,
            Freeze = false,
            SingleTarget = null,
            ExCounterDEFATK = -1f,
            RatherTargets = raterTargets,
            RatherDamage = raterDamage
        });
    }

    public void Add(BaseStates chara, allyOrEnemy charasFac, string mes = "", List<ModifierPart> modifys = null,
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

    public string GetAtTopMessage(int index)
    {
        var entry = GetAt(index);
        return entry != null ? entry.Message : "";
    }

    public BaseStates GetAtCharacter(int index)
    {
        return GetAt(index)?.Actor;
    }

    public allyOrEnemy GetAtFaction(int index)
    {
        var entry = GetAt(index);
        if (entry == null) return allyOrEnemy.alliy;
        return entry.Faction;
    }

    public List<ModifierPart> GetAtModifyList(int index)
    {
        return GetAt(index)?.Modifiers;
    }

    public bool GetAtIsFreezeBool(int index)
    {
        var entry = GetAt(index);
        return entry != null && entry.Freeze;
    }

    public BaseStates GetAtSingleTarget(int index)
    {
        return GetAt(index)?.SingleTarget;
    }

    public float GetAtExCounterDEFATK(int index)
    {
        var entry = GetAt(index);
        return entry != null ? entry.ExCounterDEFATK : -1f;
    }

    public List<BaseStates> GetAtRaterTargets(int index)
    {
        return GetAt(index)?.RatherTargets;
    }

    public float GetAtRaterDamage(int index)
    {
        var entry = GetAt(index);
        return entry != null ? entry.RatherDamage : 0f;
    }

    private ActionEntry GetAt(int index)
    {
        if (index < 0 || index >= _entries.Count) return null;
        return _entries[index];
    }
}