using System.Collections;
using Action = global::app.goal.steps.step.actions.action.@this;

namespace app.module.action.condition.decision;

/// <summary>
/// The if/elseif/else structure at a step's condition point — built ONCE from the step's actions and
/// read by everyone who needs the shape without running it: <c>condition.if</c> (to run the taken
/// branch), <c>test.discover</c> (to seed branch coverage), and the coverage head-guard. A Decision
/// IS its branches (it's an <see cref="IReadOnlyList{Branch}"/>) and carries their label
/// <see cref="Chain"/>. <c>condition.if</c> owns the RUNNING; this owns the STRUCTURE. Singular — a
/// decision is one thing (its branches), never a plural "Branches" collection.
/// </summary>
public sealed class @this : IReadOnlyList<Branch>
{
    private readonly List<Branch> _branches;

    /// <summary>The declared branch labels in order — <c>[true, false]</c> for a bare single-action
    /// if, else one per condition (<c>if</c> / <c>elseif[N]</c> / <c>else</c>). Coverage records the
    /// full chain so the report can show which declared branches were never tested.</summary>
    public IReadOnlyList<string> Chain { get; }

    private @this(List<Branch> branches, IReadOnlyList<string> chain)
    {
        _branches = branches;
        Chain = chain;
    }

    /// <summary>The decision rooted at the head <c>condition.if</c> of <paramref name="actions"/> —
    /// null when the sequence has no condition (nothing to decide).</summary>
    public static @this? Of(IList<Action> actions)
    {
        int head = Head(actions);
        return head < 0 ? null : new @this(Split(actions, head), Labels(actions, head));
    }

    /// <summary>Is <paramref name="action"/> the head <c>condition.if</c> of <paramref name="actions"/>?
    /// Coverage records sites against the head — inner-elseif firings don't own the site.</summary>
    public static bool HeadIs(IList<Action> actions, Action action)
    {
        int head = Head(actions);
        return head >= 0 && ReferenceEquals(actions[head], action);
    }

    /// <summary>Index of the first condition action, or -1 when the sequence has none.</summary>
    private static int Head(IList<Action> actions)
    {
        for (int i = 0; i < actions.Count; i++)
            if (actions[i].IsCondition) return i;
        return -1;
    }

    // A condition action starts a branch; non-conditions append to the current body; a trailing
    // body-only tail attaches to the last condition. Index THROUGH the source list so each returned
    // action carries its Step (the executed branch needs it for the orchestration guard,
    // DisableChildrenOf, and coverage site keys).
    private static List<Branch> Split(IList<Action> actions, int start)
    {
        var branches = new List<Branch>();
        List<Action>? body = null;
        Action? condition = null;

        for (int i = start; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action.IsCondition)
            {
                if (body != null) branches.Add(new Branch(condition, body));
                condition = action;
                body = new List<Action>();
            }
            else
            {
                body ??= new List<Action>();
                body.Add(action);
            }
        }
        if (body != null) branches.Add(new Branch(condition, body));
        return branches;
    }

    // {true,false} for a bare single-action if; else one label per condition action
    // (if / elseif[N] / else), tagged by its action name.
    private static List<string> Labels(IList<Action> actions, int start)
    {
        if (actions.Count == 1)
            return new List<string> { "true", "false" };

        var chain = new List<string>();
        for (int i = start; i < actions.Count; i++)
        {
            var a = actions[i];
            if (!a.IsCondition) continue;
            if (string.Equals(a.ActionName, "if", System.StringComparison.OrdinalIgnoreCase))
                chain.Add("if");
            else if (string.Equals(a.ActionName, "else", System.StringComparison.OrdinalIgnoreCase))
                chain.Add("else");
            else
                chain.Add($"elseif[{chain.Count}]");
        }
        return chain;
    }

    public int Count => _branches.Count;
    public Branch this[int i] => _branches[i];
    public IEnumerator<Branch> GetEnumerator() => _branches.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>One branch of a <see cref="@this">Decision</see> — its guard condition (null for a
/// trailing body-only tail) and the body actions that run when the guard is taken.</summary>
public sealed record Branch(Action? Condition, IReadOnlyList<Action> Body);
