using System.IO.Compression;
using app;
using global::app.actor.context;
using global::app.variables;
using global::app.config;
using EngineType = global::app.@this;

namespace PLang.Tests.App.Settings;

public class SettingsTests
{
    private (EngineType engine, global::app.actor.context.@this context) CreateEngine()
    {
        var engine = new EngineType("/app");
        var context = new global::app.actor.context.@this(engine, new Variables());
        return (engine, context);
    }

    [Test]
    public async Task Resolve_ReturnsClassDefault_WhenNoScopeSet()
    {
        // No settings have been written to any scope.
        // Resolve should fall through to the class default.
        var (engine, context) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;

        var result = engine.Config.Resolve<long>("archive.max", context, classDefault);

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

        engine.Config.Set("archive.max", goalValue, context);

        var result = engine.Config.Resolve<long>("archive.max", context, classDefault);

        await Assert.That(result).IsEqualTo(goalValue);
    }

    [Test]
    public async Task Resolve_InheritsFromParentContext()
    {
        // Parent context has a setting. Child context (no local setting) should inherit it.
        var (engine, parentContext) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;
        long parentValue = 50 * 1024 * 1024;

        engine.Config.Set("archive.max", parentValue, parentContext);

        var childContext = parentContext.CreateChild();

        var result = engine.Config.Resolve<long>("archive.max", childContext, classDefault);

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

        engine.Config.Set("archive.max", engineDefault, context, isDefault: true);

        var result = engine.Config.Resolve<long>("archive.max", context, classDefault);

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

        engine.Config.Set("archive.max", engineDefault, context, isDefault: true);
        engine.Config.Set("archive.max", goalValue, context);

        var result = engine.Config.Resolve<long>("archive.max", context, classDefault);

        await Assert.That(result).IsEqualTo(goalValue);
    }

    [Test]
    public async Task Resolve_ChildGoalScope_OverridesParentGoalScope()
    {
        // Parent has a goal-scoped value. Child sets its own. Child's value wins.
        var (engine, parentContext) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;

        engine.Config.Set("archive.max", 50L * 1024 * 1024, parentContext);

        var childContext = parentContext.CreateChild();
        engine.Config.Set("archive.max", 10L * 1024 * 1024, childContext);

        var result = engine.Config.Resolve<long>("archive.max", childContext, classDefault);

        await Assert.That(result).IsEqualTo(10L * 1024 * 1024);
    }

    [Test]
    public async Task Resolve_WidensIntToLong()
    {
        // JSON deserialization often produces int when the value fits.
        // Resolve<long> must handle int→long widening without crashing.
        var (engine, context) = CreateEngine();

        engine.Config.Set("archive.max", (int)42, context);

        var result = engine.Config.Resolve<long>("archive.max", context, 0L);

        await Assert.That(result).IsEqualTo(42L);
    }

    [Test]
    public async Task Resolve_TypeMismatch_ReturnsClassDefault()
    {
        // If the stored value can't be converted to T, fall back to classDefault.
        var (engine, context) = CreateEngine();

        engine.Config.Set("archive.max", "not-a-number", context);

        var result = engine.Config.Resolve<long>("archive.max", context, 99L);

        await Assert.That(result).IsEqualTo(99L);
    }

    [Test]
    public async Task Resolve_SkipsNullScopeInParentChain()
    {
        // Grandparent has a setting. Middle parent has no ConfigScope (null).
        // Child should still resolve grandparent's value.
        var (engine, grandparent) = CreateEngine();
        long classDefault = 100L;

        engine.Config.Set("archive.max", 42L, grandparent);

        var parent = grandparent.CreateChild(); // no settings set — ConfigScope stays null
        var child = parent.CreateChild();

        var result = engine.Config.Resolve<long>("archive.max", child, classDefault);

        await Assert.That(result).IsEqualTo(42L);
    }

