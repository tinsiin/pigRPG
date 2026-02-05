public sealed class NoOpBattleLogger : IBattleLogger
{
    public void Log(string message) { }
    public void LogWarning(string message) { }
    public void LogError(string message) { }
    public void LogAssertion(string message) { }
}
