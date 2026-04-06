using App.Context;
using App;
using App.Variables;

namespace PLang.Tests.App.Context;

public class ContextVariableTests
{
    private App.@this _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new App.@this("/test");
    }

    [Test]
    public async Task ContextVar_Engine_ReturnsEngineInstance()
    {
        var vars = _engine.Variables;
        var value = vars.GetValue("!engine");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsEqualTo(_engine);
    }

    [Test]
    public async Task ContextVar_Variables_ReturnsVariables()
    {
        var vars = _engine.Variables;
        var value = vars.GetValue("!variables");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsTypeOf<Variables>();
    }

    [Test]
    public async Task ContextVar_Context_ReturnsPLangContext()
    {
        var vars = _engine.Variables;
        var value = vars.GetValue("!context");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsTypeOf<Context.@this>();
    }

    [Test]
    public async Task ContextVar_FileSystem_ReturnsFileSystem()
    {
        var vars = _engine.Variables;
        var value = vars.GetValue("!fileSystem");

        await Assert.That(value).IsNotNull();
    }

    [Test]
    public async Task ContextVar_CallStack_ReturnsCallStack()
    {
        var vars = _engine.Variables;
        var value = vars.GetValue("!callStack");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsTypeOf<CallStack>();
    }

    [Test]
    public async Task ContextVar_Channels_ReturnsChannels()
    {
        var vars = _engine.Variables;
        var value = vars.GetValue("!channels");

        await Assert.That(value).IsNotNull();
    }

    [Test]
    public async Task ContextVar_Serializers_ReturnsSerializerRegistry()
    {
        var vars = _engine.Variables;
        var value = vars.GetValue("!serializers");

        await Assert.That(value).IsNotNull();
    }

    [Test]
    public async Task ContextVar_Goal_IsNullInitially()
    {
        var vars = _engine.Variables;
        var value = vars.GetValue("!goal");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task ContextVar_Step_IsNullInitially()
    {
        var vars = _engine.Variables;
        var value = vars.GetValue("!step");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task ContextVar_Goal_ReturnsDynamic_WhenSet()
    {
        var context = _engine.Context;
        var goal = new Goal { Name = "TestGoal" };
        context.Goal = goal;

        var vars = _engine.Variables;
        var value = vars.GetValue("!goal");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsEqualTo(goal);
    }

    [Test]
    public async Task ContextVar_Step_ReturnsDynamic_WhenSet()
    {
        var context = _engine.Context;
        var step = new Step { Index = 0, Text = "test step" };
        context.Step = step;

        var vars = _engine.Variables;
        var value = vars.GetValue("!step");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsEqualTo(step);
    }

    [Test]
    public async Task ContextVars_ExcludedFromGetNames()
    {
        var vars = _engine.Variables;
        vars.Set("regularVar", "hello");

        var names = vars.GetNames().ToList();

        await Assert.That(names).Contains("regularVar");
        await Assert.That(names).DoesNotContain("!engine");
        await Assert.That(names).DoesNotContain("!context");
        await Assert.That(names).DoesNotContain("!goal");
    }

    [Test]
    public async Task ContextVars_ExcludedFromGetAll()
    {
        var vars = _engine.Variables;
        vars.Set("regularVar", "hello");

        var all = vars.GetAll().ToList();
        var names = all.Select(d => d.Name).ToList();

        await Assert.That(names).Contains("regularVar");
        await Assert.That(names).DoesNotContain("!engine");
        await Assert.That(names).DoesNotContain("!goal");
    }

    [Test]
    public async Task ContextVars_SurviveClear()
    {
        var vars = _engine.Variables;
        vars.Set("regularVar", "hello");

        vars.Clear();

        // Regular var is gone
        await Assert.That(vars.GetValue("regularVar")).IsNull();

        // Context vars survive
        await Assert.That(vars.GetValue("!engine")).IsNotNull();
        await Assert.That(vars.GetValue("!context")).IsNotNull();
    }

    [Test]
    public async Task ContextVars_NotCloned()
    {
        var vars = _engine.Variables;
        vars.Set("regularVar", "hello");

        var clone = vars.Clone();

        // Regular var is cloned
        await Assert.That(clone.GetValue("regularVar")).IsEqualTo("hello");

        // Context vars are NOT cloned (they'd break as plain Data objects)
        await Assert.That(clone.Contains("!engine")).IsFalse();
    }

    [Test]
    public async Task DynamicData_ValueResolvesViaBaseReference()
    {
        // Proves the virtual/override fix: accessing .Value through a Data reference
        // correctly calls DynamicData.Value (not base Data.Value which returns null)
        var vars = _engine.Variables;

        // Now is a DynamicData registered by Variables constructor
        var nowValue = vars.GetValue("Now");
        await Assert.That(nowValue).IsNotNull();
        await Assert.That(nowValue).IsTypeOf<DateTimeOffset>();

        // !goal is a DynamicData registered by RegisterContextVariables
        var context = _engine.Context;
        var goal = new Goal { Name = "DynamicTest" };
        context.Goal = goal;

        var goalValue = vars.GetValue("!goal");
        await Assert.That(goalValue).IsNotNull();
        await Assert.That(goalValue).IsEqualTo(goal);
    }

    [Test]
    public async Task ContextVar_EngineProperty_AccessibleViaDotNotation()
    {
        var vars = _engine.Variables;
        var data = vars.Get("!engine.Name");

        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Value).IsEqualTo("App");
    }
}
