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

    // A quoted literal ("…", or '…' when not inside a word, so `don't` is not one); a number that stands
    // alone. These, and a %variable% (the parser's), are plang's own markers — no human language is read.
    private static readonly System.Text.RegularExpressions.Regex Literal = new(@"""([^""]*)""|(?<!\w)'([^']*)'(?!\w)");
    private static readonly System.Text.RegularExpressions.Regex Number = new(@"(?<![\w.])-?\d+(?:\.\d+)?(?![\w.])");
    private static readonly System.Text.RegularExpressions.Regex Quoted = new(@"""(?:[^""\\]|\\.)*""");

    /// <summary>What the step's words hold that its code doesn't: a %variable%, a quoted literal — and what the
    /// code writes that the words don't: a variable, a text. (A number is <see cref="Unwritten"/>'s: words can
    /// give it without digits.) Read after the code dropped what it didn't need to write, so a default left out
    /// doesn't count.</summary>
    public async System.Threading.Tasks.Task<List<string>> Cover(global::app.actor.context.@this context)
    {
        var writer = new global::app.type.format.formal.Writer();
        await Code.Output(writer, global::app.View.Store, context);
        var written = writer.ToString();
        var problems = new List<string>();
        foreach (var v in new global::app.type.item.variable.parser.@this(Text).Variable.Select(x => x.Text).Distinct())
            if (!written.Contains(v)) problems.Add($"step {Index}: {v} is in the step but not in your answer");
        // and the other way: a variable the answer names that the step's words don't is invented — a
        // system variable (%!data%, %!error%) excepted
        foreach (var v in new global::app.type.item.variable.parser.@this(written).Variable.Select(x => x.Text).Distinct())
            if (!v.StartsWith("%!") && !Text.Contains(v)) problems.Add($"step {Index}: {v} isn't in the step — use only the step's variables");
        foreach (var l in Literal.Matches(Text).Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Distinct())
            if (l.Length > 0 && !written.Contains(l)) problems.Add($"step {Index}: \"{l}\" is in the step but not in your answer");
        // and a text the answer writes that the step's words don't hold is invented (channel="X" on a
        // step that names no X) — a choice's option, a number and a dict's keys are not texts
        foreach (var t in (await Texts(Code.Items(), context)).Distinct())
            if (t.Length > 0 && !Text.Contains(t, System.StringComparison.OrdinalIgnoreCase))
                problems.Add($"step {Index}: your answer writes \"{t}\", which the step doesn't — leave out what the step doesn't give");
        return problems;
    }

    /// <summary>The numbers the code writes that the step's words don't write as digits, each with the action and
    /// property writing it — a value the words may give in any language ("retry once"), or an invented one. Only
    /// digits are read: whether the words give it is the decider's to say (<see cref="unwritten.@this"/>).</summary>
    public async System.Threading.Tasks.Task<List<unwritten.@this>> Unwritten(global::app.actor.context.@this context)
    {
        var said = Number.Matches(Text).Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
        var found = new List<unwritten.@this>();
        await Walk(Code.Items());
        return found.Distinct().ToList();

        async System.Threading.Tasks.Task Walk(IEnumerable<global::app.goal.step.action.@this> actions)
        {
            foreach (var a in actions)
            {
                foreach (var p in a.Property)
                {
                    if (p.Value is null || p.Value is global::app.goal.step.action.@this or global::app.goal.step.action.list.@this) continue;
                    var writer = new global::app.type.format.formal.Writer();
                    await p.Value.Output(writer, global::app.View.Store, context);
                    foreach (var n in Number.Matches(Quoted.Replace(writer.ToString(), "")).Select(m => m.Value))
                        if (!said.Contains(double.Parse(n, System.Globalization.CultureInfo.InvariantCulture)))
                            found.Add(new unwritten.@this(Index, $"{a.Module.Name}.{a.Name}", p.Name, n, Text));
                }
                await Walk(a.Held);
                foreach (var child in a.Child.Items()) await Walk(child.Code.Items());
            }
        }
    }

    // Every text property value the code writes, as written (its own Store writer — a template is not
    // rendered): the actions (their clauses among them), the actions they hold (a callback, a recovery), their
    // bodies.
    private async System.Threading.Tasks.Task<List<string>> Texts(
        IEnumerable<global::app.goal.step.action.@this> actions, global::app.actor.context.@this context)
    {
        var texts = new List<string>();
        foreach (var a in actions)
        {
            texts.AddRange(await Texts(a.Held, context));
            foreach (var p in a.Property)
            {
                if (p.Type?.Name != "text" || p.Value is null) continue;
                var writer = new global::app.type.format.formal.Writer();
                await p.Value.Output(writer, global::app.View.Store, context);
                var json = writer.ToString();
                if (json.StartsWith('"') && System.Text.Json.JsonSerializer.Deserialize<string>(json) is { } s) texts.Add(s);
            }
            foreach (var child in a.Child.Items()) texts.AddRange(await Texts(child.Code.Items(), context));
        }
        return texts;
    }
}
