using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;

namespace PLang.Tests.Runtime2.Errors;

public class ErrorTests
{
    [Test]
    public async Task Constructor_WithMessage_SetsDefaults()
    {
        var error = new Error("Test error");

        await Assert.That(error.Message).IsEqualTo("Test error");
        await Assert.That(error.Key).IsEqualTo("Error");
        await Assert.That(error.StatusCode).IsEqualTo(400);
        await Assert.That(error.Id).IsNotNull();
        await Assert.That(error.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_WithAllParameters_SetsValues()
    {
        var error = new Error("Not found", "NotFound", 404);

        await Assert.That(error.Message).IsEqualTo("Not found");
        await Assert.That(error.Key).IsEqualTo("NotFound");
        await Assert.That(error.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Constructor_SetsCreatedUtc()
    {
        var before = DateTime.UtcNow;

        var error = new Error("Test");

        var after = DateTime.UtcNow;
        await Assert.That(error.CreatedUtc).IsGreaterThanOrEqualTo(before);
        await Assert.That(error.CreatedUtc).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_GeneratesUniqueId()
    {
        var error1 = new Error("Error 1");
        var error2 = new Error("Error 2");

        await Assert.That(error1.Id).IsNotEqualTo(error2.Id);
    }

    [Test]
    public async Task FixSuggestion_CanBeSet()
    {
        var error = new Error("Error") { FixSuggestion = "Try restarting" };

        await Assert.That(error.FixSuggestion).IsEqualTo("Try restarting");
    }

    [Test]
    public async Task HelpfulLinks_CanBeSet()
    {
        var error = new Error("Error") { HelpfulLinks = "https://docs.example.com" };

        await Assert.That(error.HelpfulLinks).IsEqualTo("https://docs.example.com");
    }

    [Test]
    public async Task Exception_CanBeSet()
    {
        var ex = new InvalidOperationException("Test exception");
        var error = new Error("Error") { Exception = ex };

        await Assert.That(error.Exception).IsEqualTo(ex);
    }

    [Test]
    public async Task InnerError_CanBeSet()
    {
        var inner = new Error("Inner error");
        var error = new Error("Outer error") { InnerError = inner };

        await Assert.That(error.InnerError).IsNotNull();
        await Assert.That(error.InnerError!.Message).IsEqualTo("Inner error");
    }

    [Test]
    public async Task FromException_CreatesErrorFromException()
    {
        var ex = new InvalidOperationException("Something failed");

        var error = Error.FromException(ex);

        await Assert.That(error.Message).IsEqualTo("Something failed");
        await Assert.That(error.Key).IsEqualTo("Exception");
        await Assert.That(error.StatusCode).IsEqualTo(500);
        await Assert.That(error.Exception).IsEqualTo(ex);
    }

    [Test]
    public async Task FromException_WithCustomKeyAndStatusCode_UsesProvidedValues()
    {
        var ex = new ArgumentException("Bad argument");

        var error = Error.FromException(ex, "ValidationError", 400);

        await Assert.That(error.Key).IsEqualTo("ValidationError");
        await Assert.That(error.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task FromException_WithInnerException_CreatesNestedError()
    {
        var inner = new InvalidOperationException("Inner");
        var outer = new Exception("Outer", inner);

        var error = Error.FromException(outer);

        await Assert.That(error.Message).IsEqualTo("Outer");
        await Assert.That(error.InnerError).IsNotNull();
        await Assert.That(error.InnerError!.Message).IsEqualTo("Inner");
    }

    [Test]
    public async Task FromException_WithNoInnerException_HasNullInnerError()
    {
        var ex = new Exception("Single error");

        var error = Error.FromException(ex);

        await Assert.That(error.InnerError).IsNull();
    }

    [Test]
    public async Task ToString_ReturnsFormattedString()
    {
        var error = new Error("Test error", "TestKey", 400);

        var str = error.ToString();

        await Assert.That(str).IsEqualTo("[TestKey] Test error");
    }

    [Test]
    public async Task FromException_DeeplyNestedExceptions_CreatesChain()
    {
        var level3 = new Exception("Level 3");
        var level2 = new Exception("Level 2", level3);
        var level1 = new Exception("Level 1", level2);

        var error = Error.FromException(level1);

        await Assert.That(error.Message).IsEqualTo("Level 1");
        await Assert.That(error.InnerError!.Message).IsEqualTo("Level 2");
        await Assert.That(error.InnerError!.InnerError!.Message).IsEqualTo("Level 3");
        await Assert.That(error.InnerError!.InnerError!.InnerError).IsNull();
    }

    [Test]
    public async Task GoalName_CanBeSet()
    {
        var error = new Error("Error") { GoalName = "TestGoal" };

        await Assert.That(error.GoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task StepIndex_CanBeSet()
    {
        var error = new Error("Error") { StepIndex = 3 };

        await Assert.That(error.StepIndex).IsEqualTo(3);
    }

}

public class GoalErrorTests
{
    [Test]
    public async Task Constructor_SetsDefaultKey()
    {
        var error = new GoalError("Goal failed");

        await Assert.That(error.Key).IsEqualTo("GoalError");
    }

    [Test]
    public async Task NotFound_CreatesNotFoundError()
    {
        var error = GoalError.NotFound("Start");

        await Assert.That(error.Message).IsEqualTo("Goal 'Start' not found");
        await Assert.That(error.Key).IsEqualTo("NotFound");
        await Assert.That(error.StatusCode).IsEqualTo(404);
        await Assert.That(error.GoalName).IsEqualTo("Start");
    }

    [Test]
    public async Task Cancelled_CreatesCancelledError()
    {
        var error = GoalError.Cancelled();

        await Assert.That(error.Message).IsEqualTo("Execution cancelled");
        await Assert.That(error.Key).IsEqualTo("Cancelled");
        await Assert.That(error.StatusCode).IsEqualTo(499);
    }
}

public class ActionErrorTests
{
    [Test]
    public async Task Constructor_SetsDefaultKey()
    {
        var error = new ActionError("Action failed");

        await Assert.That(error.Key).IsEqualTo("ActionError");
    }

    [Test]
    public async Task NotFound_CreatesNotFoundError()
    {
        var error = ActionError.NotFound("variable.set");

        await Assert.That(error.Message).IsEqualTo("variable.set not found");
        await Assert.That(error.Key).IsEqualTo("ActionNotFound");
        await Assert.That(error.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task ActionClass_CanBeSet()
    {
        var error = new ActionError("Error") { ActionClass = "variable", ActionMethod = "set" };

        await Assert.That(error.ActionClass).IsEqualTo("variable");
        await Assert.That(error.ActionMethod).IsEqualTo("set");
    }
}

public class ServiceErrorTests
{
    [Test]
    public async Task Constructor_SetsDefaultKey()
    {
        var error = new ServiceError("Service failed");

        await Assert.That(error.Key).IsEqualTo("ServiceError");
    }

    [Test]
    public async Task FromException_CreatesServiceError()
    {
        var ex = new Exception("Service crashed");

        var error = ServiceError.FromException(ex);

        await Assert.That(error).IsTypeOf<ServiceError>();
        await Assert.That(error.Message).IsEqualTo("Service crashed");
        await Assert.That(error.Key).IsEqualTo("Exception");
        await Assert.That(error.StatusCode).IsEqualTo(500);
    }
}

public class StepErrorTests
{
    [Test]
    public async Task Constructor_SetsDefaultKey()
    {
        var error = new StepError("Step failed");

        await Assert.That(error.Key).IsEqualTo("StepError");
    }

    [Test]
    public async Task FromException_CreatesStepErrorWithStep()
    {
        var ex = new Exception("Step crashed");
        var appContext = new PLangAppContext("/app");
        using var context = new PLangContext(appContext);
        var step = new Step { Text = "test step" };
        context.Step = step;

        var error = StepError.FromException(ex, context);

        await Assert.That(error).IsTypeOf<StepError>();
        await Assert.That(error.Message).IsEqualTo("Step crashed");
        await Assert.That(error.Step).IsEqualTo(step);
    }
}
