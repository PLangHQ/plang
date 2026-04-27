using global::App.Errors;

namespace PLang.Tests.App.Errors;

public class ExceptionsTests
{
    [Test]
    public async Task AppException_WithMessage_SetsProperties()
    {
        var ex = new AppException("Test error");

        await Assert.That(ex.Message).IsEqualTo("Test error");
        await Assert.That(ex.Key).IsEqualTo("AppError");
        await Assert.That(ex.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task AppException_WithAllParameters_SetsProperties()
    {
        var ex = new AppException("Custom error", "CustomKey", 400);

        await Assert.That(ex.Message).IsEqualTo("Custom error");
        await Assert.That(ex.Key).IsEqualTo("CustomKey");
        await Assert.That(ex.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task AppException_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("Inner error");

        var ex = new AppException("Outer error", inner);

        await Assert.That(ex.Message).IsEqualTo("Outer error");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }

    [Test]
    public async Task AppException_WithInnerAndCustomKeyAndStatus_SetsAll()
    {
        var inner = new Exception("Inner");

        var ex = new AppException("Outer", inner, "CustomKey", 503);

        await Assert.That(ex.Message).IsEqualTo("Outer");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
        await Assert.That(ex.Key).IsEqualTo("CustomKey");
        await Assert.That(ex.StatusCode).IsEqualTo(503);
    }

    [Test]
    public async Task GoalNotFoundException_SetsProperties()
    {
        var ex = new GoalNotFoundException("StartGoal");

        await Assert.That(ex.GoalName).IsEqualTo("StartGoal");
        await Assert.That(ex.Message).IsEqualTo("Goal 'StartGoal' not found");
        await Assert.That(ex.Key).IsEqualTo("GoalNotFound");
        await Assert.That(ex.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task GoalNotFoundException_IsAppException()
    {
        var ex = new GoalNotFoundException("Test");

        await Assert.That(ex is AppException).IsTrue();
    }

    [Test]
    public async Task StepExecutionException_WithMessage_SetsProperties()
    {
        var ex = new StepExecutionException("Step failed", 5);

        await Assert.That(ex.Message).IsEqualTo("Step failed");
        await Assert.That(ex.StepIndex).IsEqualTo(5);
        await Assert.That(ex.Key).IsEqualTo("StepExecutionFailed");
        await Assert.That(ex.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task StepExecutionException_WithInnerException_SetsInnerException()
    {
        var inner = new Exception("Inner error");

        var ex = new StepExecutionException("Step failed", 3, inner);

        await Assert.That(ex.Message).IsEqualTo("Step failed");
        await Assert.That(ex.StepIndex).IsEqualTo(3);
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }

    [Test]
    public async Task StepExecutionException_IsAppException()
    {
        var ex = new StepExecutionException("Test", 0);

        await Assert.That(ex is AppException).IsTrue();
    }

    [Test]
    public async Task ModuleNotFoundException_SetsProperties()
    {
        var ex = new ModuleNotFoundException("HttpModule");

        await Assert.That(ex.ModuleName).IsEqualTo("HttpModule");
        await Assert.That(ex.Message).IsEqualTo("Module 'HttpModule' not found");
        await Assert.That(ex.Key).IsEqualTo("ModuleNotFound");
        await Assert.That(ex.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task ModuleNotFoundException_IsAppException()
    {
        var ex = new ModuleNotFoundException("Test");

        await Assert.That(ex is AppException).IsTrue();
    }

    [Test]
    public async Task VariableNotFoundException_SetsProperties()
    {
        var ex = new VariableNotFoundException("userName");

        await Assert.That(ex.VariableName).IsEqualTo("userName");
        await Assert.That(ex.Message).IsEqualTo("Variable 'userName' not found");
        await Assert.That(ex.Key).IsEqualTo("VariableNotFound");
        await Assert.That(ex.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task VariableNotFoundException_IsAppException()
    {
        var ex = new VariableNotFoundException("Test");

        await Assert.That(ex is AppException).IsTrue();
    }

    [Test]
    public async Task CallStackOverflowException_SetsProperties()
    {
        var ex = new CallStackOverflowException(1000);

        await Assert.That(ex.MaxDepth).IsEqualTo(1000);
        await Assert.That(ex.Message).IsEqualTo("Call stack overflow: exceeded 1000 frames");
        await Assert.That(ex.Key).IsEqualTo("CallStackOverflow");
        await Assert.That(ex.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task CallStackOverflowException_IsAppException()
    {
        var ex = new CallStackOverflowException(100);

        await Assert.That(ex is AppException).IsTrue();
    }

    [Test]
    public async Task SerializationException_WithMessage_SetsProperties()
    {
        var ex = new SerializationException("Failed to serialize");

        await Assert.That(ex.Message).IsEqualTo("Failed to serialize");
        await Assert.That(ex.TargetType).IsNull();
        await Assert.That(ex.Key).IsEqualTo("SerializationFailed");
        await Assert.That(ex.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task SerializationException_WithTargetType_SetsTargetType()
    {
        var ex = new SerializationException("Failed to deserialize", typeof(DateTime));

        await Assert.That(ex.TargetType).IsEqualTo(typeof(DateTime));
    }

    [Test]
    public async Task SerializationException_WithInnerException_SetsInnerException()
    {
        var inner = new FormatException("Invalid format");

        var ex = new SerializationException("Serialization failed", inner, typeof(int));

        await Assert.That(ex.Message).IsEqualTo("Serialization failed");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
        await Assert.That(ex.TargetType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task SerializationException_IsAppException()
    {
        var ex = new SerializationException("Test");

        await Assert.That(ex is AppException).IsTrue();
    }

    [Test]
    public async Task AllExceptions_AreExceptions()
    {
        var exceptions = new Exception[]
        {
            new AppException("Test"),
            new GoalNotFoundException("Test"),
            new StepExecutionException("Test", 0),
            new ModuleNotFoundException("Test"),
            new VariableNotFoundException("Test"),
            new CallStackOverflowException(100),
            new SerializationException("Test")
        };

        foreach (var ex in exceptions)
        {
            await Assert.That(ex is Exception).IsTrue();
        }
    }

    [Test]
    public async Task AllExceptions_CanBeCaughtAsAppException()
    {
        var exceptions = new AppException[]
        {
            new GoalNotFoundException("Test"),
            new StepExecutionException("Test", 0),
            new ModuleNotFoundException("Test"),
            new VariableNotFoundException("Test"),
            new CallStackOverflowException(100),
            new SerializationException("Test")
        };

        foreach (var ex in exceptions)
        {
            try
            {
                throw ex;
            }
            catch (AppException caught)
            {
                await Assert.That(caught).IsNotNull();
            }
        }
    }
}
