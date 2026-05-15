using global::app.Variables;
using AppModules = global::app.Modules.@this;

namespace PLang.Tests.App.Modules;

public class DescribeTests
{
    [Test]
    public async Task Describe_ActionWithConcreteReturnType_IncludesReturnTypeProperties()
    {
        var modules = new AppModules();
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
        var modules = new AppModules();
        modules.RegisterType("testmod", "basic", typeof(FakeBasicAction));

        var actions = modules.Describe();
        var action = actions.First(a => a.Module == "testmod" && a.ActionName == "basic");

        await Assert.That(action.ReturnType).IsNull();
    }

    [Test]
    public async Task Describe_DataWrappedProperty_ShowsInnerTypeName()
    {
        var modules = new AppModules();
        modules.RegisterType("testmod", "datapath", typeof(FakeDataPathAction));

        var actions = modules.Describe();
        var action = actions.First(a => a.Module == "testmod" && a.ActionName == "datapath");

        var pathParam = action.Parameters!.FirstOrDefault(d => d.Name == "Path");
        await Assert.That(pathParam).IsNotNull();
        await Assert.That(pathParam!.Value!.ToString()).Contains("path");
    }
}

// Fake action with Data<T> wrapped property
[global::app.modules.Action("datapath")]
public record FakeDataPathAction : global::app.modules.ICodeGenerated
{
    public global::app.Data.@this<global::app.FileSystem.Path> Path { get; init; }

    public Task<Data> Run() => Task.FromResult(Data.Ok());

    public Task<Data> ExecuteAsync(global::app.Goals.Goal.Steps.Step.Actions.Action.@this action,
        global::app.Actor.Context.@this context) => Task.FromResult(Data.Ok());
}

// Fake action with concrete return type
[global::app.modules.Action("getpath")]
public class FakeGetPath : global::app.modules.ICodeGenerated
{
    public Task<FakePathResult> Run() => Task.FromResult(new FakePathResult());

    public Task<Data> ExecuteAsync(global::app.Goals.Goal.Steps.Step.Actions.Action.@this action,
        global::app.Actor.Context.@this context) => Task.FromResult(Data.Ok());
}

// Fake action returning plain Data
[global::app.modules.Action("basic")]
public class FakeBasicAction : global::app.modules.ICodeGenerated
{
    public Task<Data> Run() => Task.FromResult(Data.Ok("hello"));

    public Task<Data> ExecuteAsync(global::app.Goals.Goal.Steps.Step.Actions.Action.@this action,
        global::app.Actor.Context.@this context) => Task.FromResult(Data.Ok());
}

// Fake return type simulating Path-like properties
public class FakePathResult : Data
{
    public FakePathResult() : base("") { }
    public bool Exists { get; set; }
    public long Size { get; set; }
    public string FileName { get; set; } = "";
}
