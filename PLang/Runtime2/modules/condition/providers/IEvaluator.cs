namespace PLang.Runtime2.modules.condition.providers;

/// <summary>
/// Pluggable comparison engine for condition evaluation.
/// The default implementation is <see cref="DefaultEvaluator"/>.
/// Users can swap this via <c>use library 'custom.dll'</c> containing an <see cref="IEvaluator"/> implementation.
/// </summary>
public interface IEvaluator
{
    /// <summary>
    /// Evaluates a binary expression: <paramref name="left"/> <paramref name="op"/> <paramref name="right"/>.
    /// Supported operators: ==, !=, &gt;, &lt;, &gt;=, &lt;=, contains, startswith, endswith, in, isempty, not, and, or.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="op"/> is not recognized.</exception>
    /// <exception cref="ArgumentException">Thrown when operand types are incompatible with the operator.</exception>
    bool Evaluate(object? left, string op, object? right);

    /// <summary>
    /// Returns whether <paramref name="value"/> is truthy.
    /// null=false, bool as-is, numeric 0=false, empty/whitespace string=false, empty collection=false, everything else=true.
    /// </summary>
    bool IsTruthy(object? value);
}
