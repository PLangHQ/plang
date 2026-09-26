namespace app.goal.step;

// The step judges itself through its action chain — the chain's verdict is the step's cause, and
// the step adds only what it alone knows: which step it is, and whether its code holds what its words
// say.
public sealed partial class @this
{
    /// <summary>What is wrong with this step's code, or null when nothing is. The verdict keeps its
    /// cause's key — an `on error key "ElseWithoutIf"` sees what went wrong, not only that a step did.
    /// Whether an answer holds what the step's words say is the answer's check (<see cref="Cover"/>,
    /// asked where the answer is read): a build may change the code after that (goal.call drops a
    /// redundant `x=%x%`), and the code still holds.</summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Validate(
        global::app.actor.context.@this context)
    {
        var invalid = await Code.Validate(context);
        if (invalid == null) return null;
        var causes = new List<global::app.error.Error> { invalid };
        var first = causes[0];
        return new global::app.error.StepError(
            $"step {Index} '{Text}': {string.Join("; ", causes.Select(c => c.Message))}", this, first.Key, first.StatusCode) { list = causes };
    }

    // A %variable% in the step's words; a quoted literal ("…", or '…' when not inside a word, so `don't`
    // is not one); a number that stands alone. These are plang's own markers — no human language is read.
    private static readonly System.Text.RegularExpressions.Regex Marker = new(@"%[^%\s]+%");
    private static readonly System.Text.RegularExpressions.Regex Literal = new(@"""([^""]*)""|(?<!\w)'([^']*)'(?!\w)");
    private static readonly System.Text.RegularExpressions.Regex Number = new(@"(?<![\w.])-?\d+(?:\.\d+)?(?![\w.])");
    private static readonly System.Text.RegularExpressions.Regex Quoted = new(@"""(?:[^""\\]|\\.)*""");

    /// <summary>What the step's words hold that its code doesn't: a %variable%, a quoted literal — and a
    /// number its code writes that its words don't (an invented value, RetryCount=1 on a step that
    /// retries nothing). Read after the code dropped what it didn't need to write, so a default left out
    /// doesn't count.</summary>
    public async System.Threading.Tasks.Task<List<string>> Cover(global::app.actor.context.@this context)
    {
        var writer = new global::app.channel.serializer.formal.Writer();
        await Code.Output(writer, global::app.View.Store, context);
        var written = writer.ToString();
        var problems = new List<string>();
        foreach (var v in Marker.Matches(Text).Select(m => m.Value).Distinct())
            if (!written.Contains(v)) problems.Add($"step {Index}: {v} is in the step but not in your answer");
        // and the other way: a variable the answer names that the step's words don't is invented — a
        // system variable (%!data%, %!error%) excepted
        foreach (var v in Marker.Matches(written).Select(m => m.Value).Distinct())
            if (!v.StartsWith("%!") && !Text.Contains(v)) problems.Add($"step {Index}: {v} isn't in the step — use only the step's variables");
        foreach (var l in Literal.Matches(Text).Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Distinct())
            if (l.Length > 0 && !written.Contains(l)) problems.Add($"step {Index}: \"{l}\" is in the step but not in your answer");
        var said = Number.Matches(Text).Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
        foreach (var n in Number.Matches(Quoted.Replace(written, "")).Select(m => m.Value).Distinct())
            if (!said.Contains(double.Parse(n, System.Globalization.CultureInfo.InvariantCulture)))
                problems.Add($"step {Index}: your answer writes {n}, which the step doesn't — leave out what the step doesn't give");
        return problems;
    }
}
