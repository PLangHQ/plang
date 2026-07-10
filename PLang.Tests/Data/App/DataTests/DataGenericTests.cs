using app.error;
using app.variable;
using Type = global::app.type.@this;

namespace PLang.Tests.App.DataTests;

public class DataGenericTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/DataGenericTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Ok_StoresTypedValue()
    {
        var data = global::app.data.@this<global::app.type.item.text.@this>.Ok("hello");

        await Assert.That((await data.Value())!.Clr<string>()!).IsEqualTo("hello");
        await data.IsSuccess();
    }

    [Test]
    public async Task Ok_WithType_SetsType()
    {
        var data = _app.User.Context.Ok(42, Type.Int);

        await Assert.That((await data.Value())?.ToString()).IsEqualTo("42");
        await Assert.That(data.Type).IsNotNull();
        await Assert.That(data.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Value_ReturnsTypedValue()
    {
        var data = global::app.data.@this<global::app.type.item.text.@this>.Ok("world");

        string? typed = (await data.Value())?.Clr<string>();

        await Assert.That(typed).IsEqualTo("world");
    }

    [Test]
    public async Task Value_WrongType_ReturnsDefault()
    {
        // Create a global::app.data.@this<global::app.type.item.number.@this> then set base value to a string via base class
        var data = new global::app.data.@this<global::app.type.item.number.@this>("test", 42, context: _app.User.Context);
        ((Data)data).SetValue("not an int");

        // Born-native: number is a reference wrapper, so a failed conversion yields its
        // default — null — not the value-type 0.
        await Assert.That((await data.Value())).IsNull();
    }

    [Test]
    public async Task Fail_CreatesErrorResult()
    {
        var error = new ServiceError("something failed", "TestError", 500);
        var data = global::app.data.@this<global::app.type.item.text.@this>.FromError(error);

        await data.IsFailure();
        await Assert.That(data.Error).IsNotNull();
        await Assert.That(data.Error!.Message).IsEqualTo("something failed");
    }

    [Test]
    public async Task IsAssignableToData()
    {
        global::app.data.@this<global::app.type.item.text.@this> typed = global::app.data.@this<global::app.type.item.text.@this>.Ok("test");
        Data untyped = typed;

        await untyped.IsSuccess();
        await Assert.That((await untyped.Value())!.ToString()).IsEqualTo("test");
    }

    [Test]
    public async Task TaskOfData_WorksWithGeneric()
    {
        Task<Data> task = Task.FromResult<Data>(global::app.data.@this<global::app.type.item.number.@this>.Ok(99));
        var result = await task;

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("99");
    }
}
