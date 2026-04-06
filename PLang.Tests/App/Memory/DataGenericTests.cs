using App.Errors;
using App.Variables;
using Type = App.Variables.Type;

namespace PLang.Tests.App.Memory;

public class DataGenericTests
{
    [Test]
    public async Task Ok_StoresTypedValue()
    {
        var data = Data<string>.Ok("hello");

        await Assert.That(data.Value).IsEqualTo("hello");
        await Assert.That(data.Success).IsTrue();
    }

    [Test]
    public async Task Ok_WithType_SetsType()
    {
        var data = Data<int>.Ok(42, Type.Int);

        await Assert.That(data.Value).IsEqualTo(42);
        await Assert.That(data.Type).IsNotNull();
        await Assert.That(data.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Value_ReturnsTypedValue()
    {
        var data = Data<string>.Ok("world");

        string? typed = data.Value;

        await Assert.That(typed).IsEqualTo("world");
    }

    [Test]
    public async Task Value_WrongType_ReturnsDefault()
    {
        // Create a Data<int> then set base value to a string via base class
        var data = new Data<int>("test", 42);
        ((Data)data).Value = "not an int";

        await Assert.That(data.Value).IsEqualTo(default(int));
    }

    [Test]
    public async Task Fail_CreatesErrorResult()
    {
        var error = new ServiceError("something failed", "TestError", 500);
        var data = Data<string>.FromError(error);

        await Assert.That(data.Success).IsFalse();
        await Assert.That(data.Error).IsNotNull();
        await Assert.That(data.Error!.Message).IsEqualTo("something failed");
    }

    [Test]
    public async Task IsAssignableToData()
    {
        Data<string> typed = Data<string>.Ok("test");
        Data untyped = typed;

        await Assert.That(untyped.Success).IsTrue();
        await Assert.That(untyped.Value).IsEqualTo("test");
    }

    [Test]
    public async Task TaskOfData_WorksWithGeneric()
    {
        Task<Data> task = Task.FromResult<Data>(Data<int>.Ok(99));
        var result = await task;

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(99);
    }
}
