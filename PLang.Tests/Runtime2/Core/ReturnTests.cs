using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;

namespace PLang.Tests.Runtime2.Core;

public class ReturnTests
{
    [Test]
    public async Task New_NoValue_ReturnsSuccess()
    {
        var result = new Return();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task New_WithValue_ReturnsSuccessWithValue()
    {
        var value = "test value";

        var result = new Return { Value = value };

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(value);
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task New_WithNullValue_ReturnsSuccessWithNull()
    {
        var result = new Return { Value = null };

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task New_WithError_ReturnsError()
    {
        var error = new Error("Test error", "TestKey", 500);

        var result = new Return { Error = error };

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Value).IsNull();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Test error");
    }

    [Test]
    public async Task New_WithErrorMessage_ReturnsErrorWithDefaultKey()
    {
        var result = new Return { Error = new Error("Something went wrong") };

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Something went wrong");
        await Assert.That(result.Error!.Key).IsEqualTo("Error");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task New_WithMessageAndKeyAndStatusCode_ReturnsCustomError()
    {
        var result = new Return { Error = new Error("Not found", "NotFound", 404) };

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).IsEqualTo("Not found");
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task GetValue_WithMatchingType_ReturnsTypedValue()
    {
        var result = new Return { Value = "hello" };

        var value = result.GetValue<string>();

        await Assert.That(value).IsEqualTo("hello");
    }

    [Test]
    public async Task GetValue_WithMismatchedType_ReturnsDefault()
    {
        var result = new Return { Value = "hello" };

        var value = result.GetValue<int>();

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task GetValue_WithNullValue_ReturnsDefault()
    {
        var result = new Return();

        var value = result.GetValue<string>();

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task GetValue_WithReferenceType_ReturnsNull()
    {
        var result = new Return();

        var value = result.GetValue<List<int>>();

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task ImplicitBool_SuccessResult_ReturnsTrue()
    {
        Return result = new();

        bool boolValue = result;

        await Assert.That(boolValue).IsTrue();
    }

    [Test]
    public async Task ImplicitBool_FailureResult_ReturnsFalse()
    {
        Return result = new() { Error = new Error("error") };

        bool boolValue = result;

        await Assert.That(boolValue).IsFalse();
    }

    [Test]
    public async Task ImplicitBool_CanBeUsedInIfStatement()
    {
        var successResult = new Return();
        var failResult = new Return { Error = new Error("error") };

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
        var result = new Return { Value = "test value" };

        var str = result.ToString();

        await Assert.That(str).IsEqualTo("test value");
    }

    [Test]
    public async Task ToString_SuccessWithNullValue_ReturnsSuccess()
    {
        var result = new Return();

        var str = result.ToString();

        await Assert.That(str).IsEqualTo("Success");
    }

    [Test]
    public async Task ToString_Failure_ReturnsErrorMessage()
    {
        var result = new Return { Error = new Error("Something went wrong") };

        var str = result.ToString();

        await Assert.That(str).IsEqualTo("Error: Something went wrong");
    }

    [Test]
    public async Task GetValue_WithIntegerInObjectValue_ReturnsInteger()
    {
        var result = new Return { Value = 42 };

        var value = result.GetValue<int>();

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task New_WithComplexObject_ReturnsSuccessWithObject()
    {
        var data = new { Name = "Test", Value = 123 };

        var result = new Return { Value = data };

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNotNull();
    }

    [Test]
    public async Task Value_IsMutable()
    {
        var result = new Return { Value = "initial" };
        result.Value = "changed";

        await Assert.That(result.Value).IsEqualTo("changed");
    }

    [Test]
    public async Task Error_IsMutable()
    {
        var result = new Return();
        await Assert.That(result.Success).IsTrue();

        result.Error = new Error("now failed");
        await Assert.That(result.Success).IsFalse();
    }
}
