/// <summary>
/// WatchUIUpdate.EnemySpawnPreset の適用器。WatchUIUpdate の敵UI関連4項目を保存/適用/復元する。
/// </summary>
public sealed class EnemySpawnSettingsApplier : ISettingsApplier<global::WatchUIUpdate.EnemySpawnPreset>
{
    private readonly global::WatchUIUpdate _wui;
    private global::WatchUIUpdate.EnemySpawnSettingsSnapshot _snap;

    public EnemySpawnSettingsApplier(global::WatchUIUpdate wui)
    {
        _wui = wui;
    }

    public void SaveCurrent()
    {
        _snap = _wui.SaveCurrentEnemySpawnSettings();
    }

    public void Apply(global::WatchUIUpdate.EnemySpawnPreset p)
    {
        _wui.ApplyEnemySpawnPreset(p);
    }

    public void Restore()
    {
        _wui.RestoreEnemySpawnSettings(_snap);
    }
}
