using System.Collections.Generic;

public enum BattlePhase
{
    Idle,
    WaitingForInput,
    Resolving,
    Animating,
    Completed
}

public enum ChoiceKind
{
    None,
    Skill,
    Range,
    Target
}

public enum ActionInputKind
{
    None,
    SkillSelect,
    StockSkill,
    DoNothing,
    RangeSelect,
    TargetSelect
}

public sealed class ChoiceRequest
{
    public ChoiceKind Kind;
    public int RequestId;
    public BaseStates Actor;
    public int RequiredTargetCount;
    public bool HasSingleTargetReservation;
    public SkillZoneTrait RangeMask;
    public SkillType TypeMask;

    public static ChoiceRequest None => new ChoiceRequest { Kind = ChoiceKind.None, RequestId = 0 };
}

public sealed class ActionInput
{
    public ActionInputKind Kind;
    public int RequestId;
    public BaseStates Actor;
    public BaseSkill Skill;
    public SkillZoneTrait RangeWill;
    public DirectedWill TargetWill = DirectedWill.One;
    public bool IsOption;
    public List<BaseStates> Targets = new();
}