    [Test]
    public async Task GoalRunAsync_ScopesSettingsPerGoal()
    {
        // When a goal runs, its settings scope is isolated.
        // After the goal returns, the previous scope is restored.
        var (engine, context) = CreateEngine();
        long classDefault = 100L;

        engine.Config.Set("archive.max", 50L, context);
        var scopeBefore = context.ConfigScope;

        // Simulate what Goal.RunAsync does: save, null, restore
        var saved = context.ConfigScope;
        context.ConfigScope = null;

        // Inside the "goal": no settings visible from outer scope
        var duringGoal = engine.Config.Resolve<long>("archive.max", context, classDefault);
        await Assert.That(duringGoal).IsEqualTo(classDefault);

        // Set something inside the goal
        engine.Config.Set("archive.max", 10L, context);
        var insideGoal = engine.Config.Resolve<long>("archive.max", context, classDefault);
        await Assert.That(insideGoal).IsEqualTo(10L);

        // Restore — outer scope is back
        context.ConfigScope = saved;
        var afterGoal = engine.Config.Resolve<long>("archive.max", context, classDefault);
        await Assert.That(afterGoal).IsEqualTo(50L);
    }

    [Test]
    public async Task Resolve_WidensIntToEnum()
    {
        // JSON deserialization often produces int for enum values.
        // Resolve<CompressionLevel> must handle int→enum conversion.
        var (engine, context) = CreateEngine();

        engine.Config.Set("archive.level", (int)CompressionLevel.Fastest, context);

        var result = engine.Config.Resolve("archive.level", context, CompressionLevel.Optimal);

        await Assert.That(result).IsEqualTo(CompressionLevel.Fastest);
    }

    [Test]
    public async Task Clone_PreservesConfigScope()
    {
        // A cloned context should see the same settings as the original.
        var (engine, context) = CreateEngine();
        long classDefault = 100L;

        engine.Config.Set("archive.max", 42L, context);

        var clone = context.Clone();

        var result = engine.Config.Resolve<long>("archive.max", clone, classDefault);
        await Assert.That(result).IsEqualTo(42L);
    }

    [Test]
    public async Task Clone_WritesToClone_DoNotAffectOriginal()
    {
        // Clone gets an independent copy of ConfigScope.
        // Writing to the clone must not pollute the original.
        var (engine, context) = CreateEngine();
        long classDefault = 100L;

        engine.Config.Set("archive.max", 42L, context);

        var clone = context.Clone();
        engine.Config.Set("archive.max", 999L, clone);

        // Clone sees the new value
        var cloneResult = engine.Config.Resolve<long>("archive.max", clone, classDefault);
        await Assert.That(cloneResult).IsEqualTo(999L);

        // Original is untouched
        var originalResult = engine.Config.Resolve<long>("archive.max", context, classDefault);
        await Assert.That(originalResult).IsEqualTo(42L);
    }

    [Test]
    public async Task Resolve_ConvertsStringToEnum()
    {
        // Builder may store enum settings as strings (natural language input).
        // Cast<T> must handle string→enum via Enum.TryParse.
        var (engine, context) = CreateEngine();

        engine.Config.Set("archive.level", "Fastest", context);

        var result = engine.Config.Resolve("archive.level", context, CompressionLevel.Optimal);

        await Assert.That(result).IsEqualTo(CompressionLevel.Fastest);
    }

    [Test]
    public async Task Resolve_ConvertsStringToEnum_CaseInsensitive()
    {
        var (engine, context) = CreateEngine();

        engine.Config.Set("archive.level", "fastest", context);

        var result = engine.Config.Resolve("archive.level", context, CompressionLevel.Optimal);

        await Assert.That(result).IsEqualTo(CompressionLevel.Fastest);
    }

    [Test]
    public async Task Resolve_InvalidEnumString_ReturnsClassDefault()
    {
        var (engine, context) = CreateEngine();

        engine.Config.Set("archive.level", "not-a-level", context);

        var result = engine.Config.Resolve("archive.level", context, CompressionLevel.Optimal);

        await Assert.That(result).IsEqualTo(CompressionLevel.Optimal);
    }
}
