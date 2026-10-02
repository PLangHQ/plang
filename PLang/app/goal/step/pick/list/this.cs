using number = global::app.type.item.number.@this;
using Kind = global::app.goal.step.pick.question.Kind;

namespace app.goal.step.pick.list;

/// <summary>
/// The decider's reading of one step (<c>step.Pick</c>): what it answered about the step, and the picks
/// that answer means. Build-time only — never stored in the .pr.
///
/// <para>The decider is asked in two stages against one shared state. Stage 1 asks, per step, ONE
/// choice (which module does the main work) and a yes/no per common action. Stage 2 asks what stage 1
/// leaves open: the main module's action (unless a near-certain common action of it already answers
/// it), a runner-up module whose share of the choice is at least 0.2, whether the step uses the main
/// module when it scored under 0.9 (and the runner-up), an if's branches by name, and — on a step
/// stage 1 was unsure of (nothing scored 0.8) — which one of the popular actions it uses.</para>
///
/// <para>The list owns the question ids (<c>s{index}_{question}</c>, <see cref="Key"/>) and takes
/// whatever answers carry its step's ids (<see cref="Take"/>) — there is no stage flag: a stage-1
/// answer and a stage-2 answer are told apart by their ids. It decides WHICH stage-2 questions are
/// asked (<see cref="Question"/>) from the same numbers its picks read; the decider template writes
/// their words.</para>
/// </summary>
// Not itself enumerable: a template reads it by member (step.Pick.Question, step.Pick.Item), and a
// template engine turns anything enumerable into a bare array, losing its members.
public sealed class @this
{
    // The decider's cuts: a common action scored at or above Near is certain, and its module skips stage 2;
    // a choice's second module at or above Runner is asked too; a step whose best score is under Sure is
    // asked the popular choice; a picked if at or above Possible asks its branches by name.
    private const double Near = 0.9, Runner = 0.2, Sure = 0.8, Possible = 0.5;

    private readonly global::app.goal.step.@this _step;

    // What the decider answered about this step, by question.
    private readonly Dictionary<string, number?> _module = new();                        // stage 1: module → its share of the choice
    private readonly Dictionary<string, number?> _named = new();                         // stage 1: common action → yes/no
    private readonly Dictionary<string, (string? Action, number? Confidence)> _choice = new();   // stage 2: module → its action
    private readonly Dictionary<string, number?> _also = new();                          // stage 2: module → does the step use it
    private readonly Dictionary<string, number?> _branch = new();                        // stage 2: branch → yes/no
    private Dictionary<string, number?>? _popular;                                       // stage 2: popular action → share; null when not asked
    // stage 2: module.action.Option → the offer chosen for it, with the option it is for
    private readonly Dictionary<string, (global::app.type.property.@this Property, global::app.type.item.@this Value)> _option = new();

    private List<pick.@this> _items = new();
    private List<question.@this> _question = new();

    // The app's modules by name, awaited once when the answer is taken — what the picks are made from.
    private Dictionary<string, global::app.module.@this> _modules = new(StringComparer.OrdinalIgnoreCase);

    public @this(global::app.goal.step.@this step) => _step = step;

    /// <summary>The id of one of this step's questions: <c>s{index}_{question}</c>.</summary>
    public string Key(string question) => $"s{_step.Index}_{question}";

    /// <summary>The picks: each action the decider picked for the step, with its score.</summary>
    public IReadOnlyList<pick.@this> Item => _items;

    /// <summary>What stage 2 asks of this step — the questions stage 1 leaves open, by subject.</summary>
    public IReadOnlyList<question.@this> Question => _question;

    /// <summary>The modules stage 2 is about for this step: the main one and a runner-up (unless a
    /// near-certain common action answers them) — its state shows them in full.</summary>
    public IReadOnlyList<global::app.module.@this> Module => _moduleAsked;

    /// <summary>The step tests a condition: an if was picked, so its branches are asked by name.</summary>
    public bool IsCondition { get; private set; }

