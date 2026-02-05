using UnityEngine;

public sealed class UnityBattleLogger : IBattleLogger
{
    public void Log(string message)
    {
        Debug.Log(message);
    }

    public void LogWarning(string message)
    {
        Debug.LogWarning(message);
    }

    public void LogError(string message)
    {
        Debug.LogError(message);
    }

    public void LogAssertion(string message)
    {
        Debug.LogAssertion(message);
    }
}
