using R3;

public static class UIStateHub
{
    public static ReactiveProperty<TabState> UserState { get; private set; }

    /// <summary>
    /// 選択中のキャラクターID。
    /// </summary>
    public static ReactiveProperty<CharacterId> SelectedCharacterId { get; private set; }

    public static ReactiveProperty<EyeAreaState> EyeState { get; private set; }

    public static void Bind(ReactiveProperty<TabState> userState)
    {
        UserState = userState;

        // CharacterId版も初期化（Geino初期値）
        if (SelectedCharacterId == null)
        {
            SelectedCharacterId = new ReactiveProperty<CharacterId>(CharacterId.Geino);
        }
    }

    public static void BindEyeArea(ReactiveProperty<EyeAreaState> eyeState)
    {
        EyeState = eyeState;
    }

    public static void Clear(ReactiveProperty<TabState> userState)
    {
        if (ReferenceEquals(UserState, userState))
        {
            UserState = null;
        }
    }

    public static void ClearEyeArea(ReactiveProperty<EyeAreaState> eyeState)
    {
        if (ReferenceEquals(EyeState, eyeState))
        {
            EyeState = null;
        }
    }

    /// <summary>
    /// キャラクターIDを設定。
    /// </summary>
    public static void SetSelectedCharacter(CharacterId id)
    {
        if (SelectedCharacterId != null)
        {
            SelectedCharacterId.Value = id;
        }
    }
}
