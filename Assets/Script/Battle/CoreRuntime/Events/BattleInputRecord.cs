using System.Collections.Generic;

public readonly struct BattleInputRecord
{
    public BattleInputType Type { get; }
    public string ActorName { get; }
    public string SkillName { get; }
    public DirectedWill TargetWill { get; }
    public SkillZoneTrait RangeWill { get; }
    public bool IsOption { get; }
    public string[] TargetNames { get; }
    /// <summary>
    /// ターゲットのAllCharactersリスト内でのインデックス。
    /// 同名キャラクターの曖昧性を解決するために使用。
    /// </summary>
    public int[] TargetIndices { get; }
    public int TurnCount { get; }

    public BattleInputRecord(
        BattleInputType type,
        string actorName,
        string skillName,
        DirectedWill targetWill,
        SkillZoneTrait rangeWill,
        bool isOption,
        string[] targetNames,
        int[] targetIndices,
        int turnCount)
    {
        Type = type;
        ActorName = actorName ?? "";
        SkillName = skillName ?? "";
        TargetWill = targetWill;
        RangeWill = rangeWill;
        IsOption = isOption;
        TargetNames = targetNames;
        TargetIndices = targetIndices;
        TurnCount = turnCount;
    }

    public static BattleInputRecord FromInput(BattleInput input, BaseStates actor, int turnCount, IReadOnlyList<BaseStates> allCharacters = null)
    {
        var actorName = actor != null ? actor.CharacterName : input.Actor?.CharacterName ?? "";
        var skillName = input.Skill != null ? input.Skill.SkillName : "";
        string[] targetNames = null;
        int[] targetIndices = null;

        if (input.Targets != null && input.Targets.Count > 0)
        {
            targetNames = new string[input.Targets.Count];
            targetIndices = new int[input.Targets.Count];
            for (var i = 0; i < input.Targets.Count; i++)
            {
                var target = input.Targets[i];
                targetNames[i] = target?.CharacterName ?? "";
                targetIndices[i] = FindCharacterIndex(target, allCharacters);
            }
        }

        return new BattleInputRecord(
            input.Type,
            actorName,
            skillName,
            input.TargetWill,
            input.RangeWill,
            input.IsOption,
            targetNames,
            targetIndices,
            turnCount);
    }

    private static int FindCharacterIndex(BaseStates target, IReadOnlyList<BaseStates> allCharacters)
    {
        if (target == null || allCharacters == null) return -1;
        for (var i = 0; i < allCharacters.Count; i++)
        {
            if (ReferenceEquals(allCharacters[i], target))
            {
                return i;
            }
        }
        return -1;
    }
}
