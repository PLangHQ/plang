using app;
using global::app.Actor.Context;
using global::app.Variables;
using global::app.Config;
using SigningConfig = global::app.modules.signing.Config;
using EngineType = global::app.@this;

namespace PLang.Tests.App.Settings;

public class ModuleViewTests
{
    private (EngineType engine, global::app.Actor.Context.@this context) CreateEngine()
    {
        var engine = new EngineType("/app");
        var context = new global::app.Actor.Context.@this(engine, new Variables());
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
        var context2 = new global::app.Actor.Context.@this(engine, new Variables());
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
