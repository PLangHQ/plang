using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Config;
using SigningConfig = PLang.Runtime2.modules.signing.Config;
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

        var view = engine.Config.For<SigningConfig>(context);

        await Assert.That(view).IsNotNull();
    }

    [Test]
    public async Task ModuleView_ResolvesClassDefault()
    {
        var (engine, context) = CreateEngine();
        long classDefault = 300_000;

        var view = engine.Config.For<SigningConfig>(context);
        var result = view.Resolve<long>("TimeoutMs", classDefault);

        await Assert.That(result).IsEqualTo(classDefault);
    }

    [Test]
    public async Task ModuleView_ResolvesGoalScopedValue()
    {
        var (engine, context) = CreateEngine();
        long classDefault = 300_000;
        long goalValue = 60_000;

        engine.Config.Set("signing.TimeoutMs", goalValue, context);

        var view = engine.Config.For<SigningConfig>(context);
        var result = view.Resolve<long>("TimeoutMs", classDefault);

        await Assert.That(result).IsEqualTo(goalValue);
    }

    [Test]
    public async Task ModuleView_DifferentContextsGetDifferentValues()
    {
        var (engine, context1) = CreateEngine();
        var context2 = new PLangContext(engine, new MemoryStack());
        long classDefault = 300_000;

        engine.Config.Set("signing.TimeoutMs", 60_000L, context1);
        engine.Config.Set("signing.TimeoutMs", 120_000L, context2);

        var view1 = engine.Config.For<SigningConfig>(context1);
        var view2 = engine.Config.For<SigningConfig>(context2);

        var result1 = view1.Resolve<long>("TimeoutMs", classDefault);
        var result2 = view2.Resolve<long>("TimeoutMs", classDefault);

        await Assert.That(result1).IsEqualTo(60_000L);
        await Assert.That(result2).IsEqualTo(120_000L);
    }
}
