using app.variable;

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

        var pathParam = action!.Property.Rows.First(r => r.Name == "Path");
        await Assert.That(pathParam.Type.Name).IsEqualTo("path");
    }
}

// Fake action with Data<T> wrapped property
[global::app.module.Action("datapath")]
public record FakeDataPathAction : global::app.module.ICodeGenerated
{
    public global::app.data.@this<global::app.type.item.path.@this> Path { get; init; }

    public Task<Data> Run() => Task.FromResult(Data.Ok());

    public Task<Data> Execute() => Task.FromResult(Data.Ok());
}
