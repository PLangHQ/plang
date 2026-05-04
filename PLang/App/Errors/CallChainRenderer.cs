using Call = App.CallStack.Call.@this;

namespace App.Errors;

/// <summary>
/// Folds a call chain (failing → root) into human-readable lines for error reports.
/// Two reductions:
///  - <b>Recursion compression:</b> consecutive frames sharing
///    <c>(Goal.Path, Action.Module, Action.Step.Index)</c> collapse to one line with
///    a "×N" suffix. A frame whose <c>Errors</c> is non-empty breaks the run so the
///    failing frame stays individually visible.
///  - <b>Cause annotation:</b> a frame at a recovery boundary (own <c>Cause</c> set,
///    differs from the next outer frame's <c>Cause</c>) gets a trailing
///    "⤷ caused by error in: name (line N)" hint. Inherited causes (walked up via
///    <see cref="Call.Cause"/>) don't re-annotate every descendant.
/// </summary>
public static class CallChainRenderer
{
    public static IReadOnlyList<string> Render(IReadOnlyList<Call> chain)
    {
        var lines = new List<string>();
        if (chain.Count == 0) return lines;

        int i = 0;
        while (i < chain.Count)
        {
            var head = chain[i];
            int count = 1;
            // Compress only when the head itself has no errors — failing frames stay alone.
            if (head.Errors.Count == 0)
            {
                while (i + count < chain.Count
                    && AreEquivalent(head, chain[i + count])
                    && chain[i + count].Errors.Count == 0)
                    count++;
            }

            // The annotated index is the OUTERMOST frame in the run — that's where the
            // boundary-vs-inherited-Cause check needs to look. Inner frames in a recursion
            // share the same Cause anyway (Cause walks up through Caller).
            int annotateIdx = i + count - 1;
            lines.Add(FormatFrame(head, count, IsCauseBoundary(chain, annotateIdx)));
            i += count;
        }
        return lines;
    }

    private static bool AreEquivalent(Call a, Call b)
    {
        var aStep = a.Action?.Step;
        var bStep = b.Action?.Step;
        return string.Equals(a.Action?.Module, b.Action?.Module, StringComparison.Ordinal)
            && aStep?.Index == bStep?.Index
            && string.Equals(aStep?.Goal?.Path, bStep?.Goal?.Path, StringComparison.Ordinal);
    }

    // Boundary: this frame's Cause is set AND the next outer frame's Cause is a different
    // ref (or null). Compares by reference because Cause-walk-up returns the same Call
    // instance through descendants — so ref-equality precisely identifies the introduction
    // point.
    private static bool IsCauseBoundary(IReadOnlyList<Call> chain, int i)
    {
        var cause = chain[i].Cause;
        if (cause == null) return false;
        var outer = i + 1 < chain.Count ? chain[i + 1].Cause : null;
        return !ReferenceEquals(cause, outer);
    }

    private static string FormatFrame(Call frame, int count, bool annotateCause)
    {
        var step = frame.Action?.Step;
        var goal = step?.Goal;
        var name = goal?.Name ?? frame.Action?.Module ?? "?";
        var path = goal?.Path ?? "";
        var lineSuffix = step != null ? $":{step.LineNumber}" : "";
        var multiplier = count > 1 ? $" ×{count}" : "";
        var line = $"{name}{multiplier} - {path}{lineSuffix}";

        if (annotateCause && frame.Cause != null)
        {
            var cstep = frame.Cause.Action?.Step;
            var cname = cstep?.Goal?.Name ?? frame.Cause.Action?.Module ?? "?";
            var cline = cstep != null ? $" (line {cstep.LineNumber})" : "";
            line += $"  ↷ caused by error in: {cname}{cline}";
        }
        return line;
    }
}
