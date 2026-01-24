using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/PlayNoise")]
public sealed class PlayNoiseEffect : EffectSO
{
    [SerializeField] private NoiseEntry[] noises;

    public override UniTask Apply(GameContext context)
    {
        if (context.EventUI is not INovelEventUI novelUI) return UniTask.CompletedTask;
        if (noises == null || noises.Length == 0) return UniTask.CompletedTask;

        novelUI.PlayNoise(noises);

        return UniTask.CompletedTask;
    }
}
