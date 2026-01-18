using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/Rewind To Anchor")]
public sealed class RewindToAnchorEffect : EffectSO
{
    [SerializeField] private string anchorId;
    [SerializeField] private RewindMode mode = RewindMode.PositionAndState;

    public override UniTask Apply(GameContext context)
    {
        if (context?.AnchorManager == null || string.IsNullOrEmpty(anchorId))
            return UniTask.CompletedTask;

        // Only set refresh flag if anchor exists (otherwise RewindToAnchor is a no-op)
        if (!context.AnchorManager.HasAnchor(anchorId))
            return UniTask.CompletedTask;

        context.AnchorManager.RewindToAnchor(anchorId, context, context.GateResolver, mode);
        context.RequestRefreshWithoutStep = context.IsWalkingStep;
        return UniTask.CompletedTask;
    }
}
