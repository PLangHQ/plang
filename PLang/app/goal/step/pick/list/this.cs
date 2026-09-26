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

    private List<pick.@this> _items = new();
    private List<question.@this> _question = new();

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

    /// <summary>The top three of the popular choice (when the step was asked it), most probable first.</summary>
    public IReadOnlyList<(string Name, number? Score)> Top =>
        _popular == null ? [] : _popular.OrderByDescending(p => p.Value ?? (number)0.0).Take(3).Select(p => (p.Key, p.Value)).ToList();

    /// <summary>Takes this step's answers out of a decider answer (<c>{id: {choice, confidence,
    /// probabilities, noul}}</c>) — every id of its own, whichever stage asked it — and works out the
    /// picks and the stage-2 questions again. <paramref name="popular"/> is the popular actions an
    /// unsure step is offered.</summary>
    public async Task Take(dict answer, IEnumerable<string> popular, global::app.actor.context.@this context)
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
            else if (Branch(context).Any(b => $"{b.Module.Name}.{b.Name}" == id)) _branch[id] = noul;
            else if (id.Contains('.')) _named[id] = noul;
            else _choice[id] = (await Choice(a, context), a.Get<number>("confidence", context));
        }
        _items = Picks(context);
        _question = Questions(popular, context);
        _moduleAsked = Asked(context).Select(m => context.App.Module[m]).ToList();
        IsCondition = Tests;
        IsUnsure = Unsure(context);
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
        var condition = context.App.Module["condition"];
        return condition.ActionNames.OrderBy(a => a, StringComparer.Ordinal)
            .Select(a => condition[a]!).Where(a => a.IsBranch).ToList();
    }

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
                From = asked ? From.YesNo : From.Choice,
            });
        }
        foreach (var branch in Branch(context).Select(b => $"{b.Module.Name}.{b.Name}").Where(_branch.ContainsKey))
            picks.Add(new pick.@this { Name = branch, Score = _branch[branch], From = From.Branch });
        return picks;
    }

    // ---------------------------------------------------------------- stage 2's questions

    private List<question.@this> Questions(IEnumerable<string> popular, global::app.actor.context.@this context)
    {
        var questions = new List<question.@this>();
        if (Unsure(context))
            questions.Add(new question.@this
            {
                Id = Key("@popular"), Kind = Kind.Popular,
                Option = popular.Select(a => a.Split('.')).Select(a => context.App.Module[a[0]][a[1]]!).ToList(),
            });
        if (Tests)
            foreach (var branch in Branch(context))
                questions.Add(new question.@this { Id = Key($"{branch.Module.Name}.{branch.Name}"), Kind = Kind.Branch, Branch = branch });
        var asked = Asked(context);
        foreach (var m in YesNo(context))
            questions.Add(new question.@this { Id = Key($"@also.{m}"), Kind = Kind.Use, Module = context.App.Module[m], Also = asked[0] != m });
        foreach (var m in asked)
        {
            var module = context.App.Module[m];
            if (module.Count == 1) continue;   // one action: nothing to decide
            questions.Add(new question.@this
            {
                Id = Key(m), Kind = Kind.Action, Module = module,
                Option = module.ActionNames.OrderBy(a => a, StringComparer.Ordinal).Select(a => module[a]!).ToList(),
            });
        }
        return questions;
    }
}
