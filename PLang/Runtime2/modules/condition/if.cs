using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using System.Text.RegularExpressions;

namespace PLang.Runtime2.modules.condition;

[Action("if")]
public partial class If : IContext
{
    public partial string Condition { get; init; }
    public partial GoalCall? GoalIfTrue { get; init; }
    public partial GoalCall? GoalIfFalse { get; init; }

    public async Task<Data> Run()
    {
        bool conditionResult = EvaluateCondition(Condition);
        GoalCall? goalToCall = conditionResult ? GoalIfTrue : GoalIfFalse;

        if (goalToCall != null)
        {
            var result = await Context.Engine!.RunGoalAsync(goalToCall, Context, Context.CancellationToken);
            if (!result.Success) return result;
        }

        return Data.Ok(conditionResult);
    }

    private static bool EvaluateCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return false;

        // Try parsing as plain bool first
        if (bool.TryParse(condition.Trim(), out var boolVal))
            return boolVal;

        // Try comparison operators: !=, ==, >=, <=, >, <, contains
        var match = Regex.Match(condition, @"^(.+?)\s*(!=|==|>=|<=|>|<)\s*(.+)$");
        if (match.Success)
        {
            var left = match.Groups[1].Value.Trim();
            var op = match.Groups[2].Value;
            var right = match.Groups[3].Value.Trim();

            if (double.TryParse(left, out var leftNum) && double.TryParse(right, out var rightNum))
            {
                return op switch
                {
                    ">" => leftNum > rightNum,
                    "<" => leftNum < rightNum,
                    ">=" => leftNum >= rightNum,
                    "<=" => leftNum <= rightNum,
                    "==" => leftNum == rightNum,
                    "!=" => leftNum != rightNum,
                    _ => false
                };
            }

            // String comparison
            return op switch
            {
                "==" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        // Try "contains" operator
        var containsMatch = Regex.Match(condition, @"^(.+?)\s+contains\s+(.+)$", RegexOptions.IgnoreCase);
        if (containsMatch.Success)
        {
            var haystack = containsMatch.Groups[1].Value.Trim();
            var needle = containsMatch.Groups[2].Value.Trim();
            return haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }

        // Non-empty, non-false string is truthy
        return !condition.Equals("0") && !condition.Equals("false", StringComparison.OrdinalIgnoreCase);
    }
}
