using Call = app.callstack.call.@this;

namespace app.errors;

/// <summary>
/// Folds a call chain (failing → root) into human-readable lines for error reports.
/// Compresses consecutive frames sharing <c>(Goal.Path, Action.Module, Action.Step.Index)</c>
/// into one line with a "×N" suffix. A frame whose <c>Errors</c> is non-empty breaks
/// the run so the failing frame stays individually visible.
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

            lines.Add(FormatFrame(head, count));
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

    private static string FormatFrame(Call frame, int count)
    {
        var step = frame.Action?.Step;
        var goal = step?.Goal;
        var name = goal?.Name ?? frame.Action?.Module ?? "?";
        var path = goal?.Path ?? "";
        var lineSuffix = step != null ? $":{step.LineNumber}" : "";
        var multiplier = count > 1 ? $" ×{count}" : "";
        return $"{name}{multiplier} - {path}{lineSuffix}";
    }
}
