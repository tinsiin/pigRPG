using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/RemoveTag")]
public sealed class RemoveTagEffect : EffectSO
{
    [SerializeField] private string tag;

    public override UniTask Apply(GameContext context)
    {
        if (!string.IsNullOrEmpty(tag))
        {
            context.RemoveTag(tag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Walk] RemoveTagEffect: Removed tag '{tag}'");
#endif
        }
        return UniTask.CompletedTask;
    }
}
