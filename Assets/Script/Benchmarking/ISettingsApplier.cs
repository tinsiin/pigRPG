public interface ISettingsApplier<TPreset>
{
    void SaveCurrent();
    void Apply(TPreset preset);
    void Restore();
}
