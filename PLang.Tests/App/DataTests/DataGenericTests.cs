using app.error;
using app.variable;
using Type = global::app.type.@this;

namespace PLang.Tests.App.DataTests;

public class DataGenericTests
{
    [Test]
    public async Task Ok_StoresTypedValue()
    {
        var data = global::app.data.@this<global::app.type.text.@this>.Ok("hello");

        await Assert.That(data.Value).IsEqualTo("hello");
        await data.IsSuccess();
    }

    [Test]
    public async Task Ok_WithType_SetsType()
    {
        var data = global::app.data.@this<int>.Ok(42, Type.Int);

        await Assert.That(data.Value).IsEqualTo(42);
        await Assert.That(data.Type).IsNotNull();
        await Assert.That(data.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Value_ReturnsTypedValue()
    {
        var data = global::app.data.@this<global::app.type.text.@this>.Ok("world");

        string? typed = data.Value;

        await Assert.That(typed).IsEqualTo("world");
    }

    [Test]
    public async Task Value_WrongType_ReturnsDefault()
    {
        // Create a global::app.data.@this<int> then set base value to a string via base class
        var data = new global::app.data.@this<int>("test", 42);
        ((Data)data).Value = "not an int";

        await Assert.That(data.Value).IsEqualTo(default(int));
    }

    [Test]
    public async Task Fail_CreatesErrorResult()
    {
        var error = new ServiceError("something failed", "TestError", 500);
        var data = global::app.data.@this<global::app.type.text.@this>.FromError(error);

        await data.IsFailure();
        await Assert.That(data.Error).IsNotNull();
        await Assert.That(data.Error!.Message).IsEqualTo("something failed");
    }

    [Test]
    public async Task IsAssignableToData()
    {
        global::app.data.@this<global::app.type.text.@this> typed = global::app.data.@this<global::app.type.text.@this>.Ok("test");
        Data untyped = typed;

        await untyped.IsSuccess();
        await Assert.That(untyped.Value!.ToString()).IsEqualTo("test");
    }

    [Test]
    public async Task TaskOfData_WorksWithGeneric()
    {
        Task<Data> task = Task.FromResult<Data>(global::app.data.@this<int>.Ok(99));
        var result = await task;

        await result.IsSuccess();
        await Assert.That(result.Value).IsEqualTo(99);
    }
}
