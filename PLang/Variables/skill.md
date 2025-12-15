# PLang Variables System

This skill provides comprehensive variable parsing, validation, and execution capabilities for the Plang programming language. It handles variable expressions with property access, array indexing, pipe operations, and arithmetic transformations.

## Overview

The PLang Variables system consists of two main phases:

1. **LLM Parsing Phase**: Parse variable expressions from Plang code into structured JSON
2. **Runtime Validation & Execution Phase**: Validate the parsed structure and execute operations

## Core Components

### Data Models

#### LLM Phase Models (Input from LLM)
- **VariableMapping**: Container for parsed variables from original text
- **LlmVariable**: Individual variable with its expression and operations
- **Operation**: Single operation to perform (method call, property access, etc.)

#### Runtime Phase Models (Validated & Executable)
- **RuntimeVariableMapping**: Validated mapping with calculated positions
- **RuntimeVariable**: Variable with start/end positions and validated operations
- **RuntimeOperation**: Operation with cached MethodInfo for performance
- **PipelineResult**: Collection of operations to execute in sequence

### Core Classes

#### VariableMappingHelper
Main validation and conversion class that:
- Loads available piped classes using reflection
- Validates LLM-generated mappings
- Converts LlmVariable to RuntimeVariable
- Validates class names, method names, and parameters
- Calculates start/end positions in original text
- Caches MethodInfo for performance

#### PipedClassDiscovery
Discovers classes marked with `[Piped]` attribute:
- Scans all loaded assemblies
- Finds types with PipedAttribute
- Returns list of available piped classes

#### PipedAttribute
Marks classes that can be used in pipe operations:
```csharp
[Piped]
public class MyCustomClass
{
    public string Transform(string input) { ... }
}
```

## Variable Expression Syntax

### Simple Variables
```plang
%name%
%price%
%count%
```

### Property Access
```plang
%user.name%
%product.title%
%order.customer.email%
```

### Array Indexing
```plang
%items[0]%
%matrix[2][3]%
%products[0].name%
```

### Pipe Operations
```plang
%name | to upper%
%address | make it lower%
%text | trim%
```

### Arithmetic Operations
```plang
%price * 5%%        // Multiply by 5% (0.05)
%count + 10%        // Add 10
%total - 5%%        // Subtract 5%
%amount / 2%        // Divide by 2
%i++%               // Increment
%i--%               // Decrement
```

### Complex Chaining
```plang
%user.firstName | to upper | split(' ')[0]%
%products[0].price * 5%%
%address.city | to lower | trim%
```

## Built-in Operations

The system provides these built-in operations:

### Property & Array Access
- **Column**: Access object property by name
- **Index**: Access array element by index

### String Operations
- **ToUpper**: Convert to uppercase
- **ToLower**: Convert to lowercase
- **Split**: Split string into array
- **Trim**: Remove whitespace

### Arithmetic Operations
- **Multiply**: Multiply by number or percentage
- **Add**: Add number
- **Subtract**: Subtract number
- **Divide**: Divide by number
- **Increment**: Add 1
- **Decrement**: Subtract 1

## Usage Examples

### Example 1: Parsing Simple Variables

**Input Text:**
```
"Hello %name% - %address%"
```

**LLM Returns:**
```json
{
  "originalText": "Hello %name% - %address%",
  "variables": [
    {
      "fullExpression": "%name%",
      "variableName": "name",
      "operations": []
    },
    {
      "fullExpression": "%address%",
      "variableName": "address",
      "operations": []
    }
  ]
}
```

**C# Validation:**
```csharp
var helper = new VariableMappingHelper();
var (runtimeMapping, error) = helper.ValidateMapping(llmMapping);

if (error != null)
{
    // Handle error
}

// runtimeMapping now has Start and End positions calculated
// runtimeMapping.Variables[0].Start = 6
// runtimeMapping.Variables[0].End = 12
```

### Example 2: Property Access with Operations

**Input Text:**
```
"User: %user.name | to upper%"
```

**LLM Returns:**
```json
{
  "originalText": "User: %user.name | to upper%",
  "variables": [
    {
      "fullExpression": "%user.name | to upper%",
      "variableName": "user",
      "operations": [
        {
          "class": "object",
          "method": "Column",
          "parameters": ["name"],
          "returnType": "string"
        },
        {
          "class": "string",
          "method": "ToUpper",
          "parameters": [],
          "returnType": "string"
        }
      ]
    }
  ]
}
```

