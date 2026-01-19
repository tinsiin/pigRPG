using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/AddTag")]
public sealed class AddTagEffect : EffectSO
{
    [SerializeField] private string tag;

    public override UniTask Apply(GameContext context)
    {
        if (!string.IsNullOrEmpty(tag))
        {
            context.AddTag(tag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Walk] AddTagEffect: Added tag '{tag}'");
#endif
        }
        return UniTask.CompletedTask;
    }
}