    /// <summary>Stage 1 was unsure of the step: it is offered the popular actions.</summary>
    public bool IsUnsure { get; private set; }

    private List<global::app.module.@this> _moduleAsked = new();

    /// <summary>What the prompt lists for the step, most certain first: the picks at 0.5 or more,
    /// the variable.set of a <c>write to %x%</c> whatever it scored, and on an unsure step the
    /// popular choice's options at the choice floor (0.2) or more — each with its mark.</summary>
    public IReadOnlyList<listed.@this> Listed => _listed;

    /// <summary>The step's starting formal: its certain actions — a condition chain first, in chain
    /// order, the rest by score — <c>?</c> where a value is still needed, a certain clause right after
    /// the first action, a <c>write to %x%</c> filled in last.
    /// Null when nothing is certain.</summary>
    public string? Formal { get; private set; }

    private List<listed.@this> _listed = new();

    /// <summary>The code the step's certain picks already know, before the LLM answers — what the
    /// build's walk reads (<c>goal.step.list.Scope</c>): a certain loop.foreach over the step's first
    /// variable binding its item (<c>as %i%</c>, else <c>%item%</c>) first, then each other certain
    /// action as it is (its values still to fill), a certain <c>set %x% = </c>one literal or one variable,
    /// and a <c>write to %x%</c> last. Empty when nothing is certain.</summary>
    public global::app.goal.step.action.list.@this Known { get; private set; } = new();

