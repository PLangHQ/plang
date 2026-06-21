using NavPath = global::app.variable.path.@this;
using Segment = global::app.variable.path.Segment;

namespace PLang.Tests.App.VariablesTests;

// Parity pins for the new navigation `path` value (app.variable.path) against the
// legacy free-function tokenizer Data.ParseNextSegment, which it will replace.
// The path parses a reference string ONCE into typed segments; this proves it
// yields the SAME token sequence the old per-hop tokenizer did, so rerouting reads
// (redesign step 2) is behaviour-preserving. ParseNextSegment is private + doomed,
// so we reach it by reflection rather than editing production for the test.
public class NavigationPathParityTests
{
    // Repeatedly applies the real Data.ParseNextSegment to reproduce the legacy
    // token stream (the sequence GetChild walked one hop at a time).
    private static System.Collections.Generic.List<string> LegacyTokens(string path)
    {
        var method = typeof(global::app.data.@this).GetMethod(
            "ParseNextSegment",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new System.Exception("ParseNextSegment not found — has it been removed?");

        var tokens = new System.Collections.Generic.List<string>();
        var rest = path;
        while (rest.Length > 0)
        {
            var result = method.Invoke(null, new object[] { rest })!;
            var t = result.GetType();
            var token = (string)t.GetField("Item1")!.GetValue(result)!;
            var remaining = (string)t.GetField("Item2")!.GetValue(result)!;
            if (token.Length == 0) break;
            tokens.Add(token);
            // GetChild strips a leading '.' off the remaining after a bracket split.
            rest = remaining.StartsWith('.') ? remaining[1..] : remaining;
        }
        return tokens;
    }

    private static System.Collections.Generic.List<string> NavTokens(string path)
        => System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.Select(NavPath.Parse(path).Segments, s => s.Raw));

    [Test]
    [Arguments("user.name")]
    [Arguments("goal.Steps[planStep.index]")]
    [Arguments("goal.Steps[step.Index].Actions")]
    [Arguments("items[0]")]
    [Arguments("items[0].value")]
    [Arguments("trace.plan.steps[step.Index].actions")]
    [Arguments("x!file!path")]
    [Arguments("!data.branchIndex")]
    [Arguments("data.grep(\"pattern\").maxLength(100)")]
    [Arguments("tags.\"key.with.dots\"")]
    [Arguments("single")]
    [Arguments("a.b.c.d")]
    public async Task Parse_yields_same_token_stream_as_legacy_tokenizer(string path)
    {
        var legacy = LegacyTokens(path);
        var nav = NavTokens(path);
        await Assert.That(nav).IsEquivalentTo(legacy);
    }

    [Test]
    public async Task Round_trips_to_source_form()
    {
        foreach (var s in new[]
        {
            "user.name", "goal.Steps[planStep.index]", "goal.Steps[step.Index].Actions",
            "items[0].value", "x!file!path", "!data.branchIndex",
            "data.grep(\"pattern\").maxLength(100)", "tags.\"key.with.dots\"",
        })
            await Assert.That(NavPath.Parse(s).ToString()).IsEqualTo(s);
    }

    [Test]
    public async Task Split_peels_head_from_tail()
    {
        var path = NavPath.Parse("goal.Steps[planStep.index]");
        var (head, tail) = path.Split();

        await Assert.That(head).IsTypeOf<Segment.Member>();
        await Assert.That(((Segment.Member)head!).Name).IsEqualTo("goal");
        await Assert.That(tail.ToString()).IsEqualTo("Steps[planStep.index]");

        // The bracket's inner is itself a path — planStep.index, not a string.
        var (_, t2) = tail.Split();          // peel "Steps"
        var (idxSeg, _) = t2.Split();         // the "[planStep.index]" segment
        var idx = (Segment.Index)idxSeg!;
        await Assert.That(idx.Inner.ToString()).IsEqualTo("planStep.index");
        await Assert.That(idx.Inner.Segments.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Empty_path_is_terminus()
    {
        var path = NavPath.Parse("");
        await Assert.That(path.IsEmpty).IsTrue();
        var (head, _) = path.Split();
        await Assert.That(head).IsNull();
    }
}
