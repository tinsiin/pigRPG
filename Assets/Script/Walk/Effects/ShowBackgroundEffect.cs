using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/ShowBackground")]
public sealed class ShowBackgroundEffect : EffectSO
{
    [SerializeField] private string backgroundId;
    [SerializeField] private bool useSlideIn;

    public override async UniTask Apply(GameContext context)
    {
        if (context.EventUI is not INovelEventUI novelUI) return;

        if (useSlideIn && novelUI is NovelPartEventUI novelPartUI)
        {
            await novelPartUI.SlideInBackground(backgroundId);
        }
        else
        {
            await novelUI.ShowBackground(backgroundId);
        }
    }
}
