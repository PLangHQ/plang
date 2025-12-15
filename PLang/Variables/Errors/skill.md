# PLang Variables Error System

Comprehensive error handling for the PLang Variables system. All errors implement the standard `IError` interface and provide detailed information for debugging and user feedback.

## Overview

The error system provides:
- Specific error types for different validation failures
- Descriptive error messages with context
- Fix suggestions for common issues
- Integration with Plang's error handling infrastructure
- Support for error chaining and tracking

## Error Base Class

### VariableMappingErrorBase

Abstract base class for all variable mapping errors.

**Properties:**
- `Id`: Unique identifier (GUID)
- `StatusCode`: HTTP-style status code (400 for validation errors)
- `Key`: Error type identifier
- `Message`: Human-readable error description
- `FixSuggestion`: Guidance on how to fix the error
- `HelpfulLinks`: Optional links to documentation
- `Step`: Associated GoalStep (if any)
- `Goal`: Associated Goal (if any)
- `CreatedUtc`: Timestamp of error creation
- `Exception`: Underlying exception (if any)
- `ErrorChain`: Chain of related errors
- `MessageOrDetail`: Returns the message
- `Handled`: Whether the error has been handled
- `Variables`: Context variables at time of error

**Methods:**
- `ToFormat(contentType)`: Format error for output (text/json)
- `AsData()`: Return error as data object

## Error Types

### 1. InvalidVariableError

**When it occurs:**
- Variable syntax is malformed
- Variable expression doesn't match expected format

**Key:** `InvalidVariable`

**Example:**
```csharp
new InvalidVariableError("Variable expression '%name' is missing closing %")
```

**Fix Suggestion:**
"Check variable syntax and ensure it's properly formatted"

**Usage:**
```csharp
if (!IsValidVariableSyntax(expression))
{
    return new InvalidVariableError($"Invalid syntax: {expression}");
}
```

### 2. ClassNotFoundError

**When it occurs:**
- Referenced class doesn't exist
- Class doesn't have `[Piped]` attribute
- Typo in class name

**Key:** `ClassNotFound`

**Example:**
```csharp
new ClassNotFoundError("Class 'StringHelper' not found in operation 0")
```

**Fix Suggestion:**
"Verify the class name and ensure it has the [Piped] attribute"

**Common Causes:**
- Class name typo: `"Strng"` instead of `"String"`
- Missing [Piped] attribute on custom class
- Class not loaded in current AppDomain

### 3. MethodNotFoundError

**When it occurs:**
- Method doesn't exist on specified class
- Method name is misspelled
- Method has different signature than expected

**Key:** `MethodNotFound`

**Example:**
```csharp
new MethodNotFoundError("Method 'ToUper' not found on class 'string' in operation 1")
```

**Fix Suggestion:**
"Check the method name and ensure it exists on the specified class"

**Common Causes:**
- Method name typo: `"ToUper"` instead of `"ToUpper"`
- Private/protected method (only public methods are accessible)
- Extension method not recognized

### 4. ParameterCountMismatchError

**When it occurs:**
- Wrong number of parameters provided
- Method signature doesn't match call

**Key:** `ParameterCountMismatch`

**Example:**
```csharp
new ParameterCountMismatchError("Method 'Split' expects 1 parameters but got 0 in operation 2")
```

**Fix Suggestion:**
"Verify the number of parameters matches the method signature"

**Common Scenarios:**
```plang
// Wrong: Missing parameter
%text | split%

// Right: Correct parameter count
%text | split(' ')%
```

### 5. ParameterTypeMismatchError

**When it occurs:**
- Parameter type cannot be converted to expected type
- Type incompatibility

**Key:** `ParameterTypeMismatch`

**Example:**
```csharp
new ParameterTypeMismatchError("Parameter 0 of method 'Split' expects type 'String' but got 'Int32' in operation 2")
```

**Fix Suggestion:**
"Ensure parameter types match the expected types"

**Common Scenarios:**
```plang
// Wrong: Passing number where string expected
%text | split(5)%

// Right: Correct type
%text | split(' ')%
```

