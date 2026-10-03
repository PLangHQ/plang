namespace app.goal.step;

// The step judges itself through its action chain — the chain's verdict is the step's cause, and
// the step adds only what it alone knows: which step it is, and whether its code holds what its words
// say.
public sealed partial class @this
{
    /// <summary>What is wrong with this step's code, or null when nothing is. The verdict keeps its
    /// cause's key — an `on error key "ElseWithoutIf"` sees what went wrong, not only that a step did.
    /// Whether an answer holds what the step's words say is the answer's check (<see cref="Cover"/>,
    /// asked where the answer is read): a build may change the code after that (goal.call writes a
    /// callee's name as its address), and the code still holds.</summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Validate(
        global::app.actor.context.@this context)
    {
        var invalid = await Code.Validate(context);
        if (invalid == null) return null;
        var causes = new List<global::app.error.Error> { invalid };
        var first = causes[0];
        return new global::app.error.StepError(
            $"step {Index} '{Text}': {string.Join("; ", causes.Select(c => c.Message))}", this, first.Key, first.Status) { list = causes };
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
        var writer = new global::app.goal.step.action.formal.Writer();
        await Code.Output(writer, global::app.View.Store, context);
        var written = writer.ToString();
        var problems = new List<string>();
        // a variable is the same one whatever its case, as the variable list compares names
        var comparer = global::app.type.item.variable.list.@this.Comparer;
        var said = new global::app.type.item.variable.parser.@this(Text).Variable.Select(x => x.Text).Distinct(comparer).ToList();
        var answered = new global::app.type.item.variable.parser.@this(written).Variable.Select(x => x.Text).Distinct(comparer).ToList();
        foreach (var v in said)
            if (!answered.Contains(v, comparer)) problems.Add($"step {Index}: {v} is in the step but not in your answer");
        // and the other way: a variable the answer names that the step's words don't is invented — a
        // system variable (%!data%, %!error%) excepted
        foreach (var v in answered)
            if (!v.StartsWith("%!") && !said.Contains(v, comparer)) problems.Add($"step {Index}: {v} isn't in the step — use only the step's variables");
        // a quoted literal is held by one of the answer's values, never by the line's own syntax (`Left=%oldHash%`
        // holds no "= %oldHash%"). An answer that doubles a literal's backslashes (\\n for the step's \n) holds a
        // backslash, not what the step says — told so, so the retry writes it as the step does
        var values = await Values(Code.Items(), context);
        foreach (var l in Literal.Matches(Text).Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Distinct())
            if (l.Length > 0 && !values.Any(value => value.Contains(l)))
                problems.Add(l.Contains('\\') && values.Any(value => value.Contains(l.Replace("\\", "\\\\")))
                    ? $"step {Index}: \"{l}\" is in the step, and your answer doubles its backslashes — write each escape as the step does"
                    : $"step {Index}: \"{l}\" is in the step but not in your answer");
        // a number the step writes as digits is one of its markers: an answer without it dropped what the step
        // says (`if %n% is 0` answered with no Right). Present when the digits stand in the answer on their own,
        // not inside a larger number — a duration's PT5S holds the step's 5.
        foreach (var n in Number.Matches(Quoted.Replace(Text, "")).Select(m => m.Value).Distinct())
            if (!System.Text.RegularExpressions.Regex.IsMatch(written, $@"(?<![\d.]){System.Text.RegularExpressions.Regex.Escape(n)}(?![\d.]|\.\d)"))
                problems.Add($"step {Index}: {n} is in the step but not in your answer");
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
                    var writer = new global::app.goal.step.action.formal.Writer();
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

    // Every property value the code writes, each as formal writes it alone (its own Store writer — a template is not
    // rendered, an action held as a value is walked as an action): the actions, the actions they hold, their bodies.
    private async System.Threading.Tasks.Task<List<string>> Values(
        IEnumerable<global::app.goal.step.action.@this> actions, global::app.actor.context.@this context)
    {
        var values = new List<string>();
        foreach (var a in actions)
        {
            values.AddRange(await Values(a.Held, context));
            foreach (var p in a.Property)
            {
                if (p.Value is null or global::app.goal.step.action.@this or global::app.goal.step.action.list.@this) continue;
                var writer = new global::app.goal.step.action.formal.Writer();
                await p.Value.Output(writer, global::app.View.Store, context);
                values.Add(writer.ToString());
            }
            foreach (var child in a.Child.Items()) values.AddRange(await Values(child.Code.Items(), context));
        }
        return values;
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
                var writer = new global::app.goal.step.action.formal.Writer();
                await p.Value.Output(writer, global::app.View.Store, context);
                var json = writer.ToString();
                if (json.StartsWith('"') && System.Text.Json.JsonSerializer.Deserialize<string>(json) is { } s) texts.Add(s);
            }
            foreach (var child in a.Child.Items()) texts.AddRange(await Texts(child.Code.Items(), context));
        }
        return texts;
    }
}
