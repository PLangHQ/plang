using App.Engine;
using App.Engine.Context;
using App.Engine.Variables;
using App.Engine.Settings;
using App.actions.archive;
using EngineType = App.Engine.@this;

namespace PLang.Tests.Runtime2.Engine.Settings;

public class ModuleViewTests
{
    private (EngineType engine, PLangContext context) CreateEngine()
    {
        var engine = new EngineType("/app");
        var context = new PLangContext(engine, new Variables());
        return (engine, context);
    }

    [Test]
    public void For_ReturnsModuleView()
    {
        var (engine, context) = CreateEngine();

        var view = engine.Settings.For<ArchiveSettings>(context);

        Assert.That(view).IsNotNull();
    }

    [Test]
    public void ModuleView_ResolvesClassDefault()
    {
        // No settings set — ModuleView should resolve to the class default.
        var (engine, context) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;

        var view = engine.Settings.For<ArchiveSettings>(context);
        var result = view.Resolve<long>("max", classDefault);

        Assert.That(result).IsEqualTo(classDefault);
    }

    [Test]
    public void ModuleView_ResolvesGoalScopedValue()
    {
        // A value has been set in goal scope — ModuleView should find it.
        var (engine, context) = CreateEngine();
        long classDefault = 100 * 1024 * 1024;
        long goalValue = 20 * 1024 * 1024;

        engine.Settings.Set("archive.max", goalValue, context);

        var view = engine.Settings.For<ArchiveSettings>(context);
        var result = view.Resolve<long>("max", classDefault);

        Assert.That(result).IsEqualTo(goalValue);
    }

    [Test]
    public void ModuleView_DifferentContextsGetDifferentValues()
    {
        // Two contexts with different settings. Each ModuleView should see its own value.
        var (engine, context1) = CreateEngine();
        var context2 = new PLangContext(engine, new Variables());
        long classDefault = 100 * 1024 * 1024;

        engine.Settings.Set("archive.max", 20L * 1024 * 1024, context1);
        engine.Settings.Set("archive.max", 50L * 1024 * 1024, context2);

        var view1 = engine.Settings.For<ArchiveSettings>(context1);
        var view2 = engine.Settings.For<ArchiveSettings>(context2);

        var result1 = view1.Resolve<long>("max", classDefault);
        var result2 = view2.Resolve<long>("max", classDefault);

        Assert.That(result1).IsEqualTo(20L * 1024 * 1024);
        Assert.That(result2).IsEqualTo(50L * 1024 * 1024);
    }
}