**C# Validation & Execution:**
```csharp
var helper = new VariableMappingHelper();
var (runtimeMapping, error) = helper.ValidateMapping(llmMapping);

// RuntimeVariable now has:
// - Start/End positions
// - Operations with MethodInfo cached
// - Validated parameter types
```

### Example 3: Complex Chaining

**Input Text:**
```
"Name: %user.firstName | to upper | split(' ')[0]%"
```

**LLM Returns:**
```json
{
  "originalText": "Name: %user.firstName | to upper | split(' ')[0]%",
  "variables": [
    {
      "fullExpression": "%user.firstName | to upper | split(' ')[0]%",
      "variableName": "user",
      "operations": [
        {
          "class": "object",
          "method": "Column",
          "parameters": ["firstName"],
          "returnType": "string"
        },
        {
          "class": "string",
          "method": "ToUpper",
          "parameters": [],
          "returnType": "string"
        },
        {
          "class": "string",
          "method": "Split",
          "parameters": [" "],
          "returnType": "string[]"
        },
        {
          "class": "object",
          "method": "Index",
          "parameters": [0],
          "returnType": "string"
        }
      ]
    }
  ]
}
```

### Example 4: Percentage Arithmetic

**Input Text:**
```
"Tax: %price * 5%%"
```

**LLM Returns:**
```json
{
  "originalText": "Tax: %price * 5%%",
  "variables": [
    {
      "fullExpression": "%price * 5%%",
      "variableName": "price",
      "operations": [
        {
          "class": "decimal",
          "method": "Multiply",
          "parameters": ["5%"],
          "returnType": "decimal"
        }
      ]
    }
  ]
}
```

**Note:** The "5%" is preserved as a string and will be parsed at execution time as 0.05.

## Validation Process

The `VariableMappingHelper.ValidateMapping()` method performs these validations:

1. **Position Calculation**: Finds each variable expression in original text
2. **Class Validation**: Ensures the class exists and has [Piped] attribute
3. **Method Validation**: Verifies method exists on the class
4. **Parameter Count Validation**: Checks parameter count matches method signature
5. **Parameter Type Validation**: Ensures parameters can be converted to expected types
6. **Return Type Validation**: Validates return type is a known type
7. **Operation Chaining**: Ensures output type of one operation matches input of next

## Error Handling

All validation errors implement `IError` interface and provide:
- Descriptive error messages
- Fix suggestions
- Error categorization
- Error chaining support

See PLang.Variables.Errors skill for detailed error types.

## Performance Considerations

### Caching
- **MethodInfo Caching**: Methods are resolved once during validation and cached
- **Type Dictionary**: Available types are cached in a dictionary for O(1) lookup
- **Position Calculation**: Done once during validation, not at execution time

### Efficiency
- Uses `StringBuilder` for template execution
- Reflection is minimized to validation phase only
- Runtime execution uses cached MethodInfo

## Extending the System

### Adding Custom Piped Classes
```csharp
[Piped]
public class MyCustomTransforms
{
    public string Reverse(string input)
    {
        return new string(input.Reverse().ToArray());
    }
    
    public string Encrypt(string input, string key)
    {
        // Your encryption logic
        return encrypted;
    }
}
```

Usage in Plang:
```plang
%text | Reverse%
%password | Encrypt('mykey')%
```

### Adding Built-in Operations

To add new built-in operations, modify `VariableMappingHelper`:

1. Add method name to `IsBuiltInOperation()`
2. Add validation logic in `ValidateBuiltInOperation()`
3. Implement execution logic in your executor class

## Integration with Plang Runtime

The Variables system integrates with:
- **Build Phase**: LLM parses variables during goal compilation
- **Runtime Phase**: Validated mappings are executed with actual variable values
- **Error System**: All errors integrate with Plang's error handling
- **Type System**: Works with Plang's type inference and conversion

## Best Practices

1. **Always validate** LLM output before execution
2. **Cache** RuntimeVariableMapping for repeated use
3. **Handle errors** gracefully with specific error types
4. **Use built-in operations** when possible for performance
5. **Test complex expressions** thoroughly with unit tests
6. **Document custom piped classes** for LLM understanding

## System Prompt Integration

The system includes a comprehensive system prompt that guides the LLM in parsing variables correctly. Key elements:

- Clear syntax examples for all variable types
- Detailed JSON structure specification
- Rules for operation ordering
- Examples of complex expressions
- Guidance on parameter preservation

## Future Enhancements

Potential areas for expansion:
- Conditional operators (ternary, if/else)
- Lambda expressions
- LINQ-style operations
- Custom operator precedence
- Type inference improvements
- Performance profiling tools