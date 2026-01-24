using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/HideBackground")]
public sealed class HideBackgroundEffect : EffectSO
{
    public override async UniTask Apply(GameContext context)
    {
        if (context.EventUI is not INovelEventUI novelUI) return;

        await novelUI.HideBackground();
    }
}
