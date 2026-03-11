namespace PLang.Runtime2.modules.condition.providers;

public interface IEvaluator
{
    bool Evaluate(object? left, string op, object? right);
    bool IsTruthy(object? value);
}
