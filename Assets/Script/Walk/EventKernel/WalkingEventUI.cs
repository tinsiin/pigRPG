using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class WalkingEventUI : IEventUI
{
    private readonly Walking walking;
    private readonly MessageDropper messageDropper;

    public WalkingEventUI(Walking walking, MessageDropper messageDropper)
    {
        this.walking = walking;
        this.messageDropper = messageDropper;
    }

    public void ShowMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (messageDropper == null)
        {
            Debug.LogWarning("WalkingEventUI.ShowMessage: MessageDropper is null.");
            return;
        }

        messageDropper.CreateMessage(message);
    }

    public UniTask<int> ShowChoices(string[] labels, string[] ids)
    {
        if (walking == null)
        {
            Debug.LogWarning("WalkingEventUI.ShowChoices: Walking is null.");
            return UniTask.FromResult(-1);
        }

        return walking.CreateAreaButton(labels, ids);
    }
}