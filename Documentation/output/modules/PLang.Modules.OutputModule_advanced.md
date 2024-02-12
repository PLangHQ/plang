# Output

## Introduction
The Output module in the plang programming language serves as the interface for displaying information to the user and for interacting with the user through prompts. It is designed to handle various types of output, including plain text, warnings, errors, and debug information. Additionally, it can request input from the user and validate it against specific criteria.

The integration of plang with C# is achieved through a mapping process where natural language steps defined in plang are translated into C# method calls. This mapping is facilitated by a Language Learning Model (LLM) which interprets the steps and suggests the appropriate C# methods from the Output module.

## Plang code examples
For simple documentation and examples, refer to [PLang.Modules.OutputModule.md](./PLang.Modules.OutputModule.md). The repository containing comprehensive examples is available at [PLangHQ/plang Output Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Output).

### Example: Writing Out Text
This is a common usage where the program outputs a text message to the user.
```plang
- write out 'Hello, World!'
```
Default C# Signature:
```csharp
Task Write(object content, bool writeToBuffer = false, string type = "text", int statusCode = 200)
```

### Example: Asking for User Input
Here, the program prompts the user for input and stores the response in a variable.
```plang
- ask 'What is your favorite color?', cannot be empty, write to %favoriteColor%
```
Default C# Signature:
```csharp
Task<string> Ask(string text, string type = "text", int statusCode = 200, string regexPattern = null, string errorMessage = null)
```

For more detailed documentation and examples, visit [PLang.Modules.OutputModule.md](./PLang.Modules.OutputModule.md) and the [PLangHQ/plang Output Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Output). Additionally, inspect the Program.cs source code at [PLang.Modules.OutputModule Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.OutputModule/Program.cs).

## Source code
The runtime code for the Output module, Program.cs, can be found at [PLangHQ/plang OutputModule Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.OutputModule/Program.cs).

## How plang is mapped to C#
The mapping of plang to C# occurs through a two-step process involving the Builder and Runtime.

### Builder
When the user runs `plang build`, the .goal file is processed:
1. Each step (line starting with `-`) is parsed.
2. The StepBuilder sends a query to LLM along with a list of all available modules.
3. LLM suggests the appropriate module, such as PLang.Modules.OutputModule.
4. Builder.cs (or BaseBuilder.cs if Builder.cs is not available) sends the step and all methods in the Output module to LLM.
5. LLM returns a JSON mapping the step text to a C# method with the required parameters.
6. Builder.cs or BaseBuilder.cs creates a hash of the response and saves a JSON instruction file with the .pr extension in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime
The .pr file is executed by the plang runtime:
1. The .pr file is loaded.
2. Reflection is used to load the PLang.Modules.OutputModule.
3. The "Function" property in the .pr file specifies the C# method to call.
4. If required, parameters are passed to the method.

### plang example to csharp
Here's how a plang code example maps to a .pr file and the corresponding C# method:

#### plang code example:
```plang
- write out 'Goodbye, World!'
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
        "Value": "Goodbye, World!"
      },
      {
        "Type": "bool",
        "Name": "writeToBuffer",
        "Value": "false"
      },
      {
        "Type": "string",
        "Name": "type",
        "Value": "text"
      },
      {
        "Type": "int",
        "Name": "statusCode",
        "Value": "200"
      }
    ]
  }
}
```

## Created
This documentation was created on 2024-02-10T14:27:04.