using app.error;
using app.variable;

namespace PLang.Tests.App.Core;

public class DataResultTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/DataResultTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Ok_NoValue_ReturnsSuccess()
    {
        var result = app.Ok();

        await result.IsSuccess();
        // A no-value Ok holds the plang null citizen (a real item), not C# null.
        await Assert.That((await result.Value())!.IsNull).IsTrue();
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task Ok_WithValue_ReturnsSuccessWithValue()
    {
        var value = "test value";

        var result = app.Ok(value);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo(value);
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task Ok_WithNullValue_ReturnsSuccessWithNull()
    {
        var result = app.Ok(null);

        await result.IsSuccess();
        // Ok(null) holds the plang null citizen (a real item), not C# null.
        await Assert.That((await result.Value())!.IsNull).IsTrue();
    }

    [Test]
    public async Task Fail_WithError_ReturnsError()
    {
        var error = new Error("Test error", "TestKey", 500);

        var result = app.Error(error);

        await result.IsFailure();
        await Assert.That(await (await result.Value())!.IsEmpty()).IsTrue();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Test error");
    }

    [Test]
    public async Task Fail_WithErrorMessage_ReturnsErrorWithDefaultKey()
    {
        var result = app.Error(new Error("Something went wrong"));

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Something went wrong");
        await Assert.That(result.Error!.Key).IsEqualTo("Error");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Fail_WithMessageAndKeyAndStatusCode_ReturnsCustomError()
    {
        var result = app.Error(new Error("Not found", "NotFound", 404));

        await result.IsFailure();
        await Assert.That(result.Error!.Message).IsEqualTo("Not found");
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task GetValue_WithMatchingType_ReturnsTypedValue()
    {
        var result = app.Ok("hello");

        var value = result.GetValue<string>();

        await Assert.That(value).IsEqualTo("hello");
    }

    [Test]
    public async Task GetValue_WithMismatchedType_ReturnsDefault()
    {
        var result = app.Ok("hello");

        var value = result.GetValue<int>();

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task GetValue_WithNullValue_ReturnsDefault()
    {
        var result = app.Ok();

        var value = result.GetValue<string>();

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task GetValue_WithReferenceType_ReturnsNull()
    {
        var result = app.Ok();

        var value = result.GetValue<List<int>>();

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task ImplicitBool_SuccessResult_ReturnsTrue()
    {
        Data result = app.Ok();

        bool boolValue = result;

        await Assert.That(boolValue).IsTrue();
    }

    [Test]
    public async Task ImplicitBool_FailureResult_ReturnsFalse()
    {
        Data result = app.Error(new Error("error"));

        bool boolValue = result;

        await Assert.That(boolValue).IsFalse();
    }

    [Test]
    public async Task ImplicitBool_CanBeUsedInIfStatement()
    {
        var successResult = app.Ok();
        var failResult = app.Error(new Error("error"));

        var successPassed = false;
        var failPassed = true;

        if (successResult) successPassed = true;
        if (failResult) failPassed = false;

        await Assert.That(successPassed).IsTrue();
        await Assert.That(failPassed).IsTrue();
    }

    [Test]
    public async Task ToString_SuccessWithValue_ReturnsValueString()
    {
        var result = app.Ok("test value");

        var str = result.ToString();

        await Assert.That(str).IsEqualTo("test value");
    }

    [Test]
    public async Task ToString_SuccessWithNullValue_ReturnsNull()
    {
        var result = app.Ok();

        var str = result.ToString();

        // A no-value Data renders the plang null citizen — "null", not "(null)".
        await Assert.That(str).IsEqualTo("null");
    }

    [Test]
    public async Task ToString_Failure_ReturnsErrorMessage()
    {
        var result = app.Error(new Error("Something went wrong"));

        var str = result.ToString();

        await Assert.That(str).IsEqualTo("Error: Something went wrong");
    }

    [Test]
    public async Task GetValue_WithIntegerInObjectValue_ReturnsInteger()
    {
        var result = app.Ok(42);

        var value = result.GetValue<int>();

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Ok_WithComplexObject_ReturnsSuccessWithObject()
    {
        var data = new { Name = "Test", Value = 123 };

        var result = app.Ok(data);

        await result.IsSuccess();
        await Assert.That((await result.Value())).IsNotNull();
    }

    [Test]
    public async Task Value_IsMutable()
    {
        var result = app.Ok("initial");
        result.SetValue("changed");

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("changed");
    }

    [Test]
    public async Task Error_IsMutable()
    {
        var result = app.Ok();
        await result.IsSuccess();

        result.Error = new Error("now failed");
        await result.IsFailure();
    }
}
