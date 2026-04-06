using App.Variables;
using App.Modules;

namespace PLang.Tests.App.Modules;

public class DescribeTests
{
    [Test]
    public async Task Describe_ActionWithConcreteReturnType_IncludesReturnTypeProperties()
    {
        var modules = new @this();
        modules.RegisterType("testmod", "getpath", typeof(FakeGetPath));

        var actions = modules.Describe();
        var action = actions.First(a => a.Module == "testmod" && a.ActionName == "getpath");

        await Assert.That(action.ReturnType).IsNotNull();
        await Assert.That(action.ReturnType!.Count).IsGreaterThan(0);

        var existsProp = action.ReturnType!.FirstOrDefault(d => d.Name == "Exists");
        await Assert.That(existsProp).IsNotNull();
        await Assert.That(existsProp!.Value!.ToString()).IsEqualTo("bool");

        var sizeProp = action.ReturnType!.FirstOrDefault(d => d.Name == "Size");
        await Assert.That(sizeProp).IsNotNull();
        await Assert.That(sizeProp!.Value!.ToString()).IsEqualTo("long");
    }

    [Test]
    public async Task Describe_ActionReturningData_HasNullReturnType()
    {
        var modules = new @this();
        modules.RegisterType("testmod", "basic", typeof(FakeBasicAction));

        var actions = modules.Describe();
        var action = actions.First(a => a.Module == "testmod" && a.ActionName == "basic");

        await Assert.That(action.ReturnType).IsNull();
    }
}

// Fake action with concrete return type
[App.modules.Action("getpath")]
public class FakeGetPath : App.modules.ICodeGenerated
{
    public Task<FakePathResult> Run() => Task.FromResult(new FakePathResult());

    public Task<Data> ExecuteAsync(App.Goals.Goal.Steps.Step.Actions.Action.@this action,
        App.@this engine,
        App.Context.@this context) => Task.FromResult(Data.Ok());
}

// Fake action returning plain Data
[App.modules.Action("basic")]
public class FakeBasicAction : App.modules.ICodeGenerated
{
    public Task<Data> Run() => Task.FromResult(Data.Ok("hello"));

    public Task<Data> ExecuteAsync(App.Goals.Goal.Steps.Step.Actions.Action.@this action,
        App.@this engine,
        App.Context.@this context) => Task.FromResult(Data.Ok());
}

// Fake return type simulating Path-like properties
public class FakePathResult : Data
{
    public FakePathResult() : base("") { }
    public bool Exists { get; set; }
    public long Size { get; set; }
    public string FileName { get; set; } = "";
}
