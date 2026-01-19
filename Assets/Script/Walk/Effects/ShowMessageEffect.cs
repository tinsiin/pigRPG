using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/ShowMessage")]
public sealed class ShowMessageEffect : EffectSO
{
    [TextArea(2, 5)]
    [SerializeField] private string message;

    public override UniTask Apply(GameContext context)
    {
        if (string.IsNullOrEmpty(message)) return UniTask.CompletedTask;

        // Use the event UI from context to show message
        context.EventUI?.ShowMessage(message);

        return UniTask.CompletedTask;
    }
}
