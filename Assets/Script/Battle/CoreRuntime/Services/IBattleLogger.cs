public interface IBattleLogger
{
    void Log(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogAssertion(string message);
}
