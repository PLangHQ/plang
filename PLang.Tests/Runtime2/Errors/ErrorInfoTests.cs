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
    public async Task ErrorChain_IsEmptyByDefault()
    {
        var error = new Error("Error");

        await Assert.That(error.ErrorChain).IsNotNull();
        await Assert.That(error.ErrorChain.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ErrorChain_CanAppendErrors()
    {
        var error1 = new Error("Original error");
        var error2 = new Error("Error during handling");
        error1.ErrorChain.Add(error2);

        await Assert.That(error1.ErrorChain.Count).IsEqualTo(1);
        await Assert.That(error1.ErrorChain[0].Message).IsEqualTo("Error during handling");
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
    public async Task FromException_WithInnerException_WalksClrChain()
    {
        var inner = new InvalidOperationException("Inner");
        var outer = new Exception("Outer", inner);

        var error = Error.FromException(outer);

        await Assert.That(error.Message).IsEqualTo("Outer");
        await Assert.That(error.Exception).IsNotNull();
        await Assert.That(error.Exception!.InnerException).IsNotNull();
        await Assert.That(error.Exception!.InnerException!.Message).IsEqualTo("Inner");
    }

    [Test]
    public async Task FromException_WithNoInnerException_HasNullException()
    {
        var ex = new Exception("Single error");

        var error = Error.FromException(ex);

        await Assert.That(error.Exception!.InnerException).IsNull();
    }

    [Test]
    public async Task ToString_ReturnsFormattedString()
    {
        var error = new Error("Test error", "TestKey", 400);

        var str = error.ToString();

        await Assert.That(str).IsEqualTo("[TestKey] Test error");
    }

    [Test]
    public async Task Step_CanBeSet()
    {
        var step = new Step { Index = 0, Text = "do something" };
        var goal = new Goal { Name = "TestGoal" };
        step.Goal = goal;
        var error = new Error("Error", step);

        await Assert.That(error.Step).IsNotNull();
        await Assert.That(error.Step!.Goal!.Name).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task Format_IncludesKeyAndStatusCode()
    {
        var step = new Step { Index = 0, Text = "set name" };
        var error = new Error("Something went wrong", step, "TestKey", 500);

        var formatted = error.Format();

        await Assert.That(formatted).Contains("TestKey(500)");
        await Assert.That(formatted).Contains("Something went wrong");
    }

    [Test]
    public async Task Format_IncludesGoalAndStep()
    {
        var goal = new Goal { Name = "Start", Path = "Start.goal" };
        var step = new Step { Index = 2, Text = "write to file", LineNumber = 5 };
        step.Goal = goal;
        var error = new Error("File not found", step);

        var formatted = error.Format();

        await Assert.That(formatted).Contains("Start.goal:5");
        await Assert.That(formatted).Contains("write to file");
    }

    [Test]
    public async Task Format_IncludesErrorChain()
    {
        var step = new Step { Index = 0, Text = "do something" };
        var error1 = new Error("Original error", step);
        var error2 = new Error("Handler error", step, "HandlerError", 500);
        error1.ErrorChain.Add(error2);

        var formatted = error1.Format();

        await Assert.That(formatted).Contains("Error during error handling [1]");
        await Assert.That(formatted).Contains("HandlerError(500)");
    }

    [Test]
    public async Task Format_IncludesFixSuggestionAndLinks()
    {
        var step = new Step { Index = 0, Text = "connect db" };
        var error = new Error("Connection failed", step, "Error", 500)
        {
            FixSuggestion = "Check your connection string",
            HelpfulLinks = "https://docs.example.com/db"
        };

        var formatted = error.Format();

        await Assert.That(formatted).Contains("Fix Suggestions:");
        await Assert.That(formatted).Contains("Check your connection string");
        await Assert.That(formatted).Contains("Helpful Links:");
        await Assert.That(formatted).Contains("https://docs.example.com/db");
    }

    [Test]
    public async Task Format_IncludesException()
    {
        var step = new Step { Index = 0, Text = "call api" };
        var ex = new InvalidOperationException("Boom");
        var error = new Error("API call failed", step, "Error", 500) { Exception = ex };

        var formatted = error.Format();

        await Assert.That(formatted).Contains("InvalidOperationException: Boom");
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
    public async Task ActionModule_CanBeSet()
    {
        var error = new ActionError("Error") { ActionModule = "variable", ActionName = "set" };

        await Assert.That(error.ActionModule).IsEqualTo("variable");
        await Assert.That(error.ActionName).IsEqualTo("set");
    }

    [Test]
    public async Task FormatExtra_IncludesAction()
    {
        var step = new Step { Index = 1, Text = "set name to John" };
        var error = new ActionError("Missing param", step) { ActionModule = "variable", ActionName = "set" };

        var formatted = error.Format();

        await Assert.That(formatted).Contains("variable.set");
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
        await using var engine = new Engine("/app");
        using var context = new PLangContext(engine);
        var step = new Step { Text = "test step" };
        context.Step = step;

        var error = StepError.FromException(ex, context);

        await Assert.That(error).IsTypeOf<StepError>();
        await Assert.That(error.Message).IsEqualTo("Step crashed");
        await Assert.That(error.Step).IsEqualTo(step);
    }
}