### 6. InvalidReturnTypeError

**When it occurs:**
- Return type string cannot be resolved to actual type
- Type name is invalid or unknown

**Key:** `InvalidReturnType`

**Example:**
```csharp
new InvalidReturnTypeError("Return type 'Strng[]' is not valid in operation 3")
```

**Fix Suggestion:**
"Check that the return type is a valid type name"

**Common Causes:**
- Typo in type name
- Custom type not loaded
- Incorrect array syntax

### 7. VariableNotFoundError

**When it occurs:**
- Variable expression not found in original text
- Mismatch between LLM output and actual text

**Key:** `VariableNotFound`

**Example:**
```csharp
new VariableNotFoundError("Variable expression '%name%' not found in original text")
```

**Fix Suggestion:**
"Ensure the variable expression matches exactly what appears in the original text"

**Common Causes:**
- LLM hallucinated a variable
- Text was modified after parsing
- Case sensitivity issues

### 8. ParameterValidationError

**When it occurs:**
- Built-in operation parameter validation fails
- Special parameter requirements not met

**Key:** `ParameterValidation`

**Example:**
```csharp
new ParameterValidationError("Column operation requires exactly one string parameter in operation 0")
```

**Fix Suggestion:**
"Review the parameter requirements for this operation"

**Built-in Operation Requirements:**

| Operation | Required Parameters |
|-----------|-------------------|
| Column | Exactly 1 string |
| Index | Exactly 1 (any type) |
| Multiply | Exactly 1 (numeric or "N%") |
| Add | Exactly 1 (numeric) |
| Subtract | Exactly 1 (numeric) |
| Divide | Exactly 1 (numeric) |
| Increment | None |
| Decrement | None |

## Error Handling Patterns

### Pattern 1: Validation with Error Return
```csharp
var helper = new VariableMappingHelper();
var (mapping, error) = helper.ValidateMapping(llmMapping);

if (error != null)
{
    // Log error
    logger.LogError(error.Message);
    
    // Show user-friendly message
    Console.WriteLine(error.FixSuggestion);
    
    // Return or throw
    return error;
}

// Continue with validated mapping
```

### Pattern 2: Specific Error Handling
```csharp
var (mapping, error) = helper.ValidateMapping(llmMapping);

if (error != null)
{
    switch (error)
    {
        case ClassNotFoundError classError:
            // Suggest available classes
            SuggestSimilarClasses(classError);
            break;
            
        case MethodNotFoundError methodError:
            // Suggest available methods
            SuggestSimilarMethods(methodError);
            break;
            
        case ParameterCountMismatchError paramError:
            // Show method signature
            ShowMethodSignature(paramError);
            break;
            
        default:
            // Generic error handling
            LogError(error);
            break;
    }
}
```

### Pattern 3: Error Chaining
```csharp
try
{
    var (mapping, error) = helper.ValidateMapping(llmMapping);
    
    if (error != null)
    {
        // Create wrapper error with context
        var wrapperError = new BuildError("Failed to validate variables");
        wrapperError.ErrorChain.Add(error);
        return wrapperError;
    }
}
catch (Exception ex)
{
    var error = new InvalidVariableError("Unexpected error during validation");
    error.Exception = ex;
    return error;
}
```

## Integration with Plang Error System

All error classes integrate seamlessly with Plang's error infrastructure:

### Error Tracking
```csharp
error.Step = currentStep;
error.Goal = currentGoal;
error.Variables = GetContextVariables();
```

### Error Formatting
```csharp
// As text
var text = error.ToFormat("text");

// As JSON
var json = error.ToFormat("json");

// As data object
var data = error.AsData();
```

### Error Handling Workflow
```csharp
if (error != null && !error.Handled)
{
    // Log to system
    errorLogger.Log(error);
    
    // Mark as handled
    error.Handled = true;
    
    // Notify user
    NotifyUser(error.Message, error.FixSuggestion);
}
```

## Testing Error Scenarios

