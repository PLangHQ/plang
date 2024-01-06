
# Output Module Documentation

## Introduction
The Output module in the plang programming language serves as the bridge between the user interface and the program's internal logic. It allows for the display of information to the user and the collection of user input. Advanced users familiar with programming will appreciate the seamless integration of plang's natural language steps with the robust functionality of C# methods. This documentation will guide you through the most common usages of the Output module, providing plang code examples and their corresponding C# method signatures.

## Plang code examples
For simple documentation and examples, please refer to [PLang.Modules.OutputModule.md](./PLang.Modules.OutputModule.md). The repository for additional examples can be found at [PLangHQ/plang Output Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Output).

### Writing to the Console
The `Write` method in C# is used to output text or variables to the console. This is a common operation for displaying information to the user.

#### Plang Example:
```plang
Output
- write out 'Hello, World!'
```

#### C# Method Signature:
```csharp
Task Write(object? content = null, bool writeToBuffer = false, string type = "text", int statusCode = 200)
```

### Asking for User Input
The `Ask` method prompts the user for input and can be customized with a specific type and status code.

#### Plang Example:
```plang
Output
- ask 'What is your favorite color?', write to %userColor%
```

#### C# Method Signature:
```csharp
Task<string> Ask(string text, string type = "text", int statusCode = 200)
```

For more detailed documentation and all examples, please refer to [PLang.Modules.OutputModule.md](./PLang.Modules.OutputModule.md) and the [PLangHQ/plang Output Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Output). Additionally, you can look at the [Program.cs source code](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.OutputModule/Program.cs) for a deeper understanding of the Output module's implementation.

## Source code
The runtime code for the Output module, `Program.cs`, can be found at [PLangHQ/plang Output Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.OutputModule/Program.cs).

## How plang is mapped to C#
Modules in plang are utilized through a build and runtime process that translates natural language steps into executable C# code.

### Builder
When the user runs `plang build`, the .goal file is processed as follows:
1. Each step (line starting with `-`) is parsed.
2. A query is sent to the LLM for each step, along with a list of all available modules ([StepBuilder.cs](https://github.com/PLangHQ/plang/blob/main/PLang/Building/StepBuilder.cs)).
3. LLM suggests a module, such as `PLang.Modules.OutputModule`, and then receives all methods within the Output module along with the step.
4. Depending on availability, either `Builder.cs` or `BaseBuilder.cs` is used to generate a JSON instruction file with the `.pr` extension, stored in `.build/{GoalName}/01. {StepName}.pr`.

### Runtime
During runtime, the .pr file is executed as follows:
1. The .pr file is loaded by the plang runtime.
2. Reflection is used to load the `PLang.Modules.OutputModule`.
3. The "Function" property in the .pr file specifies the C# method to call.
4. If required, parameters are passed to the method.

### plang example to csharp
Here's how a plang code example is mapped to a method in the Output module and represented in a .pr file:

#### Plang Code Example:
```plang
Output
- write out 'Current time is %Now%', type 'info'
```

#### Example Instruction .pr file:
```json
{
  "Action": {
    "FunctionName": "Write",
    "Parameters": [
      {
        "Type": "string",
        "Name": "content",
        "Value": "Current time is %Now%"
      },
      {
        "Type": "string",
        "Name": "type",
        "Value": "info"
      }
    ]
  }
}
```

## Created
This documentation was created on 2024-01-02T22:12:30.