    // The step's words the known code reads: its first variable, a foreach's `as` name, and a
    // `%x% = ` that ends in one literal (quoted text, a number, true/false) or one variable. The
    // variables are the parser's; the words around them are these.
    private static readonly System.Text.RegularExpressions.Regex As =
        new(@"\bas\s+%?([A-Za-z_]\w*)%?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex Assigns = new(@"^\s*=(?!=)");
    private static readonly System.Text.RegularExpressions.Regex AssignsOne =
        new(@"^\s*=\s*(""(?:[^""\\]|\\.)*""|-?\d+(?:\.\d+)?|true|false|%\S+%)\s*$");

    // The step's variables in the order its words write them, each with where it ends in the words.
    private IEnumerable<(global::app.type.item.variable.@this Variable, int End)> Placed()
    {
        var text = _step.Text;
        int from = 0;
        foreach (var v in new global::app.type.item.variable.parser.@this(text).Variable)
        {
            from = text.IndexOf(v.Text, from, StringComparison.Ordinal) + v.Text.Length;
            yield return (v, from);
        }
    }

    // The step's first variable that names a value by members.
    private global::app.type.item.variable.@this? First()
        => Placed().Select(p => p.Variable).FirstOrDefault(v => v.IsMembers);

    // The variable an assignment writes: `%x% = …` (not `==`), whatever it is set to.
    private global::app.type.item.variable.@this? Target()
        => Placed().FirstOrDefault(p => p.Variable.IsBare && Assigns.IsMatch(_step.Text[p.End..])).Variable;

    // `%x% = ` ending in one literal or one bare variable: the variable and what it is set to.
    private (string Name, string Value)? Assigned()
    {
        foreach (var (v, end) in Placed())
        {
            if (!v.IsBare || AssignsOne.Match(_step.Text[end..]) is not { Success: true } m) continue;
            var value = m.Groups[1].Value;
            if (value.StartsWith('%')
                && new global::app.type.item.variable.parser.@this(value).Whole is not { IsBare: true }) continue;
            return (v.Text, value);
        }
        return null;
    }

    // Every action in the code, wherever it sits: the step's actions, the actions they hold, their
    // clauses' recovery, their bodies.
    private IEnumerable<global::app.goal.step.action.@this> Every(IEnumerable<global::app.goal.step.action.@this> actions)
    {
        foreach (var a in actions)
        {
            yield return a;
            foreach (var h in Every(a.Held)) yield return h;
            foreach (var child in a.Child.Items())
                foreach (var c in Every(child.Code.Items())) yield return c;
        }
    }

    /// <summary>The variable the step's words say it writes — a <c>write to %x%</c>, or the <c>%x%</c> of a
    /// <c>%x% = …</c> on a step that tests no condition (there `=` compares). Null when the words say none.</summary>
    private string? Writes()
    {
        if (Destination() is { } known) return known.Text;
        return !Tests && Target() is { } target ? target.Text : null;
    }

    // The step's words that fill a value the decider can't: `write to %x%` is a known variable.set, `on
    // error … call` names what a recovery runs, `call X name=value` passes arguments.
    private static readonly System.Text.RegularExpressions.Regex WriteTo =
        new(@"write to\s+(?=%)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // The variable a `write to %x%` names.
    private global::app.type.item.variable.@this? Destination()
        => WriteTo.Match(_step.Text) is { Success: true } m
            ? new global::app.type.item.variable.parser.@this(_step.Text).Read(m.Index + m.Length)
            : null;

    /// <summary>The top three of the popular choice (when the step was asked it), most probable first.</summary>
    public IReadOnlyList<(string Name, number? Score)> Top =>
        _popular == null ? [] : _popular.OrderByDescending(p => p.Value ?? (number)0.0).Take(3).Select(p => (p.Key, p.Value)).ToList();

    /// <summary>Takes this step's answers out of a decider answer (<c>{id: {choice, confidence,
    /// probabilities, noul}}</c>) — every id of its own, whichever stage asked it — and works out the
    /// picks and the stage-2 questions again. <paramref name="popular"/> is the popular actions an
    /// unsure step is offered.</summary>
    public async Task Take(dict answer, IEnumerable<string> popular, global::app.actor.context.@this context)
    {
        // A cached step is already built, and a step written in formal is its code: the decider is
        // asked nothing about either.
        if (_step.IsCached || _step.IsFormal) return;
        _modules = context.App.module.list.Items().ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var prefix = Key("");
        foreach (var entry in answer.Entries(context))
        {
            if (!entry.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (await entry.Value() is not dict a) continue;
            var id = entry.Name[prefix.Length..];
            var noul = a.Get<number>("noul", context);
            if (id == "@module") await Share(a, context, _module);
            else if (id == "@popular") await Share(a, context, _popular = new());
            else if (id.StartsWith("@also.", StringComparison.Ordinal)) _also[id["@also.".Length..]] = noul;
            else if (id.StartsWith(OptionKey, StringComparison.Ordinal)) await Choose(id[OptionKey.Length..], await Choice(a, context), context);
            else if (Branch(context).Any(b => $"{b.Module.Name}.{b.Name}" == id)) _branch[id] = noul;
            else if (id.Contains('.')) _named[id] = noul;
            else _choice[id] = (await Choice(a, context), a.Get<number>("confidence", context));
        }
        _items = Picks(context);
        _question = await Questions(popular, context);
        _moduleAsked = Asked(context).Select(m => _modules[m]).ToList();
        IsCondition = Tests;
        IsUnsure = Unsure(context);
        _listed = Listing(context);
        Formal = Prefill(context);
        Known = await Code(context.App.module.list, context);
    }

    // ---------------------------------------------------------------- the answer against the picks

    /// <summary>Where the step's code and the decider disagree: what refuses the code (a certain pick
    /// left out, an action the decider did not list, an action held as a value that is not listed —
    /// goal.call excepted), and the warnings it builds with (an action the decider was not sure of,
    /// one from the popular choice).</summary>
    public (List<string> Refused, List<global::app.warning.@this> Warning) Agree(global::app.goal.step.action.list.@this code)
    {
        var i = _step.Index;
        var used = code.Own.Distinct().ToList();
        // a certain action may sit anywhere in the step — `on error set %x% = …` sets inside the recovery
        var every = Every(code.Items()).ToList();
        var anywhere = every.Select(a => $"{a.Module.Name}.{a.Name}").Concat(used).ToHashSet();
        var refused = _listed.Where(l => l.Mark == listed.Mark.Certain && !anywhere.Contains(l.Name))
            .Select(l => $"step {i} leaves out {l.Name}, which the decider is certain of ({l.Shown})").ToList();
        // an option the decider says the step gives is in the code: chosen and left out refuses it, as a certain action
        // left out does — unless the value chosen is the option's default, which the code holds by leaving it out
        foreach (var (key, (property, value)) in _option)
        {
            var name = key[..key.LastIndexOf('.')];
            var built = every.Where(a => $"{a.Module.Name}.{a.Name}" == name).ToList();
            if (built.Count > 0 && built.All(a => a[property.Name] == null) && !property.IsDefault(value))
            {
                var option = new global::app.goal.step.action.formal.Writer();
                option.Option(property.Name, value);
                refused.Add($"step {i}'s {name} leaves out {property.Name}, which the decider says the step gives: write {option}");
            }
        }
        refused.AddRange(Unlisted(used));
        refused.AddRange(Held(code.Items()).Distinct().Where(a => a != "goal.call" && _listed.All(l => l.Name != a))
            .Select(a => $"step {i} holds {a}, which is not listed; only goal.call may be held without being listed"));
        // the variable the step's words write is written by a variable.set somewhere in the step's code
        if (Writes() is { } written && !every.Any(a => a.Module.Name == "variable" && a.Name == "set"
                && string.Equals(a["Name"]?.Value?.ToString()?.Trim('%', '"'), written.Trim('%'), StringComparison.OrdinalIgnoreCase)))
            refused.Add($"step {i} says it writes {written}, but no action writes it: end the step with variable.set(Name={written}, Value=%!data%)");
        var warnings = new List<global::app.warning.@this>();
        foreach (var l in _listed.Where(l => used.Contains(l.Name)))
            if (l.Mark == listed.Mark.Possible)
                warnings.Add(new() { Key = "Unsure", Message = $"step {i} uses {l.Name}, which the decider was not sure of ({l.Shown})" });
            else if (l.Mark == listed.Mark.Popular)
                warnings.Add(new() { Key = "Popular", Message = $"step {i} uses {l.Name} from the decider's popular-action choice ({l.Shown}): unsure" });
        return (refused, warnings);
    }

    /// <summary>The actions named that the decider did not list for the step — each refusal names the
    /// step's list, so the retry has what it needs.</summary>
    public IEnumerable<string> Unlisted(IEnumerable<string> names) =>
        names.Distinct().Where(a => _listed.All(l => l.Name != a))
            .Select(a => $"{a} isn't one of step {_step.Index}'s actions ({string.Join(", ", _listed.Select(l => l.Name))})");

    // The actions held as values: a property's action, a recovery's actions — and what those hold.
    private static IEnumerable<string> Held(IEnumerable<global::app.goal.step.action.@this> actions)
    {
        foreach (var a in actions)
        {
            foreach (var held in a.Held)
            {
                yield return $"{held.Module.Name}.{held.Name}";
                foreach (var name in Held([held])) yield return name;
            }
            foreach (var child in a.Child.Items())
                foreach (var name in Held(child.Code.Items())) yield return name;
        }
    }

    // ---------------------------------------------------------------- what the prompt shows

    private List<listed.@this> Listing(global::app.actor.context.@this context)
    {
        // how sure stage 1 was of each module stage 2 asked about — shown beside an action picked through one
        var modules = string.Join(", ", Asked(context).Select(m =>
            $"{m} {((double)(_module.GetValueOrDefault(m) ?? (number)0.0)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}"));
        var shown = new List<(string Name, number Score)>();
        foreach (var p in _items)
            if (p.Score is { } s && s >= (number)Possible) shown.Add((p.Name, s));
        var known = Destination() != null;
        if (known && shown.All(p => p.Name != "variable.set"))
            shown.Add(("variable.set", _items.FirstOrDefault(p => p.Name == "variable.set")?.Score ?? (number)0.0));
        var popular = Popular();
        foreach (var (name, score) in popular)
            if (shown.All(p => p.Name != name)) shown.Add((name, score));
        // an action picked through its module is certain only when its module is too: its score is its certainty
        // within the module, and a module the decider is unsure of leaves the step's words to decide
        bool Through(string name) => _items.FirstOrDefault(i => i.Name == name)?.From is From.YesNo or From.Choice;
        bool Sure(string name) => !Through(name)
            || (_module.GetValueOrDefault(name.Split('.')[0]) ?? (number)0.0) >= (number)Near;
        // most certain first; equal scores keep their order
        return shown.OrderByDescending(p => p.Score).Select(p => new listed.@this
        {
            Name = p.Name, Score = p.Score,
            Mark = p.Score >= (number)Near && Sure(p.Name) ? listed.Mark.Certain
                 : p.Name == "variable.set" && known ? listed.Mark.WriteTo
                 : popular.ContainsKey(p.Name) && !(_items.FirstOrDefault(i => i.Name == p.Name)?.Score is { } own && own >= (number)Possible)
                     ? listed.Mark.Popular
                 : listed.Mark.Possible,
            Module = modules.Length > 0 && Through(p.Name) ? modules : null,
            Option = Chosen(Catalog(p.Name, context)).Select(c => c.Written).ToList(),
        }).ToList();
    }

    // The popular choice's options at the choice floor or more.
    private Dictionary<string, number> Popular() =>
        (_popular ?? new()).Where(p => (p.Value ?? (number)0.0) >= (number)Runner).ToDictionary(p => p.Key, p => p.Value ?? (number)0.0);

    private string? Prefill(global::app.actor.context.@this context)
    {
        var known = Destination();
        var certain = _listed.Where(l => l.Mark == listed.Mark.Certain && !(l.Name == "variable.set" && known != null))
            .Select(l => Catalog(l.Name, context)).Where(a => a != null).Select(a => a!).ToList();
        // a lone if — no elseif or else listed, nothing indented below the step — holds the step's other actions in
        // its { }
        var chain = _listed.Any(l => l.Name is "condition.elseif" or "condition.else");
        var below = _step.Goal is { } goal && _step.Index < goal.Step.CountRaw
                    && ReferenceEquals(goal.Step[_step.Index], _step) && goal.Step.Body(_step.Index).CountRaw > 0;
        var nests = certain.Count(a => a.Link == 0) == 1 && !chain && !below;
        // each certain action takes its own place: the test a lone if is asked of before it (a step whose answer goes
        // to its own destination has no such test), the chain in its order, the rest after — a tie in the scores
        // never puts a body's action before its if
        certain = certain.OrderBy(a => a.Place(nests && known == null)).ToList();
        var line = new line.@this(nests);
        foreach (var action in certain) action.Prefill(line, Call(action));
        if (known != null) line.Append($"variable.set(Name={known.Text}, Value=%!data%)");
        return line.ToString();
    }

    // The known code, written in formal and read the way an answer is read. A line that doesn't read
    // knows nothing.
    private async Task<global::app.goal.step.action.list.@this> Code(
        global::app.type.item.list.@this<global::app.module.@this> modules, global::app.actor.context.@this context)
    {
        var text = _step.Text;
        var known = Destination();
        var certain = _listed.Where(l => l.Mark == listed.Mark.Certain).Select(l => Catalog(l.Name, context))
            .Where(a => a != null).Select(a => a!).ToList();
        var line = new List<string>();
        // each certain action adds itself (a loop leads: what follows it is its body)
        foreach (var action in certain)
        {
            var name = $"{action.Module.Name}.{action.Name}";
            if (name == "loop.foreach")
            {
                if (First() is not { } collection) continue;
                var item = As.Match(text) is { Success: true } named ? named.Groups[1].Value : "item";
                action.Know(line, $"loop.foreach(Collection={collection.Text}, Item=%{item}%)");
            }
            else if (name == "variable.set")
            {
                if (known != null || Assigned() is not { } set) continue;
                line.Add($"variable.set(Name={set.Name}, Value={set.Value})");
            }
            else action.Know(line, $"{name}()");
        }
        if (known != null) line.Add($"variable.set(Name={known.Text}, Value=%!data%)");
        if (line.Count == 0) return new();
        var read = new global::app.goal.step.action.formal.Reader(_step, modules).Read(string.Join("; ", line), context);
        if (read.Success) return (global::app.goal.step.action.list.@this)read.Peek()!;
        await (context.App.Debug?.Write($"build.pick: step {_step.Index}'s known code does not read: {read.Error?.Message}") ?? Task.CompletedTask);
        return new();
    }

    // One action as the pre-fill starts it: its required properties by name alone — a slot still to fill, nothing
    // the LLM could copy as a value — then each option the decider chose a value of (`Template=plang`; "none" leaves
    // it out). Any other optional property gets no slot — it is the LLM's to add when the step names it (a Recovery,
    // a Parameter, a RetryCount), as the examples teach.
    private string Call(global::app.goal.step.action.@this action)
    {
        var chosen = Chosen(action);
        // a required property the decider chose a value of is written with it, not as a slot still to fill
        var given = action.Property.Where(p => p.Required && chosen.All(c => c.Property != p))
            .Select(p => p.Name).Concat(chosen.Select(c => c.Written));
        return $"{action.Module.Name}.{action.Name}({string.Join(", ", given)})";
    }

    // The options the decider chose a value of for <paramref name="action"/>, each as formal writes an option
    // (`Template="plang"`, `Item=%value%`, `Name="Page"`, a bare `Permission` the writer fills). What the starting line
    // writes and what the listed action carries: one reading, so they never drift.
    private List<(global::app.type.property.@this Property, string Written)> Chosen(global::app.goal.step.action.@this? action)
    {
        if (action == null) return [];
        var asked = $"{action.Module.Name}.{action.Name}.";
        var chosen = new List<(global::app.type.property.@this, string)>();
        foreach (var (key, (property, value)) in _option)
            if (key.StartsWith(asked, StringComparison.Ordinal))
            {
                var writer = new global::app.goal.step.action.formal.Writer();
                writer.Option(property.Name, value);
                chosen.Add((property, writer.ToString()));
            }
        return chosen;
    }

    // The option `key` (module.action.Option) takes the offer the decider chose — the one shown as `choice`; "none", or
    // a text no offer shows, leaves the option out.
    private async Task Choose(string key, string? choice, global::app.actor.context.@this context)
    {
        _option.Remove(key);
        var at = key.LastIndexOf('.');
        if (choice is null or question.@this.None || at < 0 || Catalog(key[..at], context) is not { } action
            || action[key[(at + 1)..]] is not { } property) return;
        foreach (var offer in await Offered(action, property, context))
            if (Shown(offer, context) == choice) { _option[key] = (property, offer); return; }
    }

    // What `property` of `action` is offered in this step: what its type offers (a closed set its options, a
    // collection the members the step can name, any other the step's variables) — but never a variable the action
    // itself writes: the step's write-to, where its answer goes, or one the action binds when the step names none
    // (foreach's %item%), which is choosing none. The question shows these, and a pick is matched against these.
    private async Task<List<global::app.type.item.@this>> Offered(global::app.goal.step.action.@this action,
        global::app.type.property.@this property, global::app.actor.context.@this context)
    {
        var written = action.Bound.Cast<global::app.type.item.@this>().Append(Destination())
            .OfType<global::app.type.item.@this>().Select(v => Shown(v, context)).ToHashSet(StringComparer.Ordinal);
        return (await property.Type.Offers(_step)).Where(o => !written.Contains(Shown(o, context))).ToList();
    }

    // An offer as the decider is shown it: its text, as a text channel writes it (`%field%`, `plang`, `Page`).
    private string Shown(global::app.type.item.@this offer, global::app.actor.context.@this context)
    {
        using var stream = new System.IO.MemoryStream();
        offer.Write(new global::app.type.item.text.Writer(stream, System.Text.Encoding.UTF8,
            context.Setting.Of<global::app.setting.@this>().Culture));
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private global::app.goal.step.action.@this? Catalog(string name, global::app.actor.context.@this context)
    {
        var parts = name.Split('.', 2);
        return _modules.TryGetValue(parts[0], out var module) ? module[parts[1]] : null;
    }

    // A choice's answer as each option's share: its probabilities, or its pick at its confidence.
    private async Task Share(dict answer, global::app.actor.context.@this context, Dictionary<string, number?> into)
    {
        if (answer.Get<dict>("probabilities", context) is { } probabilities)
        {
            foreach (var option in probabilities.Entries(context))
                into[option.Name] = await option.Value() as number;
            return;
        }
        if (await Choice(answer, context) is { } choice)
            into[choice] = answer.Get<number>("confidence", context);
    }

    // A choice's pick, read through its slot's own door.
    private async Task<string?> Choice(dict answer, global::app.actor.context.@this context)
        => answer.Get("choice", context) is { } slot ? (await slot.Value())?.ToString() : null;

    // The actions that may follow an if — the condition module's own branches (action.IsBranch).
    private List<global::app.goal.step.action.@this> Branch(global::app.actor.context.@this context)
    {
        var condition = _modules["condition"];
        return condition.ActionNames.OrderBy(a => a, StringComparer.Ordinal)
            .Select(a => condition[a]!).Where(a => a.IsBranch).ToList();
    }

    // ---------------------------------------------------------------- what stage 1 means

    // The modules of the choice, most probable first — only modules the app has.
    private List<(string Module, number Share)> Ranked(global::app.actor.context.@this context) =>
        _module.Where(m => m.Value is not null && _modules.ContainsKey(m.Key))
               .Select(m => (m.Key, m.Value!))
               .OrderByDescending(m => m.Item2)
               .ToList();

    // The modules a near-certain common action already answers.
    private HashSet<string> Settled =>
        _named.Where(a => a.Value is { } p && p >= (number)Near).Select(a => a.Key.Split('.')[0]).ToHashSet();

    // The modules stage 2 asks the action of: the main one and a runner-up at or above Runner, each
    // unless settled.
    private List<string> Asked(global::app.actor.context.@this context)
    {
        var ranked = Ranked(context);
        var settled = Settled;
        var asked = ranked.Take(1).Where(m => !settled.Contains(m.Module)).Select(m => m.Module).ToList();
        asked.AddRange(ranked.Skip(1).Take(1).Where(m => m.Share >= (number)Runner && !settled.Contains(m.Module)).Select(m => m.Module));
        return asked;
    }

    // The modules stage 2 asks "does the step use it" of: the main one under Near (its share of the
    // "main work" choice is not whether it is used), and the runner-up.
    private List<string> YesNo(global::app.actor.context.@this context)
    {
        var ranked = Ranked(context);
        return Asked(context).Where(m => m != ranked[0].Module || ranked[0].Share < (number)Near).ToList();
    }

    // A picked if asks its branches by name.
    private bool Tests => _named.TryGetValue("condition.if", out var p) && p is { } s && s >= (number)Possible;

    // Stage 1 was unsure of the step: neither a common action nor a module scored Sure.
    private bool Unsure(global::app.actor.context.@this context)
    {
        var scores = _named.Values.Where(p => p is not null).Concat(Ranked(context).Select(m => (number?)m.Share)).ToList();
        return scores.Count == 0 || scores.Max()! < (number)Sure;
    }

    // ---------------------------------------------------------------- the picks

    private List<pick.@this> Picks(global::app.actor.context.@this context)
    {
        var picks = _named.Where(a => a.Value is not null)
            .Select(a => new pick.@this { Name = a.Key, Score = a.Value, From = From.Common }).ToList();
        var yesNo = YesNo(context);
        foreach (var m in Asked(context))
        {
            var module = _modules[m];
            string? action = module.Count == 1 ? module.ActionNames.Single() : _choice.GetValueOrDefault(m).Action;
            if (action == null) continue;
            var name = $"{m}.{action}";
            // a common action keeps its own stage-1 score — the one the Near rule reads
            if (picks.Any(p => p.Name == name)) continue;
            var asked = yesNo.Contains(m);
            picks.Add(new pick.@this
            {
                Name = name,
                Score = asked ? _also.GetValueOrDefault(m) : _module.GetValueOrDefault(m),
                From = asked ? From.YesNo : From.Choice,
            });
        }
        foreach (var branch in Branch(context).Select(b => $"{b.Module.Name}.{b.Name}").Where(_branch.ContainsKey))
            picks.Add(new pick.@this { Name = branch, Score = _branch[branch], From = From.Branch });
        return picks;
    }

    // ---------------------------------------------------------------- stage 2's questions

    // An option question's id: @option.{module}.{action}.{option} — apart from a common action's ({module}.{action}).
    private const string OptionKey = "@option.";

    private async Task<List<question.@this>> Questions(IEnumerable<string> popular, global::app.actor.context.@this context)
    {
        var questions = new List<question.@this>();
        if (Unsure(context))
            questions.Add(new question.@this
            {
                Id = Key("@popular"), Kind = Kind.Popular,
                Option = popular.Select(a => a.Split('.')).Select(a => _modules[a[0]][a[1]]!).ToList(),
            });
        if (Tests)
            foreach (var branch in Branch(context))
                questions.Add(new question.@this { Id = Key($"{branch.Module.Name}.{branch.Name}"), Kind = Kind.Branch, Branch = branch });
        var asked = Asked(context);
        foreach (var m in YesNo(context))
            questions.Add(new question.@this { Id = Key($"@also.{m}"), Kind = Kind.Use, Module = _modules[m], Also = asked[0] != m });
        foreach (var m in asked)
        {
            var module = _modules[m];
            if (module.Count == 1) continue;   // one action: nothing to decide
            questions.Add(new question.@this
            {
                Id = Key(m), Kind = Kind.Action, Module = module,
                Option = module.ActionNames.OrderBy(a => a, StringComparer.Ordinal).Select(a => module[a]!).ToList(),
            });
        }
        // the options whose notes ask: of each action certain from stage 1, and of every action of each module asked
        // its action here (a one-action module's one) — answered beside it; the starting line uses an answer only
        // when its action ends up certain
        var actions = _items.Where(p => p.Score is { } s && s >= (number)Near).Select(p => Catalog(p.Name, context))
            .Concat(asked.SelectMany(m => _modules[m].ActionNames.Select(a => _modules[m][a])))
            .Where(a => a != null).Select(a => a!).Distinct();
        foreach (var action in actions)
        {
            await action.Note.Value(context.Ok());
            foreach (var line in action.Note.Line)
            {
                if (line is not { Ask: not null, Name: { } named } || action[named.ToString()] is not { } property) continue;
                // an option with nothing to offer isn't asked
                var offers = (await Offered(action, property, context)).Select(o => Shown(o, context)).ToList();
                if (offers.Count == 0) continue;
                questions.Add(new question.@this
                {
                    Id = Key($"{OptionKey}{action.Module.Name}.{action.Name}.{property.Name}"), Kind = Kind.Option,
                    Action = action, Property = property, Values = [.. offers, question.@this.None],
                });
            }
        }
        return questions;
    }
}
