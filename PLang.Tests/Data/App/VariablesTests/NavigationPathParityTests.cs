using NavPath = global::app.variable.path.@this;
using Segment = global::app.variable.path.Segment;

namespace PLang.Tests.App.VariablesTests;

// Tokenization spec for the navigation `path` value (app.variable.path). A reference
// string parses ONCE into typed segments. These expected token streams were pinned
// equal to the legacy free-function Data.ParseNextSegment (redesign step 1, before it
// was deleted in step 2), so they double as the parity record proving the reroute is
// behaviour-preserving.
public class NavigationPathParityTests
{
    private static System.Collections.Generic.List<string> NavTokens(string path)
        => System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.Select(NavPath.Parse(path).Segments, s => s.Raw));

    [Test]
    [Arguments("user.name", new[] { "user", "name" })]
    [Arguments("goal.Steps[planStep.index]", new[] { "goal", "Steps", "[planStep.index]" })]
    [Arguments("goal.Steps[step.Index].Actions", new[] { "goal", "Steps", "[step.Index]", "Actions" })]
    [Arguments("items[0]", new[] { "items", "[0]" })]
    [Arguments("items[0].value", new[] { "items", "[0]", "value" })]
    [Arguments("trace.plan.steps[step.Index].actions", new[] { "trace", "plan", "steps", "[step.Index]", "actions" })]
    [Arguments("x!file!path", new[] { "x", "!file", "!path" })]
    [Arguments("!data.branchIndex", new[] { "!data", "branchIndex" })]
    [Arguments("data.grep(\"pattern\").maxLength(100)", new[] { "data", "grep(\"pattern\")", "maxLength(100)" })]
    [Arguments("tags.\"key.with.dots\"", new[] { "tags", "\"key.with.dots\"" })]
    [Arguments("single", new[] { "single" })]
    [Arguments("a.b.c.d", new[] { "a", "b", "c", "d" })]
    public async Task Parse_yields_expected_token_stream(string path, string[] expected)
    {
        var nav = NavTokens(path);
        await Assert.That(nav).IsEquivalentTo(System.Linq.Enumerable.ToList(expected));
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
