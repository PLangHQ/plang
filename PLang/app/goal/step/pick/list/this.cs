using number = global::app.type.item.number.@this;

namespace app.goal.step.pick.list;

/// <summary>
/// The decider's reading of one step (<c>step.Pick</c>): what it answered about the step, and the picks
/// that answer means. Build-time only — never stored in the .pr.
///
/// <para>The decider is asked in two stages against one shared state. Stage 1 asks, per step, ONE
/// choice (which module does the main work) and a yes/no per common action. Stage 2 asks what stage 1
/// leaves open: the main module's action (unless a near-certain common action of it already answers
/// it), a runner-up module whose share of the choice is at least 0.2, whether the step uses the main
/// module when it scored under 0.9 (and the runner-up), elseif/else by name after a picked if, and —
/// on a step stage 1 was unsure of (nothing scored 0.8) — which one of the popular actions it uses.</para>
///
/// <para>The list owns the question ids (<c>s{index}_{question}</c>, <see cref="Key"/>) and takes
/// whatever answers carry its step's ids (<see cref="Take"/>) — there is no stage flag: a stage-1
/// answer and a stage-2 answer are told apart by their ids. It asks stage 2's questions itself
/// (<see cref="Question"/>), so the numbers that choose them are the same ones its picks read.</para>
/// </summary>
public sealed class @this : IReadOnlyList<pick.@this>
{
    // The decider's cuts: a common action scored at or above Near is certain, and its module skips stage 2;
    // a choice's second module at or above Runner is asked too; a step whose best score is under Sure is
    // asked the popular choice; a picked if at or above Possible asks elseif/else by name.
    private const double Near = 0.9, Runner = 0.2, Sure = 0.8, Possible = 0.5;

    // The branches that can follow an if in the same step, asked by name: stage 2 is one choice per
    // module, so it can never give two actions of the condition module.
    private static readonly string[] Branch = ["condition.elseif", "condition.else"];

    // The actions most steps use (share of steps over the labelled goals and the builder's own), test-only
    // families left out — what an unsure step is asked to choose one of.
    private static readonly string[] Popular =
    [
        "variable.set", "goal.call", "output.write", "error.handle", "condition.if", "file.read", "file.save",
        "error.throw", "file.delete", "signing.sign", "list.count", "math.add", "loop.foreach", "signing.verify",
        "cache.wrap",
    ];

    private readonly global::app.goal.step.@this _step;

    // What the decider answered about this step, by question.
    private readonly Dictionary<string, number?> _module = new();                        // stage 1: module → its share of the choice
    private readonly Dictionary<string, number?> _named = new();                         // stage 1: common action → yes/no
    private readonly Dictionary<string, (string? Action, number? Confidence)> _choice = new();   // stage 2: module → its action
    private readonly Dictionary<string, number?> _also = new();                          // stage 2: module → does the step use it
    private readonly Dictionary<string, number?> _branch = new();                        // stage 2: elseif/else → yes/no
    private Dictionary<string, number?>? _popular;                                       // stage 2: popular action → share; null when not asked

    private List<pick.@this> _items = new();

    public @this(global::app.goal.step.@this step) => _step = step;

    /// <summary>The id of one of this step's questions: <c>s{index}_{question}</c>.</summary>
    public string Key(string question) => $"s{_step.Index}_{question}";

    public int Count => _items.Count;
    public pick.@this this[int index] => _items[index];
    public IEnumerator<pick.@this> GetEnumerator() => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>The top three of the popular choice (when the step was asked it), most probable first.</summary>
    public IReadOnlyList<(string Name, number? Score)> Top =>
        _popular == null ? [] : _popular.OrderByDescending(p => p.Value ?? (number)0.0).Take(3).Select(p => (p.Key, p.Value)).ToList();

    /// <summary>Takes this step's answers out of a decider answer (<c>{id: {choice, confidence,
    /// probabilities, noul}}</c>) — every id of its own, whichever stage asked it — and works out the
    /// picks again.</summary>
    public async Task Take(dict answer, global::app.actor.context.@this context)
    {
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
            else if (Branch.Contains(id)) _branch[id] = noul;
            else if (id.Contains('.')) _named[id] = noul;
            else _choice[id] = (await Choice(a, context), a.Get<number>("confidence", context));
        }
        _items = Picks(context);
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

    // ---------------------------------------------------------------- what stage 1 means

    // The modules of the choice, most probable first — only modules the app has.
    private List<(string Module, number Share)> Ranked(global::app.actor.context.@this context) =>
        _module.Where(m => m.Value is not null && context.App.Module.Contains(m.Key))
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
        var asked = Asked(context);
        return asked.Where(m => m != ranked[0].Module || ranked[0].Share < (number)Near).ToList();
    }

    // A picked if asks elseif/else by name.
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
            .Select(a => new pick.@this { Name = a.Key, Score = a.Value, From = "stage 1" }).ToList();
        var yesNo = YesNo(context);
        foreach (var m in Asked(context))
        {
            var module = context.App.Module[m];
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
                From = asked ? "stage 2 yes/no" : "stage 2",
            });
        }
        foreach (var (branch, score) in _branch)
            picks.Add(new pick.@this { Name = branch, Score = score, From = "branch" });
        return picks;
    }

    // ---------------------------------------------------------------- stage 2's questions

    /// <summary>What stage 2 asks of this step, keyed by id — the questions stage 1 leaves open.</summary>
    public async Task<dict> Question(global::app.actor.context.@this context)
    {
        var questions = new dict();
        var i = _step.Index;
        var text = _step.Text.Trim();
        if (Unsure(context))
            questions.Set(Key("@popular"), new Dictionary<string, object?>
            {
                ["type"] = "choice",
                ["criteria"] = Popular.ToDictionary(a => a, _ => (object?)null),
                ["instructions"] = $"Step {i} is `{text}`. Which of these actions does step {i} use?",
            });
        if (Tests)
            foreach (var branch in Branch)
                questions.Set(Key(branch), new Dictionary<string, object?>
                {
                    ["type"] = "noul",
                    ["instructions"] = $"Step {i} is `{text}`. It tests a condition. Does step {i} also have `{branch}` — " +
                                       $"{await Prose(context.App.Module["condition"][branch.Split('.')[1]]!, context)}?",
                });
        var asked = Asked(context);
        foreach (var m in YesNo(context))
        {
            var also = asked[0] == m ? "" : " also";
            questions.Set(Key($"@also.{m}"), new Dictionary<string, object?>
            {
                ["type"] = "noul",
                ["instructions"] = $"Step {i} is `{text}`. Does step {i}{also} use the plang module `{m}`?",
            });
        }
        foreach (var m in asked)
        {
            var module = context.App.Module[m];
            if (module.Count == 1) continue;   // one action: nothing to decide
            var criteria = new Dictionary<string, object?>();
            foreach (var action in module.ActionNames.OrderBy(a => a, StringComparer.Ordinal))
                criteria[action] = await Prose(module[action]!, context);
            questions.Set(Key(m), new Dictionary<string, object?>
            {
                ["type"] = "choice",
                ["instructions"] = $"Step {i} of this goal is `{text}`. It uses the plang module `{m}`. Which action of `{m}` does step {i} call?",
                ["criteria"] = criteria,
            });
        }
        return questions;
    }

    // An action's description, as its teaching file says it.
    private async Task<string?> Prose(global::app.goal.step.action.@this action, global::app.actor.context.@this context)
    {
        var read = await action.Description.Path.ReadText(context);
        return read.Success ? (await read.Value())?.ToString()?.Trim() : null;
    }
}
