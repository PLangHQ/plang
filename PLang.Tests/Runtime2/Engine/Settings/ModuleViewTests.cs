using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Settings;
using ArchiveSettings = PLang.Runtime2.actions.archive.Settings;
using EngineType = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Engine.Settings;

public class ModuleViewTests
{
    private (EngineType engine, PLangContext context) CreateEngine()
    {
        var engine = new EngineType("/app");
        var context = new PLangContext(engine, new MemoryStack());
        return (engine, context);
    }

    [Test]
    public async Task For_ReturnsModuleView()
    {
        var (engine, context) = CreateEngine();

        var view = engine.Settings.For<ArchiveSettings>(context);

        await Assert.That(view).IsNotNull();
    }

    [Test]
    public async Task ModuleView_ResolvesClassDefault()
    {
        // No settings set — ModuleView should resolve to the class default.
        var (engine, context) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;

        var view = engine.Settings.For<ArchiveSettings>(context);
        var result = view.Resolve<long>("max", classDefault);

        await Assert.That(result).IsEqualTo(classDefault);
    }

    [Test]
    public async Task ModuleView_ResolvesGoalScopedValue()
    {
        // A value has been set in goal scope — ModuleView should find it.
        var (engine, context) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;
        long goalValue = 20 * 1024 * 1024;

        engine.Settings.Set("archive.max", goalValue, context);

        var view = engine.Settings.For<ArchiveSettings>(context);
        var result = view.Resolve<long>("max", classDefault);

        await Assert.That(result).IsEqualTo(goalValue);
    }

    [Test]
    public async Task ModuleView_DifferentContextsGetDifferentValues()
    {
        // Two contexts with different settings. Each ModuleView should see its own value.
        var (engine, context1) = CreateEngine();
        var context2 = new PLangContext(engine, new MemoryStack());
        long classDefault = 100 * 1024 * 1024;

        engine.Settings.Set("archive.max", 20L * 1024 * 1024, context1);
        engine.Settings.Set("archive.max", 50L * 1024 * 1024, context2);

        var view1 = engine.Settings.For<ArchiveSettings>(context1);
        var view2 = engine.Settings.For<ArchiveSettings>(context2);

        var result1 = view1.Resolve<long>("max", classDefault);
        var result2 = view2.Resolve<long>("max", classDefault);

        await Assert.That(result1).IsEqualTo(20L * 1024 * 1024);
        await Assert.That(result2).IsEqualTo(50L * 1024 * 1024);
    }
}
