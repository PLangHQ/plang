using PLang.Runtime2.Errors;

namespace PLang.Tests.Runtime2.Errors;

public class ErrorCategoryTests
{
    [Test]
    public async Task Error_4xx_IsApplication()
    {
        var error = new Error("bad request", "BadRequest", 400);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Application);
    }

    [Test]
    public async Task Error_404_IsApplication()
    {
        var error = new Error("not found", "NotFound", 404);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Application);
    }

    [Test]
    public async Task Error_5xx_IsRuntime()
    {
        var error = new Error("internal error", "InternalError", 500);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Runtime);
    }

    [Test]
    public async Task Error_502_IsRuntime()
    {
        var error = new Error("bad gateway", "BadGateway", 502);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Runtime);
    }

    [Test]
    public async Task ValidationError_AlwaysApplication()
    {
        var error = new ValidationError("invalid input", "Validation", 500);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Application);
    }

    [Test]
    public async Task AssertionError_AlwaysApplication()
    {
        var error = new AssertionError("assertion failed", "AssertFailed", 500);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Application);
    }

    [Test]
    public async Task StepError_AlwaysRuntime()
    {
        var error = new StepError("step failed", "StepError", 400);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Runtime);
    }

    [Test]
    public async Task GoalError_AlwaysRuntime()
    {
        var error = new GoalError("goal failed", "GoalError", 400);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Runtime);
    }

    [Test]
    public async Task ServiceError_AlwaysRuntime()
    {
        var error = new ServiceError("service failed", "ServiceError", 400);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Runtime);
    }

    [Test]
    public async Task ActionError_InheritsBaseLogic_4xx_IsApplication()
    {
        var error = new ActionError("not found", "NotFound", 404);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Application);
    }

    [Test]
    public async Task ActionError_InheritsBaseLogic_5xx_IsRuntime()
    {
        var error = new ActionError("crash", "Crash", 500);
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Runtime);
    }

    [Test]
    public async Task ApplicationError_FormatIsConcise()
    {
        var error = new ValidationError("Email is required");
        var formatted = error.Format();

        await Assert.That(formatted).Contains("Error: Email is required");
        await Assert.That(formatted).DoesNotContain("================");
        await Assert.That(formatted).DoesNotContain("Call stack");
    }

    [Test]
    public async Task RuntimeError_FormatHasFullDetail()
    {
        var error = new Error("Something crashed", "InternalError", 500);
        var formatted = error.Format();

        await Assert.That(formatted).Contains("Something crashed");
        await Assert.That(formatted).Contains("==================");
    }
}
