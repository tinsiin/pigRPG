using R3;

public static class UIStateHub
{
    public static ReactiveProperty<TabState> UserState { get; private set; }

    /// <summary>
    /// スキルUIのキャラクター状態（旧: enum版）。
    /// 新キャラ対応のため、SelectedCharacterIdの使用を推奨。
    /// </summary>
    [System.Obsolete("SelectedCharacterIdを使用してください")]
    public static ReactiveProperty<SkillUICharaState> SkillState { get; private set; }

    /// <summary>
    /// 選択中のキャラクターID（新: CharacterId版）。
    /// 新キャラクターにも対応。
    /// </summary>
    public static ReactiveProperty<CharacterId> SelectedCharacterId { get; private set; }

    public static ReactiveProperty<EyeAreaState> EyeState { get; private set; }

#pragma warning disable CS0618 // Obsolete warning suppressed for compatibility
    public static void Bind(ReactiveProperty<TabState> userState, ReactiveProperty<SkillUICharaState> skillState)
    {
        UserState = userState;
        SkillState = skillState;

        // CharacterId版も初期化（Geino初期値）
        if (SelectedCharacterId == null)
        {
            SelectedCharacterId = new ReactiveProperty<CharacterId>(CharacterId.Geino);
        }
    }
#pragma warning restore CS0618

    public static void BindEyeArea(ReactiveProperty<EyeAreaState> eyeState)
    {
        EyeState = eyeState;
    }

#pragma warning disable CS0618 // Obsolete warning suppressed for compatibility
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
#pragma warning restore CS0618

    public static void ClearEyeArea(ReactiveProperty<EyeAreaState> eyeState)
    {
        if (ReferenceEquals(EyeState, eyeState))
        {
            EyeState = null;
        }
    }

    /// <summary>
    /// キャラクターIDを設定（新キャラ対応）。
    /// </summary>
    public static void SetSelectedCharacter(CharacterId id)
    {
        if (SelectedCharacterId != null)
        {
            SelectedCharacterId.Value = id;
        }

        // 固定メンバーの場合は旧SkillStateも同期
#pragma warning disable CS0618
        if (SkillState != null && id.IsOriginalMember)
        {
            SkillState.Value = id.Value switch
            {
                "geino" => SkillUICharaState.geino,
                "noramlia" => SkillUICharaState.normalia,
                "sites" => SkillUICharaState.sites,
                _ => SkillUICharaState.geino
            };
        }
#pragma warning restore CS0618
    }
}
