using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Tests.Runtime2.Context;

public class ContextVariableTests
{
    private Engine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new Engine("/test");
    }

    [Test]
    public async Task ContextVar_Engine_ReturnsEngineInstance()
    {
        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!engine");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsEqualTo(_engine);
    }

    [Test]
    public async Task ContextVar_MemoryStack_ReturnsMemoryStack()
    {
        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!memoryStack");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsTypeOf<MemoryStack>();
    }

    [Test]
    public async Task ContextVar_Context_ReturnsPLangContext()
    {
        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!context");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsTypeOf<PLangContext>();
    }

    [Test]
    public async Task ContextVar_FileSystem_ReturnsFileSystem()
    {
        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!fileSystem");

        await Assert.That(value).IsNotNull();
    }

    [Test]
    public async Task ContextVar_CallStack_ReturnsCallStack()
    {
        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!callStack");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsTypeOf<CallStack>();
    }

    [Test]
    public async Task ContextVar_Channels_ReturnsChannels()
    {
        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!channels");

        await Assert.That(value).IsNotNull();
    }

    [Test]
    public async Task ContextVar_Serializers_ReturnsSerializerRegistry()
    {
        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!serializers");

        await Assert.That(value).IsNotNull();
    }

    [Test]
    public async Task ContextVar_Goal_IsNullInitially()
    {
        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!goal");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task ContextVar_Step_IsNullInitially()
    {
        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!step");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task ContextVar_Goal_ReturnsDynamic_WhenSet()
    {
        var context = _engine.Context;
        var goal = new Goal { Name = "TestGoal" };
        context.Goal = goal;

        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!goal");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsEqualTo(goal);
    }

    [Test]
    public async Task ContextVar_Step_ReturnsDynamic_WhenSet()
    {
        var context = _engine.Context;
        var step = new Step { Index = 0, Text = "test step" };
        context.Step = step;

        var ms = _engine.MemoryStack;
        var value = ms.GetValue("!step");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsEqualTo(step);
    }

    [Test]
    public async Task ContextVars_ExcludedFromGetNames()
    {
        var ms = _engine.MemoryStack;
        ms.Set("regularVar", "hello");

        var names = ms.GetNames().ToList();

        await Assert.That(names).Contains("regularVar");
        await Assert.That(names).DoesNotContain("!engine");
        await Assert.That(names).DoesNotContain("!context");
        await Assert.That(names).DoesNotContain("!goal");
    }

    [Test]
    public async Task ContextVars_ExcludedFromGetAll()
    {
        var ms = _engine.MemoryStack;
        ms.Set("regularVar", "hello");

        var all = ms.GetAll().ToList();
        var names = all.Select(d => d.Name).ToList();

        await Assert.That(names).Contains("regularVar");
        await Assert.That(names).DoesNotContain("!engine");
        await Assert.That(names).DoesNotContain("!goal");
    }

    [Test]
    public async Task ContextVars_SurviveClear()
    {
        var ms = _engine.MemoryStack;
        ms.Set("regularVar", "hello");

        ms.Clear();

        // Regular var is gone
        await Assert.That(ms.GetValue("regularVar")).IsNull();

        // Context vars survive
        await Assert.That(ms.GetValue("!engine")).IsNotNull();
        await Assert.That(ms.GetValue("!context")).IsNotNull();
    }

    [Test]
    public async Task ContextVars_NotCloned()
    {
        var ms = _engine.MemoryStack;
        ms.Set("regularVar", "hello");

        var clone = ms.Clone();

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
        var ms = _engine.MemoryStack;

        // Now is a DynamicData registered by MemoryStack constructor
        var nowValue = ms.GetValue("Now");
        await Assert.That(nowValue).IsNotNull();
        await Assert.That(nowValue).IsTypeOf<DateTime>();

        // !goal is a DynamicData registered by RegisterContextVariables
        var context = _engine.Context;
        var goal = new Goal { Name = "DynamicTest" };
        context.Goal = goal;

        var goalValue = ms.GetValue("!goal");
        await Assert.That(goalValue).IsNotNull();
        await Assert.That(goalValue).IsEqualTo(goal);
    }

    [Test]
    public async Task ContextVar_EngineProperty_AccessibleViaDotNotation()
    {
        var ms = _engine.MemoryStack;
        var data = ms.Get("!engine.Name");

        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Value).IsEqualTo("Runtime2");
    }
}
