using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class EffectSO : ScriptableObject
{
    public abstract UniTask Apply(GameContext context);
}