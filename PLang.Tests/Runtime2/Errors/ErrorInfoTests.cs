using PLang.Runtime2.Errors;

namespace PLang.Tests.Runtime2.Errors;

public class ErrorInfoTests
{
    [Test]
    public async Task Constructor_WithMessage_SetsDefaults()
    {
        var error = new ErrorInfo("Test error");

        await Assert.That(error.Message).IsEqualTo("Test error");
        await Assert.That(error.Key).IsEqualTo("Error");
        await Assert.That(error.StatusCode).IsEqualTo(400);
        await Assert.That(error.Id).IsNotNull();
        await Assert.That(error.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_WithAllParameters_SetsValues()
    {
        var error = new ErrorInfo("Not found", "NotFound", 404);

        await Assert.That(error.Message).IsEqualTo("Not found");
        await Assert.That(error.Key).IsEqualTo("NotFound");
        await Assert.That(error.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Constructor_SetsCreatedUtc()
    {
        var before = DateTime.UtcNow;

        var error = new ErrorInfo("Test");

        var after = DateTime.UtcNow;
        await Assert.That(error.CreatedUtc).IsGreaterThanOrEqualTo(before);
        await Assert.That(error.CreatedUtc).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_GeneratesUniqueId()
    {
        var error1 = new ErrorInfo("Error 1");
        var error2 = new ErrorInfo("Error 2");

        await Assert.That(error1.Id).IsNotEqualTo(error2.Id);
    }

    [Test]
    public async Task FixSuggestion_CanBeSet()
    {
        var error = new ErrorInfo("Error") { FixSuggestion = "Try restarting" };

        await Assert.That(error.FixSuggestion).IsEqualTo("Try restarting");
    }

    [Test]
    public async Task HelpfulLinks_CanBeSet()
    {
        var error = new ErrorInfo("Error") { HelpfulLinks = "https://docs.example.com" };

        await Assert.That(error.HelpfulLinks).IsEqualTo("https://docs.example.com");
    }

    [Test]
    public async Task Exception_CanBeSet()
    {
        var ex = new InvalidOperationException("Test exception");
        var error = new ErrorInfo("Error") { Exception = ex };

        await Assert.That(error.Exception).IsEqualTo(ex);
    }

    [Test]
    public async Task InnerError_CanBeSet()
    {
        var inner = new ErrorInfo("Inner error");
        var error = new ErrorInfo("Outer error") { InnerError = inner };

        await Assert.That(error.InnerError).IsEqualTo(inner);
        await Assert.That(error.InnerError!.Message).IsEqualTo("Inner error");
    }

    [Test]
    public async Task FromException_CreatesErrorFromException()
    {
        var ex = new InvalidOperationException("Something failed");

        var error = ErrorInfo.FromException(ex);

        await Assert.That(error.Message).IsEqualTo("Something failed");
        await Assert.That(error.Key).IsEqualTo("Exception");
        await Assert.That(error.StatusCode).IsEqualTo(500);
        await Assert.That(error.Exception).IsEqualTo(ex);
    }

    [Test]
    public async Task FromException_WithCustomKeyAndStatusCode_UsesProvidedValues()
    {
        var ex = new ArgumentException("Bad argument");

        var error = ErrorInfo.FromException(ex, "ValidationError", 400);

        await Assert.That(error.Key).IsEqualTo("ValidationError");
        await Assert.That(error.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task FromException_WithInnerException_CreatesNestedError()
    {
        var inner = new InvalidOperationException("Inner");
        var outer = new Exception("Outer", inner);

        var error = ErrorInfo.FromException(outer);

        await Assert.That(error.Message).IsEqualTo("Outer");
        await Assert.That(error.InnerError).IsNotNull();
        await Assert.That(error.InnerError!.Message).IsEqualTo("Inner");
    }

    [Test]
    public async Task FromException_WithNoInnerException_HasNullInnerError()
    {
        var ex = new Exception("Single error");

        var error = ErrorInfo.FromException(ex);

        await Assert.That(error.InnerError).IsNull();
    }

    [Test]
    public async Task NotFound_CreatesNotFoundError()
    {
        var error = ErrorInfo.NotFound("User");

        await Assert.That(error.Message).IsEqualTo("User not found");
        await Assert.That(error.Key).IsEqualTo("NotFound");
        await Assert.That(error.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task NotFound_WithDifferentResource_FormatsMessage()
    {
        var error = ErrorInfo.NotFound("Goal 'Start'");

        await Assert.That(error.Message).IsEqualTo("Goal 'Start' not found");
    }

    [Test]
    public async Task InvalidInput_CreatesInvalidInputError()
    {
        var error = ErrorInfo.InvalidInput("Email is required");

        await Assert.That(error.Message).IsEqualTo("Email is required");
        await Assert.That(error.Key).IsEqualTo("InvalidInput");
        await Assert.That(error.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Unauthorized_CreatesUnauthorizedError_DefaultMessage()
    {
        var error = ErrorInfo.Unauthorized();

        await Assert.That(error.Message).IsEqualTo("Unauthorized");
        await Assert.That(error.Key).IsEqualTo("Unauthorized");
        await Assert.That(error.StatusCode).IsEqualTo(401);
    }

    [Test]
    public async Task Unauthorized_CreatesUnauthorizedError_CustomMessage()
    {
        var error = ErrorInfo.Unauthorized("Invalid token");

        await Assert.That(error.Message).IsEqualTo("Invalid token");
        await Assert.That(error.Key).IsEqualTo("Unauthorized");
        await Assert.That(error.StatusCode).IsEqualTo(401);
    }

    [Test]
    public async Task ToString_ReturnsFormattedString()
    {
        var error = new ErrorInfo("Test error", "TestKey", 400);

        var str = error.ToString();

        await Assert.That(str).IsEqualTo("[TestKey] Test error");
    }

    [Test]
    public async Task FromException_DeeplyNestedExceptions_CreatesChain()
    {
        var level3 = new Exception("Level 3");
        var level2 = new Exception("Level 2", level3);
        var level1 = new Exception("Level 1", level2);

        var error = ErrorInfo.FromException(level1);

        await Assert.That(error.Message).IsEqualTo("Level 1");
        await Assert.That(error.InnerError!.Message).IsEqualTo("Level 2");
        await Assert.That(error.InnerError!.InnerError!.Message).IsEqualTo("Level 3");
        await Assert.That(error.InnerError!.InnerError!.InnerError).IsNull();
    }
}
