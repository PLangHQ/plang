using app.type.item.variable;

namespace PLang.Tests.App.Modules;

public class DescribeTests
{
    [Test]
    public async Task Describe_DataWrappedProperty_ShowsInnerTypeName()
    {
        var app = TestApp.Create("/test"); var modules = app.Module;
        modules.RegisterType("testmod", "datapath", typeof(FakeDataPathAction));

        var action = modules["testmod"]["datapath"];
        await Assert.That(action).IsNotNull();

        var pathParam = action!.Property.First(r => r.Name == "Path");
        await Assert.That(pathParam.Type.Name).IsEqualTo("path");
    }
}

// Fake action with Data<T> wrapped property
[global::app.module.Action("datapath")]
public record FakeDataPathAction : global::app.module.ICodeGenerated
{
    public global::app.data.@this<global::app.type.item.path.@this> Path { get; init; }

    public Task<Data> Start() => Task.FromResult(Data.Ok());

    Task<Data> global::app.module.ICodeGenerated.Start() => Task.FromResult(Data.Ok());
}
