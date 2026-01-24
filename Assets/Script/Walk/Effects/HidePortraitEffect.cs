using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/HidePortrait")]
public sealed class HidePortraitEffect : EffectSO
{
    [SerializeField] private PortraitPosition position;
    [SerializeField] private bool useExit; // trueなら横にスライドアウト（捌ける）

    public override async UniTask Apply(GameContext context)
    {
        if (context.EventUI is not INovelEventUI novelUI) return;

        if (useExit && novelUI is NovelPartEventUI novelPartUI)
        {
            await novelPartUI.ExitPortrait(position);
        }
        else
        {
            await novelUI.HidePortrait(position);
        }
    }
}
