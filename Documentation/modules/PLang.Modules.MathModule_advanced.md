# Math Module Documentation

## Introduction 
The Math module in `plang` provides a comprehensive set of functions for performing various mathematical operations, including basic arithmetic, powers, roots, and more complex calculations. This module is designed to facilitate mathematical computations in a natural language format, allowing users to express their intentions clearly and concisely.

`plang` integrates seamlessly with C# methods, enabling users to leverage the power of C# libraries while maintaining the simplicity of natural language. Each step in a `plang` goal file is translated into a corresponding C# method call, allowing for efficient execution of mathematical operations.

## Plang code examples
- Simple documentation and examples can be found at [./PLang.Modules.MathModule.md](./PLang.Modules.MathModule.md).
- The repository for examples can be found at [https://github.com/PLangHQ/plang/tree/main/Tests/Math](https://github.com/PLangHQ/plang/tree/main/Tests/Math).

### Example 1: Basic Arithmetic Addition
This example demonstrates how to perform a simple addition operation using the Math module.

```plang
- what is 5 plus 7, write to %sum%
- write out %sum%
```

**C# Method Signature:**
```csharp
public (double?, IError?) Add(double a, double b);
```

### Example 2: Square Root Calculation
This example shows how to calculate the square root of a number.

```plang
- solve for sqrt(16), write to %result%
- write out %result%
```

**C# Method Signature:**
```csharp
public (double?, IError?) Sqrt(double value);
```

### Example 3: Fibonacci Sequence
This example retrieves the 10th Fibonacci number.

```plang
- find the 10th fibonacci number, write to %fib%
- write out %fib%
```

**C# Method Signature:**
```csharp
public (int?, IError?) Fibonacci(int n);
```

Not all methods are demonstrated, for more detail:
- Simple documentation and all examples can be found at [./PLang.Modules.MathModule.md](./PLang.Modules.MathModule.md).
- The repository for examples can be found at [https://github.com/PLangHQ/plang/tree/main/Tests/Math](https://github.com/PLangHQ/plang/tree/main/Tests/Math).
- For additional insights, refer to the source code in [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.MathModule/Program.cs).

## Source code
Program.cs is the runtime code, which can be found at [https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.MathModule/Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.MathModule/Program.cs).

## How plang is mapped to C#
This section explains how modules are utilized in `plang`.

### Builder 
When the user runs `plang build`, the .goal file is read:
1. Each step in the goal file (lines starting with -) is parsed.
2. For each step, a question is sent to the LLM (see [StepBuilder.cs](https://github.com/PLangHQ/plang/blob/main/PLang/Building/StepBuilder.cs)).
3. The StepBuilder sends a list of all available modules to the LLM.
4. The LLM returns a suggestion of the module to use, in this case, `PLang.Modules.MathModule`.
5. The builder sends all the methods in the Math module to the LLM along with the step.
6. This is done using either `Builder.cs` (see source code) or `BaseBuilder.cs` (see [BaseBuilder.cs](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/BaseBuilder.cs)), depending on availability.
7. The LLM returns a JSON response that maps the step text to a C# method and the required parameters.
8. The Builder.cs or BaseBuilder.cs (depending on availability) creates a hash of the response to store with the instruction file and saves a JSON instruction file with the .pr extension at the location `.build/{GoalName}/01. {StepName}.pr`.

### Runtime
The .pr file is then used by the `plang` runtime to execute the step:
1. The `plang` runtime loads the .pr file.
2. It uses reflection to load the `PLang.Modules.MathModule`.
3. The .pr file will contain a "Function" property.
4. The Function property indicates which C# method to call.
5. Parameters may be provided if the method requires them.

### plang example to C#
Here is a plang code example that maps to a method in the Math module.

```plang
- what is 5 plus 7, write to %sum%
```

**Mapping to C# Method:**
- This plang code maps to the `Add` method in the Math module.

**Example Instruction .pr file:**
```json
{
  "Action": {
    "FunctionName": "Add",
    "Parameters": [
      {
        "Type": "double",
        "Name": "a",
        "Value": "5"
      },
      {
        "Type": "double",
        "Name": "b",
        "Value": "7"
      }
    ],
    "ReturnValue": {
      "Type": "double",
      "VariableName": "sum"
    }
  }
}
```

## Created
This documentation is created on 2025-07-05T16:32:45.