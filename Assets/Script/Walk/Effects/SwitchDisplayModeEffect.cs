using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/SwitchDisplayMode")]
public sealed class SwitchDisplayModeEffect : EffectSO
{
    [SerializeField] private DisplayMode targetMode;

    public override async UniTask Apply(GameContext context)
    {
        if (context.EventUI is not INovelEventUI novelUI) return;

        await novelUI.SwitchTextBox(targetMode);
    }
}
