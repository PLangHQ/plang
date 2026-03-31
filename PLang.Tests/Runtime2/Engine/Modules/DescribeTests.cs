using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Modules;

namespace PLang.Tests.Runtime2.Engine.Modules;

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
[PLang.Runtime2.modules.Action("getpath")]
public class FakeGetPath : PLang.Runtime2.modules.ICodeGenerated
{
    public Task<FakePathResult> Run() => Task.FromResult(new FakePathResult());

    public Task<Data> ExecuteAsync(List<Data> parameters,
        PLang.Runtime2.Engine.@this engine,
        PLang.Runtime2.Engine.Context.PLangContext context,
        List<Data>? defaults) => Task.FromResult(Data.Ok());
}

// Fake action returning plain Data
[PLang.Runtime2.modules.Action("basic")]
public class FakeBasicAction : PLang.Runtime2.modules.ICodeGenerated
{
    public Task<Data> Run() => Task.FromResult(Data.Ok("hello"));

    public Task<Data> ExecuteAsync(List<Data> parameters,
        PLang.Runtime2.Engine.@this engine,
        PLang.Runtime2.Engine.Context.PLangContext context,
        List<Data>? defaults) => Task.FromResult(Data.Ok());
}

// Fake return type simulating PathData-like properties
public class FakePathResult : Data
{
    public FakePathResult() : base("") { }
    public bool Exists { get; set; }
    public long Size { get; set; }
    public string FileName { get; set; } = "";
}
