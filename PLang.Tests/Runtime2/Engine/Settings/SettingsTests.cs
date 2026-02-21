using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Settings;
using EngineType = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Engine.Settings;

public class SettingsTests
{
    private (EngineType engine, PLangContext context) CreateEngine()
    {
        var engine = new EngineType("/app");
        var context = new PLangContext(engine, new MemoryStack());
        return (engine, context);
    }

    [Test]
    public async Task Resolve_ReturnsClassDefault_WhenNoScopeSet()
    {
        // No settings have been written to any scope.
        // Resolve should fall through to the class default.
        var (engine, context) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;

        var result = engine.Settings.Resolve<long>("archive.max", context, classDefault);

        await Assert.That(result).IsEqualTo(classDefault);
    }

    [Test]
    public async Task Resolve_ReturnsGoalScopedValue_WhenSet()
    {
        // A settings handler writes to the context's goal scope.
        // Resolve should find it.
        var (engine, context) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;
        long goalValue = 20 * 1024 * 1024;

        engine.Settings.Set("archive.max", goalValue, context);

        var result = engine.Settings.Resolve<long>("archive.max", context, classDefault);

        await Assert.That(result).IsEqualTo(goalValue);
    }

    [Test]
    public async Task Resolve_InheritsFromParentContext()
    {
        // Parent context has a setting. Child context (no local setting) should inherit it.
        var (engine, parentContext) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;
        long parentValue = 50 * 1024 * 1024;

        engine.Settings.Set("archive.max", parentValue, parentContext);

        var childContext = parentContext.CreateChild();

        var result = engine.Settings.Resolve<long>("archive.max", childContext, classDefault);

        await Assert.That(result).IsEqualTo(parentValue);
    }

    [Test]
    public async Task Resolve_EngineDefaultOverridesClassDefault()
    {
        // Engine default is set, no goal scope set.
        // Should return engine default, not class default.
        var (engine, context) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;
        long engineDefault = 200 * 1024 * 1024;

        engine.Settings.Set("archive.max", engineDefault, context, isDefault: true);

        var result = engine.Settings.Resolve<long>("archive.max", context, classDefault);

        await Assert.That(result).IsEqualTo(engineDefault);
    }

    [Test]
    public async Task Resolve_GoalScopeOverridesEngineDefault()
    {
        // Both engine default and goal scope are set.
        // Goal scope should win.
        var (engine, context) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;
        long engineDefault = 200 * 1024 * 1024;
        long goalValue = 20 * 1024 * 1024;

        engine.Settings.Set("archive.max", engineDefault, context, isDefault: true);
        engine.Settings.Set("archive.max", goalValue, context);

        var result = engine.Settings.Resolve<long>("archive.max", context, classDefault);

        await Assert.That(result).IsEqualTo(goalValue);
    }

    [Test]
    public async Task Resolve_ChildGoalScope_OverridesParentGoalScope()
    {
        // Parent has a goal-scoped value. Child sets its own. Child's value wins.
        var (engine, parentContext) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;

        engine.Settings.Set("archive.max", 50L * 1024 * 1024, parentContext);

        var childContext = parentContext.CreateChild();
        engine.Settings.Set("archive.max", 10L * 1024 * 1024, childContext);

        var result = engine.Settings.Resolve<long>("archive.max", childContext, classDefault);

        await Assert.That(result).IsEqualTo(10L * 1024 * 1024);
    }
}
