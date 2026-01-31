using PLang.Building.Model;
using PLang.Errors;
using PLang.Utils;

namespace PLang.Tests.Utils;

public class StepHelperTests
{
    [Test]
    public async Task GetErrorHandlerForStep_WithNullErrorHandlers_ReturnsNull()
    {
        // Arrange
        List<ErrorHandler>? errorHandlers = null;
        var error = new Error("Test error", "TestKey");

        // Act
        var result = StepHelper.GetErrorHandlerForStep(errorHandlers, error);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetErrorHandlerForStep_WithNullError_ReturnsNull()
    {
        // Arrange
        var errorHandlers = new List<ErrorHandler> { new ErrorHandler() };
        IError? error = null;

        // Act
        var result = StepHelper.GetErrorHandlerForStep(errorHandlers, error);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetErrorHandlerForStep_WithCatchAllHandler_ReturnsThatHandler()
    {
        // Arrange
        var catchAllHandler = new ErrorHandler(); // No Message, Key, or StatusCode set
        var errorHandlers = new List<ErrorHandler> { catchAllHandler };
        var error = new Error("Some error message", "SomeKey");

        // Act
        var result = StepHelper.GetErrorHandlerForStep(errorHandlers, error);

        // Assert
        await Assert.That(result).IsEqualTo(catchAllHandler);
    }

    [Test]
    public async Task GetErrorHandlerForStep_WithMatchingMessage_ReturnsThatHandler()
    {
        // Arrange
        var messageHandler = new ErrorHandler { Message = "specific" };
        var errorHandlers = new List<ErrorHandler> { messageHandler };
        var error = new Error("This has a specific message in it", "SomeKey");

        // Act
        var result = StepHelper.GetErrorHandlerForStep(errorHandlers, error);

        // Assert
        await Assert.That(result).IsEqualTo(messageHandler);
    }

    [Test]
    public async Task GetErrorHandlerForStep_WithMessageNotMatching_ReturnsNull()
    {
        // Arrange
        var messageHandler = new ErrorHandler { Message = "specific" };
        var errorHandlers = new List<ErrorHandler> { messageHandler };
        var error = new Error("This has a different message", "SomeKey");

        // Act
        var result = StepHelper.GetErrorHandlerForStep(errorHandlers, error);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetErrorHandlerForStep_WithMatchingKey_ReturnsThatHandler()
    {
        // Arrange
        var keyHandler = new ErrorHandler { Key = "SpecificKey" };
        var errorHandlers = new List<ErrorHandler> { keyHandler };
        var error = new Error("Some message", "SpecificKey");

        // Act
        var result = StepHelper.GetErrorHandlerForStep(errorHandlers, error);

        // Assert
        await Assert.That(result).IsEqualTo(keyHandler);
    }

    [Test]
    public async Task GetErrorHandlerForStep_WithWildcardKey_ReturnsThatHandler()
    {
        // Arrange
        var wildcardHandler = new ErrorHandler { Key = "*" };
        var errorHandlers = new List<ErrorHandler> { wildcardHandler };
        var error = new Error("Some message", "AnyKey");

        // Act
        var result = StepHelper.GetErrorHandlerForStep(errorHandlers, error);

        // Assert
        await Assert.That(result).IsEqualTo(wildcardHandler);
    }

    [Test]
    public async Task GetErrorHandlerForStep_WithMatchingStatusCode_ReturnsThatHandler()
    {
        // Arrange
        var statusCodeHandler = new ErrorHandler { StatusCode = 404 };
        var errorHandlers = new List<ErrorHandler> { statusCodeHandler };
        var error = new Error("Not found", "NotFoundKey", StatusCode: 404);

        // Act
        var result = StepHelper.GetErrorHandlerForStep(errorHandlers, error);

        // Assert
        await Assert.That(result).IsEqualTo(statusCodeHandler);
    }

    [Test]
    public async Task GetErrorHandlerForStep_WithMultipleHandlers_ReturnsFirstMatch()
    {
        // Arrange
        var firstHandler = new ErrorHandler { Message = "error" };
        var secondHandler = new ErrorHandler { Key = "*" };
        var catchAllHandler = new ErrorHandler();
        var errorHandlers = new List<ErrorHandler> { firstHandler, secondHandler, catchAllHandler };
        var error = new Error("This is an error message", "SomeKey");

        // Act
        var result = StepHelper.GetErrorHandlerForStep(errorHandlers, error);

        // Assert
        await Assert.That(result).IsEqualTo(firstHandler);
    }
}
