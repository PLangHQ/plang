using app.actor.context;
using app;
using app.module.list;

namespace PLang.Tests.App.actions.list;

/// <summary>
/// FLOOR — flatten's recursive-descent arm over a genuinely nested runtime list.
/// There is no language op that constructs nesting: a list literal flattens through the
/// wire read, and `list.add` of a list flattens into the target. So a nested
/// <c>app.type.list</c> can only be built directly in C#. ListGoalRunTests covers the
/// language path (already-flat output); this pins the recursion the wire path can't reach.
/// </summary>
public class ListFlattenRecursionTests
{
    [Test]
    public async Task Flatten_RecursesIntoNestedLists()
    {
        await using var app = TestApp.Create("/app");
        var context = app.User.Context;

        // [1, [2,3], [4,[5]]] as a raw nested List — flatten's FromRaw converts it to a
        // native nested list, then FlattenNative recurses (line 30).
        var nested = new List<object?> { 1, new List<object?> { 2, 3 }, new List<object?> { 4, new List<object?> { 5 } } };
        context.Variable.Set("myList", nested);

        var result = await new Flatten { Context = context, ListName = new app.variable.@this("myList") }.Run();

        await result.IsSuccess();
        var flat = ((await result.Value()) as global::app.module.list.type.list)?.value as global::app.type.list.@this;
        await Assert.That((int)flat!.Count).IsEqualTo(5);
        var vals = new List<string?>();
        for (int i = 0; i < (int)flat.Count; i++) vals.Add((await flat.At(i)!.Value())?.ToString());
        await Assert.That(string.Join(",", vals)).IsEqualTo("1,2,3,4,5");
    }
}
