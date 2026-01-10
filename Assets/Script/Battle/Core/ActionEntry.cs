using System.Collections.Generic;

public enum ActionType
{
    Act,
    Rather,
    Counter,
    Skip
}

public sealed class ActionEntry
{
    public ActionType Type;
    public BaseStates Actor;
    public allyOrEnemy Faction;
    public string Message;
    public List<ModifierPart> Modifiers;
    public bool Freeze;
    public BaseStates SingleTarget;
    public float ExCounterDEFATK;
    public List<BaseStates> RatherTargets;
    public float RatherDamage;
}