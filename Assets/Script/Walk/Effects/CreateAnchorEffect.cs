using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/Create Anchor")]
public sealed class CreateAnchorEffect : EffectSO
{
    [SerializeField] private string anchorId;
    [SerializeField] private AnchorScope scope = AnchorScope.Region;

    public override UniTask Apply(GameContext context)
    {
        if (context?.AnchorManager == null || string.IsNullOrEmpty(anchorId))
            return UniTask.CompletedTask;

        context.AnchorManager.CreateAnchor(anchorId, context, context.GateResolver, scope);

        return UniTask.CompletedTask;
    }
}
