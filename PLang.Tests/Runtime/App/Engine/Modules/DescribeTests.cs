using app.variable;

namespace PLang.Tests.App.Modules;

public class DescribeTests
{
    [Test]
    public async Task Describe_ActionWithConcreteReturnType_IncludesReturnTypeProperties()
    {
        var app = new global::app.@this("/test"); var modules = app.Module;
        modules.RegisterType("testmod", "getpath", typeof(FakeGetPath));

        var actions = await modules.Describe();
        var action = actions.First(a => a.Module == "testmod" && a.ActionName == "getpath");

        await Assert.That(action.ReturnType).IsNotNull();
        await Assert.That(action.ReturnType!.Count).IsGreaterThan(0);

        var existsProp = action.ReturnType!.FirstOrDefault(d => d.Name == "Exists");
        await Assert.That(existsProp).IsNotNull();
        await Assert.That((await existsProp!.Value())!.ToString()).IsEqualTo("bool");

        var sizeProp = action.ReturnType!.FirstOrDefault(d => d.Name == "Size");
        await Assert.That(sizeProp).IsNotNull();
        // long → number (post-Stage-2: numerics surface as kinds of number).
        await Assert.That((await sizeProp!.Value())!.ToString()).IsEqualTo("number");
    }

    [Test]
    public async Task Describe_ActionReturningData_HasNullReturnType()
    {
        var app = new global::app.@this("/test"); var modules = app.Module;
        modules.RegisterType("testmod", "basic", typeof(FakeBasicAction));

        var actions = await modules.Describe();
        var action = actions.First(a => a.Module == "testmod" && a.ActionName == "basic");

        await Assert.That(action.ReturnType).IsNull();
    }

    [Test]
    public async Task Describe_DataWrappedProperty_ShowsInnerTypeName()
    {
        var app = new global::app.@this("/test"); var modules = app.Module;
        modules.RegisterType("testmod", "datapath", typeof(FakeDataPathAction));

        var actions = await modules.Describe();
        var action = actions.First(a => a.Module == "testmod" && a.ActionName == "datapath");

        var pathParam = action.Parameters!.FirstOrDefault(d => d.Name == "Path");
        await Assert.That(pathParam).IsNotNull();
        await Assert.That((await pathParam!.Value())!.ToString()).Contains("path");
    }
}

// Fake action with Data<T> wrapped property
[global::app.module.Action("datapath")]
public record FakeDataPathAction : global::app.module.ICodeGenerated
{
    public global::app.data.@this<global::app.type.path.@this> Path { get; init; }

    public Task<Data> Run() => Task.FromResult(Data.Ok());

    public Task<Data> ExecuteAsync(global::app.goal.steps.step.actions.action.@this action,
        global::app.actor.context.@this context) => Task.FromResult(Data.Ok());
}

// Fake action with concrete return type
[global::app.module.Action("getpath")]
public class FakeGetPath : global::app.module.ICodeGenerated
{
    public Task<FakePathResult> Run() => Task.FromResult(new FakePathResult());

    public Task<Data> ExecuteAsync(global::app.goal.steps.step.actions.action.@this action,
        global::app.actor.context.@this context) => Task.FromResult(Data.Ok());
}

// Fake action returning plain Data
[global::app.module.Action("basic")]
public class FakeBasicAction : global::app.module.ICodeGenerated
{
    public Task<Data> Run() => Task.FromResult(Data.Ok("hello"));

    public Task<Data> ExecuteAsync(global::app.goal.steps.step.actions.action.@this action,
        global::app.actor.context.@this context) => Task.FromResult(Data.Ok());
}

// Fake return type simulating Path-like properties
public class FakePathResult : Data
{
    public FakePathResult() : base("") { }
    public bool Exists { get; set; }
    public long Size { get; set; }
    public string FileName { get; set; } = "";
}
