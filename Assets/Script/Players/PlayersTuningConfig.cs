public sealed class PlayersTuningConfig : IPlayersTuning
{
    public float ExplosionVoid { get; private set; }
    public int HpToMaxPConversionFactor { get; private set; }
    public int MentalHpToPRecoveryConversionFactor { get; private set; }
    public BaseSkillPassive EmotionalAttachmentSkillWeakeningPassive { get; private set; }

    public float ExplosionVoidValue => ExplosionVoid;
    public BaseSkillPassive EmotionalAttachmentSkillWeakeningPassiveRef => EmotionalAttachmentSkillWeakeningPassive;

    public void Initialize(int hpToMaxP, int mentalHpToP, BaseSkillPassive weakeningPassive)
    {
        HpToMaxPConversionFactor = hpToMaxP;
        MentalHpToPRecoveryConversionFactor = mentalHpToP;
        EmotionalAttachmentSkillWeakeningPassive = weakeningPassive;
    }

    public void SetExplosionVoid(float value)
    {
        ExplosionVoid = value;
    }
}
