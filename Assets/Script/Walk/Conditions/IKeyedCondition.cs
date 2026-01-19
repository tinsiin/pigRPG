public enum ConditionKeyType
{
    Tag,
    Flag,
    Counter
}

public interface IKeyedCondition
{
    string ConditionKey { get; }
    ConditionKeyType KeyType { get; }
}
