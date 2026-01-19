using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/RemoveEncounterOverlay")]
public sealed class RemoveEncounterOverlayEffect : EffectSO
{
    [SerializeField] private string overlayId;

    public override UniTask Apply(GameContext context)
    {
        if (!string.IsNullOrEmpty(overlayId))
        {
            context.RemoveEncounterOverlay(overlayId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Walk] RemoveEncounterOverlayEffect: Removed '{overlayId}'");
#endif
        }
        return UniTask.CompletedTask;
    }
}
