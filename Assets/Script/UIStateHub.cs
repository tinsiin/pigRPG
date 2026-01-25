using R3;

public static class UIStateHub
{
    public static ReactiveProperty<TabState> UserState { get; private set; }
    public static ReactiveProperty<SkillUICharaState> SkillState { get; private set; }
    public static ReactiveProperty<EyeAreaState> EyeState { get; private set; }

    public static void Bind(ReactiveProperty<TabState> userState, ReactiveProperty<SkillUICharaState> skillState)
    {
        UserState = userState;
        SkillState = skillState;
    }

    public static void BindEyeArea(ReactiveProperty<EyeAreaState> eyeState)
    {
        EyeState = eyeState;
    }

    public static void Clear(ReactiveProperty<TabState> userState, ReactiveProperty<SkillUICharaState> skillState)
    {
        if (ReferenceEquals(UserState, userState))
        {
            UserState = null;
        }
        if (ReferenceEquals(SkillState, skillState))
        {
            SkillState = null;
        }
    }

    public static void ClearEyeArea(ReactiveProperty<EyeAreaState> eyeState)
    {
        if (ReferenceEquals(EyeState, eyeState))
        {
            EyeState = null;
        }
    }
}
