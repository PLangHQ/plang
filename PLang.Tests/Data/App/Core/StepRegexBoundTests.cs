namespace PLang.Tests.App.Core;

// A step's words are an author's (or a third-party app's) text: every regex the step reads them with is bounded —
// linear (NonBacktracking) or with a match timeout — so no line backtracks for ever.
public class StepRegexBoundTests
{
    [Test] public async Task EveryRegexAStepHolds_IsBounded()
    {
        var regexes = typeof(global::app.goal.step.@this)
            .GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(System.Text.RegularExpressions.Regex))
            .Select(f => (f.Name, Regex: (System.Text.RegularExpressions.Regex)f.GetValue(null)!))
            .ToList();
        await Assert.That(regexes.Count).IsGreaterThan(0);

        var unbounded = regexes.Where(r => !r.Regex.Options.HasFlag(System.Text.RegularExpressions.RegexOptions.NonBacktracking)
                                           && r.Regex.MatchTimeout == System.Text.RegularExpressions.Regex.InfiniteMatchTimeout)
            .Select(r => r.Name).ToList();
        await Assert.That(unbounded).IsEmpty();
    }
}
