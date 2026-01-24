using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/ShowPortrait")]
public sealed class ShowPortraitEffect : EffectSO
{
    [Header("左立ち絵")]
    [SerializeField] private bool updateLeft;
    [SerializeField] private PortraitState leftPortrait;

    [Header("右立ち絵")]
    [SerializeField] private bool updateRight;
    [SerializeField] private PortraitState rightPortrait;

    public override async UniTask Apply(GameContext context)
    {
        if (context.EventUI is not INovelEventUI novelUI) return;

        var left = updateLeft ? leftPortrait : null;
        var right = updateRight ? rightPortrait : null;

        await novelUI.ShowPortrait(left, right);
    }
}