### Unit Test Example
```csharp
[Test]
public async Task InvalidClass_ReturnsClassNotFoundError()
{
    // Arrange
    var llmMapping = new VariableMapping
    {
        OriginalText = "Hello %name%",
        Variables = new List<LlmVariable>
        {
            new LlmVariable
            {
                FullExpression = "%name%",
                VariableName = "name",
                Operations = new List<Operation>
                {
                    new Operation
                    {
                        Class = "NonExistentClass",
                        Method = "Transform",
                        Parameters = new object[] { },
                        ReturnType = "string"
                    }
                }
            }
        }
    };
    
    var helper = new VariableMappingHelper();
    
    // Act
    var (result, error) = helper.ValidateMapping(llmMapping);
    
    // Assert
    await Assert.That(error).IsNotNull();
    await Assert.That(error).IsTypeOf<ClassNotFoundError>();
    await Assert.That(error.Message).Contains("NonExistentClass");
    await Assert.That(error.Key).IsEqualTo("ClassNotFound");
}
```

## Best Practices

### 1. Always Check for Errors
```csharp
// Good
var (mapping, error) = helper.ValidateMapping(llmMapping);
if (error != null) { /* handle */ }

// Bad - ignoring errors
var (mapping, _) = helper.ValidateMapping(llmMapping);
```

### 2. Provide Context
```csharp
// Good - includes context
new MethodNotFoundError($"Method '{method}' not found on class '{className}' in operation {index}")

// Bad - vague message
new MethodNotFoundError("Method not found")
```

### 3. Use Specific Error Types
```csharp
// Good - specific error type
return new ParameterCountMismatchError(message);

// Bad - generic error
return new InvalidVariableError(message);
```

### 4. Chain Related Errors
```csharp
var validationError = new InvalidVariableError("Failed validation");
validationError.ErrorChain.Add(underlyingError);
return validationError;
```

### 5. Include Fix Suggestions
```csharp
// Already included in constructor, but you can customize:
public class MyCustomError : VariableMappingErrorBase
{
    public MyCustomError(string method, List<string> availableMethods)
        : base(
            $"Method '{method}' not found",
            "CustomMethodNotFound",
            $"Did you mean one of: {string.Join(", ", availableMethods)}?"
        )
    {
    }
}
```

## Error Message Guidelines

### Good Error Messages
✅ "Method 'ToUper' not found on class 'string' in operation 1"
✅ "Parameter 0 of method 'Split' expects type 'String' but got 'Int32'"
✅ "Variable expression '%name%' not found in original text"

### Poor Error Messages
❌ "Invalid"
❌ "Error in operation"
❌ "Something went wrong"

### Message Components
1. **What went wrong**: Clear description of the problem
2. **Where it occurred**: Operation index, variable name, etc.
3. **What was expected**: Expected type, count, format
4. **What was received**: Actual value that caused the error

## Debugging Tips

### Enable Detailed Logging
```csharp
if (error != null)
{
    logger.LogError($"Error: {error.Key}");
    logger.LogError($"Message: {error.Message}");
    logger.LogError($"Fix: {error.FixSuggestion}");
    logger.LogError($"Context: {JsonSerializer.Serialize(error.Variables)}");
}
```

### Inspect Error Chain
```csharp
void PrintErrorChain(IError error, int depth = 0)
{
    var indent = new string(' ', depth * 2);
    Console.WriteLine($"{indent}{error.Message}");
    
    foreach (var chainedError in error.ErrorChain)
    {
        PrintErrorChain(chainedError, depth + 1);
    }
}
```

### Add Breakpoints at Error Creation
Set breakpoints in error constructors to catch errors at creation time:
```csharp
public ClassNotFoundError(string message) 
    : base(message, "ClassNotFound", "Verify the class name...")
{
    // Breakpoint here to inspect call stack
}
```

## Future Enhancements

Potential improvements to the error system:
- Error recovery suggestions with code fixes
- Similar name suggestions (fuzzy matching)
- Error statistics and reporting
- Localization support for error messages
- Interactive error resolution in IDE
- Error prevention with IntelliSense-style warnings