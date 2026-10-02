namespace app.goal.step.mask;

/// <summary>
/// A step as the decider and the writer read it: each distinct variable a placeholder (<c>%v1%</c>, <c>%v2%</c>, …, in the
/// order the step first writes them; a repeat its first number), every other word untouched —
/// <c>call goal Page module=%!app.module.file%</c> reads <c>call goal Page module=%v1%</c>, so a variable's own words
/// (<c>module</c>, <c>file</c>) are never read as the step's. An answer written in placeholders is restored to the
/// step's variables (<see cref="Restore"/>) before it is read. The placeholders' stem is one the step doesn't use, so a
/// variable of the step's own named like one (<c>%v1%</c>) is never one.
/// </summary>
public sealed class @this
{
    // the step's text this mask was made from
    private readonly string _source;
    // each placeholder and the variable it stands for, in first-appearance order
    private readonly List<(string Placeholder, string Variable)> _map = new();

    internal @this(string source)
    {
        _source = source;
        var variables = new global::app.type.item.variable.parser.@this(source).Variable;
        var stem = "v";
        while (variables.Any(v => System.Text.RegularExpressions.Regex.IsMatch(v.Text, $"^%{System.Text.RegularExpressions.Regex.Escape(stem)}\\d+%$")))
            stem += "_";

        var text = new System.Text.StringBuilder();
        int at = 0;
        foreach (var variable in variables)
        {
            var found = source.IndexOf(variable.Text, at, StringComparison.Ordinal);
            if (found < 0) continue;
            var placeholder = _map.FirstOrDefault(m => m.Variable == variable.Text).Placeholder;
            if (placeholder == null)
            {
                placeholder = $"%{stem}{_map.Count + 1}%";
                _map.Add((placeholder, variable.Text));
            }
            text.Append(source, at, found - at).Append(placeholder);
            at = found + variable.Text.Length;
        }
        Text = text.Append(source, at, source.Length - at).ToString();
    }

    /// <summary>The step's text, its variables placeholders.</summary>
    public string Text { get; }

    /// <summary>The placeholders, in the order the step first writes their variables — what a step's own variables are
    /// offered as.</summary>
    public IReadOnlyList<string> Placeholder => _map.Select(m => m.Placeholder).ToList();

    /// <summary>Whether this mask was made from <paramref name="text"/>.</summary>
    internal bool Of(string text) => string.Equals(_source, text, StringComparison.Ordinal);

    /// <summary><paramref name="line"/>, written in the step's variables, written in placeholders.</summary>
    public string Hide(string line)
    {
        foreach (var (placeholder, variable) in _map.OrderByDescending(m => m.Variable.Length))
            line = line.Replace(variable, placeholder, StringComparison.Ordinal);
        return line;
    }

    /// <summary><paramref name="answer"/>, written in placeholders, written in the step's variables.</summary>
    public string Restore(string answer)
    {
        foreach (var (placeholder, variable) in _map.OrderByDescending(m => m.Placeholder.Length))
            answer = answer.Replace(placeholder, variable, StringComparison.Ordinal);
        return answer;
    }
}
