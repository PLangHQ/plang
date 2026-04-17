using global::App.Actor.Context;
using App;
using global::App.Variables;

namespace PLang.Tests.App.actions.variable;

public class SetTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/app");
    }

    [Test]
    public async Task Set_SetsVariable()
    {
        var context = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%testVar%"), ("value", "testValue"));
        var result = await _app.Run(action, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("testVar")).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_SetsTypeInfo()
    {
        var context = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%count%"), ("value", 42), ("type", "int"));
        var result = await _app.Run(action, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.Get("count")!.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Set_ReturnsOk()
    {
        var context = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%testVar%"), ("value", "testValue"));
        var result = await _app.Run(action, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("testVar")).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_SetsTypeOnStoredVariable()
    {
        var context = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%count%"), ("value", 42), ("type", "int"));
        var result = await _app.Run(action, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.Get("count")!.Type!.Value).IsEqualTo("int");
    }

    [Test]
    public async Task Set_AsDefault_DoesNotOverwriteExisting()
    {
        var context = _app.Context;

        // Set initial value
        var setAction = TestAction.Create("variable", "set", ("name", "%x%"), ("value", "original"));
        await _app.Run(setAction, context);

        // Try to set default — should not overwrite
        var defaultAction = TestAction.Create("variable", "set", ("name", "%x%"), ("value", "default"), ("asdefault", true));
        var result = await _app.Run(defaultAction, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("x")).IsEqualTo("original");
    }

    [Test]
    public async Task Set_AsDefault_SetsWhenNotExists()
    {
        var context = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%y%"), ("value", "default"), ("asdefault", true));
        var result = await _app.Run(action, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("y")).IsEqualTo("default");
    }

    // --- ValidateBuild tests ---

    [Test]
    public async Task ValidateBuild_LiteralThis_ReturnsError()
    {
        var parameters = new List<Data> { new Data("Value", "this") };
        var result = global::App.modules.variable.Set.ValidateBuild(parameters);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("this");
    }

    [Test]
    public async Task ValidateBuild_VariableReference_ReturnsNull()
    {
        var parameters = new List<Data>
        {
            new Data("Value", "%myVar%", global::App.Data.Type.FromName("int"))
        };
        var result = global::App.modules.variable.Set.ValidateBuild(parameters);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ValidateBuild_TypeMismatch_ReturnsError()
    {
        var parameters = new List<Data>
        {
            new Data("Value", "not a number", global::App.Data.Type.FromName("int"))
        };
        var result = global::App.modules.variable.Set.ValidateBuild(parameters);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("type=int");
    }

    [Test]
    public async Task ValidateBuild_ValidTypeMatch_ReturnsNull()
    {
        var parameters = new List<Data>
        {
            new Data("Value", 42, global::App.Data.Type.FromName("int"))
        };
        var result = global::App.modules.variable.Set.ValidateBuild(parameters);

        await Assert.That(result).IsNull();
    }
}
