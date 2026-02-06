using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;

namespace PLang.Tests.Runtime2.Core;

public class GoalResultTests
{
    [Test]
    public async Task Ok_NoValue_ReturnsSuccess()
    {
        var result = GoalResult.Ok();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task Ok_WithValue_ReturnsSuccessWithValue()
    {
        var value = "test value";

        var result = GoalResult.Ok(value);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(value);
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task Ok_WithNullValue_ReturnsSuccessWithNull()
    {
        var result = GoalResult.Ok(null);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task Fail_WithErrorInfo_ReturnsError()
    {
        var error = new ErrorInfo("Test error", "TestKey", 500);

        var result = GoalResult.Fail(error);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Value).IsNull();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Test error");
    }

    [Test]
    public async Task Fail_WithMessage_ReturnsErrorWithDefaultKey()
    {
        var result = GoalResult.Fail("Something went wrong");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Something went wrong");
        await Assert.That(result.Error!.Key).IsEqualTo("Error");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Fail_WithMessageAndKeyAndStatusCode_ReturnsCustomError()
    {
        var result = GoalResult.Fail("Not found", "NotFound", 404);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).IsEqualTo("Not found");
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task SuccessTask_NoValue_ReturnsCompletedTaskWithSuccess()
    {
        var task = GoalResult.SuccessTask();

        var result = await task;

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task SuccessTask_WithValue_ReturnsCompletedTaskWithValue()
    {
        var value = 42;

        var task = GoalResult.SuccessTask(value);
        var result = await task;

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(value);
    }

    [Test]
    public async Task ErrorTask_WithErrorInfo_ReturnsCompletedTaskWithError()
    {
        var error = new ErrorInfo("Error message");

        var task = GoalResult.ErrorTask(error);
        var result = await task;

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).IsEqualTo("Error message");
    }

    [Test]
    public async Task ErrorTask_WithMessage_ReturnsCompletedTaskWithError()
    {
        var task = GoalResult.ErrorTask("Error occurred", "ErrorKey", 503);
        var result = await task;

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).IsEqualTo("Error occurred");
        await Assert.That(result.Error!.Key).IsEqualTo("ErrorKey");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(503);
    }

    [Test]
    public async Task GetValue_WithMatchingType_ReturnsTypedValue()
    {
        var result = GoalResult.Ok("hello");

        var value = result.GetValue<string>();

        await Assert.That(value).IsEqualTo("hello");
    }

    [Test]
    public async Task GetValue_WithMismatchedType_ReturnsDefault()
    {
        var result = GoalResult.Ok("hello");

        var value = result.GetValue<int>();

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task GetValue_WithNullValue_ReturnsDefault()
    {
        var result = GoalResult.Ok();

        var value = result.GetValue<string>();

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task GetValue_WithReferenceType_ReturnsNull()
    {
        var result = GoalResult.Ok();

        var value = result.GetValue<List<int>>();

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task ImplicitBool_SuccessResult_ReturnsTrue()
    {
        GoalResult result = GoalResult.Ok();

        bool boolValue = result;

        await Assert.That(boolValue).IsTrue();
    }

    [Test]
    public async Task ImplicitBool_FailureResult_ReturnsFalse()
    {
        GoalResult result = GoalResult.Fail("error");

        bool boolValue = result;

        await Assert.That(boolValue).IsFalse();
    }

    [Test]
    public async Task ImplicitBool_CanBeUsedInIfStatement()
    {
        var successResult = GoalResult.Ok();
        var failResult = GoalResult.Fail("error");

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
        var result = GoalResult.Ok("test value");

        var str = result.ToString();

        await Assert.That(str).IsEqualTo("test value");
    }

    [Test]
    public async Task ToString_SuccessWithNullValue_ReturnsSuccessNoValue()
    {
        var result = GoalResult.Ok();

        var str = result.ToString();

        await Assert.That(str).IsEqualTo("Success (no value)");
    }

    [Test]
    public async Task ToString_Failure_ReturnsErrorMessage()
    {
        var result = GoalResult.Fail("Something went wrong");

        var str = result.ToString();

        await Assert.That(str).IsEqualTo("Error: Something went wrong");
    }

    [Test]
    public async Task GetValue_WithIntegerInObjectValue_ReturnsInteger()
    {
        var result = GoalResult.Ok(42);

        var value = result.GetValue<int>();

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Ok_WithComplexObject_ReturnsSuccessWithObject()
    {
        var data = new { Name = "Test", Value = 123 };

        var result = GoalResult.Ok(data);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNotNull();
    }
}
