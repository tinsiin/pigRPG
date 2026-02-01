using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Growth Settings", fileName = "GrowthSettings")]
public sealed class GrowthSettings : ScriptableObject
{
    public float winRate = 0.88f;
    public float runOutRate = 0.33f;
    public float allyRunOutRate = 0.66f;
    public float runOutTotalDivisor = 5f;
    public float allyRunOutMinFactor = 1f / 5f;
    public float allyRunOutMaxFactor = 3f / 5f;
    public float reEncountRate = 0.3f;
    public float reEncountDivisor = 4.2f;

    private static GrowthSettings _default;

    public static GrowthSettings Default
    {
        get
        {
            if (_default == null)
            {
                _default = CreateInstance<GrowthSettings>();
                _default.hideFlags = HideFlags.HideAndDontSave;
            }
            return _default;
        }
    }
}
