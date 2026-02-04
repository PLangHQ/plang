using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Runtime;

namespace PLang.Tests.Errors;

public class ErrorChainTests
{
	[Test]
	public async Task Add_SingleError_AddsToChain()
	{
		// Arrange
		var error1 = new Error("First error", "Error1", 400);
		var error2 = new Error("Second error", "Error2", 500);

		// Act
		error1.Add(error2);

		// Assert
		await Assert.That(error1.ErrorChain.Count).IsEqualTo(1);
		await Assert.That(error1.ErrorChain[0].Message).IsEqualTo("Second error");
	}

	[Test]
	public async Task Add_MultipleErrors_AddsAllToChain()
	{
		// Arrange
		var rootError = new Error("Root error", "RootError", 400);
		var error1 = new Error("First chained error", "Error1", 400);
		var error2 = new Error("Second chained error", "Error2", 500);
		var error3 = new StepError("Step error", null, "StepError", 400);

		// Act
		rootError.Add(error1).Add(error2).Add(error3);

		// Assert
		await Assert.That(rootError.ErrorChain.Count).IsEqualTo(3);
		await Assert.That(rootError.ErrorChain[0].Message).IsEqualTo("First chained error");
		await Assert.That(rootError.ErrorChain[1].Message).IsEqualTo("Second chained error");
		await Assert.That(rootError.ErrorChain[2].Message).IsEqualTo("Step error");
	}

	[Test]
	public async Task Add_DuplicateError_DoesNotAddTwice()
	{
		// Arrange
		var rootError = new Error("Root error", "RootError", 400);
		var duplicateError = new Error("Same error", "SameKey", 400);

		// Act
		rootError.Add(duplicateError);
		rootError.Add(duplicateError); // Add same instance again

		// Assert
		await Assert.That(rootError.ErrorChain.Count).IsEqualTo(1);
	}

	[Test]
	public async Task Add_ErrorWithSameKeyAndMessage_DoesNotAddTwice()
	{
		// Arrange
		var rootError = new Error("Root error", "RootError", 400);
		var error1 = new Error("Duplicate message", "DuplicateKey", 400);
		var error2 = new Error("Duplicate message", "DuplicateKey", 500); // Same key and message, different status

		// Act
		rootError.Add(error1);
		rootError.Add(error2);

		// Assert
		await Assert.That(rootError.ErrorChain.Count).IsEqualTo(1);
	}

	[Test]
	public async Task Add_NullError_ReturnsWithoutAdding()
	{
		// Arrange
		var rootError = new Error("Root error", "RootError", 400);

		// Act
		rootError.Add(null!);

		// Assert
		await Assert.That(rootError.ErrorChain.Count).IsEqualTo(0);
	}

	[Test]
	public async Task Add_SelfReference_DoesNotAdd()
	{
		// Arrange
		var error = new Error("Self error", "SelfError", 400);

		// Act
		error.Add(error);

		// Assert
		await Assert.That(error.ErrorChain.Count).IsEqualTo(0);
	}

	[Test]
	public async Task Add_ChainsReturnsSelf_AllowsFluentChaining()
	{
		// Arrange
		var rootError = new Error("Root error", "RootError", 400);
		var error1 = new Error("Error 1", "Error1", 400);
		var error2 = new Error("Error 2", "Error2", 400);

		// Act
		var result = rootError.Add(error1).Add(error2);

		// Assert
		await Assert.That(result).IsSameReferenceAs(rootError);
		await Assert.That(rootError.ErrorChain.Count).IsEqualTo(2);
	}

	[Test]
	public async Task Add_AggregatesVariablesFromChainedErrors()
	{
		// Arrange
		var rootError = new Error("Root error", "RootError", 400);
		rootError.Variables.Add(new ObjectValue("rootVar", "rootValue"));

		var error1 = new Error("Error 1", "Error1", 400);
		error1.Variables.Add(new ObjectValue("var1", "value1"));

		var error2 = new Error("Error 2", "Error2", 400);
		error2.Variables.Add(new ObjectValue("var2", "value2"));

		// Act
		rootError.Add(error1).Add(error2);

		// Assert
		await Assert.That(rootError.Variables.Count).IsEqualTo(3);
	}

	[Test]
	public async Task IsErrorHandled_EmptyChain_ReturnsFalse()
	{
		// Arrange
		var error = new Error("Error", "Error", 400);

		// Assert
		await Assert.That(error.IsErrorHandled).IsFalse();
	}

	[Test]
	public async Task IsErrorHandled_ChainWithHandledError_ReturnsTrue()
	{
		// Arrange
		var rootError = new Error("Root error", "RootError", 400);
		var handledError = new PLang.Errors.ErrorHandled(rootError);
		rootError.Add(handledError);

		// Assert
		await Assert.That(rootError.IsErrorHandled).IsTrue();
	}

	[Test]
	public async Task ToFormat_WithErrorChain_IncludesAllErrors()
	{
		// Arrange
		var rootError = new Error("Root error message", "RootError", 400);
		var error1 = new Error("First chained error", "Error1", 400);
		var error2 = new Error("Second chained error", "Error2", 500);

		rootError.Add(error1).Add(error2);

		// Act
		var formatted = rootError.ToFormat("text").ToString();

		// Assert
		await Assert.That(formatted).Contains("Root error message");
		await Assert.That(formatted).Contains("First chained error");
		await Assert.That(formatted).Contains("Second chained error");
	}

	[Test]
	public async Task ErrorChain_DirectListAdd_Works()
	{
		// Arrange
		var rootError = new Error("Root error", "RootError", 400);
		var error1 = new Error("Direct add error", "Error1", 400);

		// Act - using direct list access (the pattern used throughout the codebase)
		rootError.ErrorChain.Add(error1);

		// Assert
		await Assert.That(rootError.ErrorChain.Count).IsEqualTo(1);
		await Assert.That(rootError.ErrorChain[0].Message).IsEqualTo("Direct add error");
	}

	[Test]
	public async Task GetErrorMessageFromChain_ReturnsFormattedMessages()
	{
		// Arrange
		var rootError = new Error("Root error", "RootError", 400);
		var error1 = new Error("First error", "Error1", 400);
		var error2 = new Error("Second error", "Error2", 500);
		rootError.ErrorChain.Add(error1);
		rootError.ErrorChain.Add(error2);

		// Act
		var message = ErrorHelper.GetErrorMessageFromChain(rootError);

		// Assert
		await Assert.That(message).Contains("First error");
		await Assert.That(message).Contains("Second error");
	}
}
