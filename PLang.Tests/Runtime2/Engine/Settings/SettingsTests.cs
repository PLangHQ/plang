using System.IO.Compression;
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

    [Test]
    public async Task Resolve_WidensIntToLong()
    {
        // JSON deserialization often produces int when the value fits.
        // Resolve<long> must handle int→long widening without crashing.
        var (engine, context) = CreateEngine();

        engine.Settings.Set("archive.max", (int)42, context);

        var result = engine.Settings.Resolve<long>("archive.max", context, 0L);

        await Assert.That(result).IsEqualTo(42L);
    }

    [Test]
    public async Task Resolve_TypeMismatch_ReturnsClassDefault()
    {
        // If the stored value can't be converted to T, fall back to classDefault.
        var (engine, context) = CreateEngine();

        engine.Settings.Set("archive.max", "not-a-number", context);

        var result = engine.Settings.Resolve<long>("archive.max", context, 99L);

        await Assert.That(result).IsEqualTo(99L);
    }

    [Test]
    public async Task Resolve_SkipsNullScopeInParentChain()
    {
        // Grandparent has a setting. Middle parent has no SettingsScope (null).
        // Child should still resolve grandparent's value.
        var (engine, grandparent) = CreateEngine();
        long classDefault = 100L;

        engine.Settings.Set("archive.max", 42L, grandparent);

        var parent = grandparent.CreateChild(); // no settings set — SettingsScope stays null
        var child = parent.CreateChild();

        var result = engine.Settings.Resolve<long>("archive.max", child, classDefault);

        await Assert.That(result).IsEqualTo(42L);
    }

    [Test]
    public async Task GoalRunAsync_ScopesSettingsPerGoal()
    {
        // When a goal runs, its settings scope is isolated.
        // After the goal returns, the previous scope is restored.
        var (engine, context) = CreateEngine();
        long classDefault = 100L;

        engine.Settings.Set("archive.max", 50L, context);
        var scopeBefore = context.SettingsScope;

        // Simulate what Goal.RunAsync does: save, null, restore
        var saved = context.SettingsScope;
        context.SettingsScope = null;

        // Inside the "goal": no settings visible from outer scope
        var duringGoal = engine.Settings.Resolve<long>("archive.max", context, classDefault);
        await Assert.That(duringGoal).IsEqualTo(classDefault);

        // Set something inside the goal
        engine.Settings.Set("archive.max", 10L, context);
        var insideGoal = engine.Settings.Resolve<long>("archive.max", context, classDefault);
        await Assert.That(insideGoal).IsEqualTo(10L);

        // Restore — outer scope is back
        context.SettingsScope = saved;
        var afterGoal = engine.Settings.Resolve<long>("archive.max", context, classDefault);
        await Assert.That(afterGoal).IsEqualTo(50L);
    }

    [Test]
    public async Task Resolve_WidensIntToEnum()
    {
        // JSON deserialization often produces int for enum values.
        // Resolve<CompressionLevel> must handle int→enum conversion.
        var (engine, context) = CreateEngine();

        engine.Settings.Set("archive.level", (int)CompressionLevel.Fastest, context);

        var result = engine.Settings.Resolve("archive.level", context, CompressionLevel.Optimal);

        await Assert.That(result).IsEqualTo(CompressionLevel.Fastest);
    }

    [Test]
    public async Task Clone_PreservesSettingsScope()
    {
        // A cloned context should see the same settings as the original.
        var (engine, context) = CreateEngine();
        long classDefault = 100L;

        engine.Settings.Set("archive.max", 42L, context);

        var clone = context.Clone();

        var result = engine.Settings.Resolve<long>("archive.max", clone, classDefault);
        await Assert.That(result).IsEqualTo(42L);
    }
}
