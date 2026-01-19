using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/PushEncounterOverlay")]
public sealed class PushEncounterOverlayEffect : EffectSO
{
    [SerializeField] private string overlayId;
    [SerializeField] private float multiplier = 1f;
    [Tooltip("Number of steps this overlay lasts. -1 = infinite until removed.")]
    [SerializeField] private int steps = -1;
    [Tooltip("If true, this overlay persists across saves.")]
    [SerializeField] private bool persistent;

    public override UniTask Apply(GameContext context)
    {
        if (!string.IsNullOrEmpty(overlayId))
        {
            context.PushEncounterOverlay(overlayId, multiplier, steps, persistent);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Walk] PushEncounterOverlayEffect: Pushed '{overlayId}' (x{multiplier}, {steps} steps, persistent={persistent})");
#endif
        }
        return UniTask.CompletedTask;
    }
}
