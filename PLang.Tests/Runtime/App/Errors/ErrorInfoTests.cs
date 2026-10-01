using app.actor.context;
using app;
using app.error;

namespace PLang.Tests.App.Errors;

public class ErrorTests
{
    [Test]
    public async Task Constructor_WithMessage_SetsDefaults()
    {
        var error = new Error("Test error");

        await Assert.That(error.Message).IsEqualTo("Test error");
        await Assert.That(error.Key).IsEqualTo("Error");
        await Assert.That(error.Status.Code.ToInt32()).IsEqualTo(400);
        await Assert.That(error.Id).IsNotNull();
        await Assert.That(error.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_WithAllParameters_SetsValues()
    {
        var error = new Error("Not found", "NotFound", 404);

        await Assert.That(error.Message).IsEqualTo("Not found");
        await Assert.That(error.Key).IsEqualTo("NotFound");
        await Assert.That(error.Status.Code.ToInt32()).IsEqualTo(404);
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
    public async Task CausingList_IsEmptyByDefault()
    {
        var error = new Error("Error");

        await Assert.That(error.list).IsNotNull();
        await Assert.That(error.list.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CausingList_CanAppendErrors()
    {
        var error1 = new Error("Original error");
        var error2 = new Error("Error during handling");
        error1.list.Add(error2);

        await Assert.That(error1.list.Count).IsEqualTo(1);
        await Assert.That(error1.list[0].Message).IsEqualTo("Error during handling");
    }

    [Test]
    public async Task FromException_CreatesErrorFromException()
    {
        var ex = new InvalidOperationException("Something failed");

        var error = Error.FromException(ex);

        // an exception plang didn't raise: ServiceError (500), its message naming the exception's type
        await Assert.That(error.Message).IsEqualTo("InvalidOperationException: Something failed");
        await Assert.That(error.Key).IsEqualTo("ServiceError");
        await Assert.That(error.Status.Code.ToInt32()).IsEqualTo(500);
        await Assert.That(error.Exception).IsEqualTo(ex);
    }

    [Test]
    public async Task FromException_OfACarriedError_IsThatErrorWhole()
    {
        var carried = new Error("Bad argument", "ValidationError", 400);

        var error = Error.FromException(new global::app.error.AppException(carried));

        await Assert.That(error).IsSameReferenceAs(carried);
    }

    [Test]
    public async Task FromException_WithInnerException_WalksClrChain()
    {
        var inner = new InvalidOperationException("Inner");
        var outer = new Exception("Outer", inner);

        var error = Error.FromException(outer);

        await Assert.That(error.Message).IsEqualTo("Exception: Outer");
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
        var goal = new Goal { Name = "TestGoal" };
        var step = new Step { Goal = goal, Index = 0, Text = "do something" };
        var error = new Error("Error", step);

        await Assert.That(error.Step).IsNotNull();
        await Assert.That(error.Step!.Goal!.Name).IsEqualTo("TestGoal");
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
        await Assert.That(error.Status.Code.ToInt32()).IsEqualTo(404);
    }

    [Test]
    public async Task Cancelled_CreatesCancelledError()
    {
        var error = GoalError.Cancelled();

        await Assert.That(error.Message).IsEqualTo("Execution cancelled");
        await Assert.That(error.Key).IsEqualTo("Cancelled");
        await Assert.That(error.Status.Code.ToInt32()).IsEqualTo(499);
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
        await Assert.That(error.Status.Code.ToInt32()).IsEqualTo(404);
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
        await Assert.That(error.Status.Code.ToInt32()).IsEqualTo(500);
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
}
