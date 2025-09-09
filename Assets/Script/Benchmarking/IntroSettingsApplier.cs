/// <summary>
/// WatchUIUpdate.IntroPreset の適用器。WatchUIUpdate の4項目を保存/適用/復元する。
/// </summary>
public sealed class IntroSettingsApplier : ISettingsApplier<global::WatchUIUpdate.IntroPreset>
{
    private readonly global::WatchUIUpdate _wui;
    private global::WatchUIUpdate.IntroSettingsSnapshot _snap;

    public IntroSettingsApplier(global::WatchUIUpdate wui)
    {
        _wui = wui;
    }

    public void SaveCurrent()
    {
        _snap = _wui.SaveCurrentIntroSettings();
    }

    public void Apply(global::WatchUIUpdate.IntroPreset p)
    {
        _wui.ApplyIntroPreset(p);
    }

    public void Restore()
    {
        _wui.RestoreIntroSettings(_snap);
    }
}
