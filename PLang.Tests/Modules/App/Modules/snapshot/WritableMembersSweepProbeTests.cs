namespace PLang.Tests.App.Modules.snapshot;

// Sweep (snapshot 306): every [LlmBuilder] property a navigation set can write. A member is listed by the type list when it is
// public-read and marked [LlmBuilder], and property.Write refuses only a property with no setter at all (CanWrite), then
// SetValue — which also calls a private or init setter. Prints class, member, setter kind; asserts nothing.
public class WritableMembersSweepProbeTests
{
    [Test]
    public async Task ListEveryWritableLlmBuilderMember()
    {
        var found = new List<string>();
        foreach (var type in typeof(global::app.@this).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract || t.IsAbstract && !t.IsSealed))
        {
            System.Reflection.PropertyInfo[] props;
            try { props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly); }
            catch { continue; }
            foreach (var prop in props)
            {
                if (!prop.CanRead || prop.Name == "EqualityContract") continue;
                if (!System.Attribute.IsDefined(prop, typeof(global::app.LlmBuilderAttribute))) continue;
                if (!prop.CanWrite) continue;
                var set = prop.GetSetMethod(true)!;
                var kind = set.IsPublic ? (set.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit") ? "init" : "PUBLIC-SET")
                    : set.IsAssembly ? "internal-set" : set.IsFamily || set.IsFamilyOrAssembly ? "protected-set" : "private-set";
                found.Add($"{type.FullName} . {prop.Name} : {prop.PropertyType.Name} [{kind}]");
            }
        }
        await TestContext.Current!.OutputWriter.WriteLineAsync("SWEEP\n" + string.Join("\n", found.OrderBy(x => x)));
        await Assert.That(found.Count).IsGreaterThanOrEqualTo(0);
    }

    private static global::app.@this App() => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-sweep-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();

    // property.Write checks CanWrite (any setter, private or internal too) and calls SetValue, which reaches a non-public setter.
    // So a navigation set from plang can rename a goal and renumber or retext a step even where the setter is internal.
    [Test]
    public async Task ANavigationSet_CannotWriteAGoalName_ThroughAnInternalSetter()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", context) };
        app.goal.list.Add(goal);
        await context.Variable.Set("g", goal);

        var set = await (await context.Action("variable.set(Name=%g.name%, Value=\"Renamed\")")).Start(context);

        await TestContext.Current!.OutputWriter.WriteLineAsync($"NONPUBLIC goal.name: set.Success={set.Success} key={set.Error?.Key} name={goal.Name}");
        await Assert.That(goal.Name).IsEqualTo("G");
    }

    [Test]
    public async Task ANavigationSet_CannotWriteAStepsIndexOrText_ThroughAnInternalSetter()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", context) };
        var step = new Step(app.type.list) { Goal = goal, Index = 0, Text = "original" };
        goal.Step.Add(step);
        app.goal.list.Add(goal);
        await context.Variable.Set("s", step);

        await (await context.Action("variable.set(Name=%s.index%, Value=\"5\")")).Start(context);
        await (await context.Action("variable.set(Name=%s.text%, Value=\"EDITED\")")).Start(context);

        await TestContext.Current!.OutputWriter.WriteLineAsync($"NONPUBLIC step: index={step.Index} text={step.Text}");
        await Assert.That(step.Index).IsEqualTo(0);
        await Assert.That(step.Text).IsEqualTo("original");
    }
}
