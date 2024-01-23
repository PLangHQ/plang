# Terminal

## Introduction

The Terminal module in plang is a powerful interface that allows users to execute and interact with system commands and processes directly from plang scripts. This module is particularly useful for automating tasks that involve command-line tools or scripts. Advanced users with programming experience will appreciate the seamless integration between plang's natural language steps and the underlying C# methods that execute the desired terminal commands.

In plang, each step described in natural language is mapped to a corresponding C# method, leveraging the capabilities of Large Language Models (LLMs) to interpret and translate the user's intent into executable code. This documentation will guide you through the process of using the Terminal module within plang and how it corresponds to C# methods.

## Plang code examples

For simple documentation and examples, please refer to [PLang.Modules.TerminalModule.md](./PLang.Modules.TerminalModule.md). The repository containing a comprehensive set of examples can be found at [PLangHQ/plang - Terminal Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Terminal).

### Running a Terminal Command

A common use case is to run a terminal command and capture its output. Below is a plang example followed by the default C# method signature.

```plang
Terminal
- run 'ls' with parameters '-la', write to %fileList%
```

C# Method Signature:
```csharp
Dictionary<string, object> RunTerminal(string appExecutableName, List<string> parameters = null, string pathToWorkingDirInTerminal = null, string dataOutputVariable = null, string errorDebugInfoOutputVariable = null, string dataStreamDelta = null, string errorStreamDelta = null)
```

### Reading User Input

Another frequent operation is reading user input from the terminal.

```plang
Terminal
- read 'Please enter your command: ', write to %userCommand%
```

C# Method Signature:
```csharp
void Read(string variableName = null)
```

For more detailed documentation and additional examples, please refer to [PLang.Modules.TerminalModule.md](./PLang.Modules.TerminalModule.md) and the [PLangHQ/plang - Terminal Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Terminal). To understand the full implementation, inspect the Program.cs source code at [PLang.Modules.TerminalModule - Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/TerminalModule/Program.cs).

## Source code

The runtime code for the Terminal module is available at [PLang.Modules.TerminalModule - Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/TerminalModule/Program.cs). The Builder.cs, which is responsible for the construction of steps, can be found at [PLang.Modules.TerminalModule - Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/TerminalModule/Builder.cs).

## How plang is mapped to C#

### Builder

When a user runs `plang build`, the .goal file is processed as follows:

1. Each step in the goal file (line starts with `-`) is parsed.
2. For each step, a query is sent to LLM, along with a list of all available modules.
3. The LLM suggests a module to use, in this case, `PLang.Modules.TerminalModule`.
4. Builder.cs (or BaseBuilder.cs if Builder.cs is not available) sends the step and all methods in the Terminal module to LLM.
5. LLM returns a JSON mapping the step text to a C# method with the required parameters.
6. Builder.cs or BaseBuilder.cs:
   - Creates a hash of the response to store with the instruction file.
   - Saves a JSON instruction file with the .pr extension at location `.build/{GoalName}/01. {StepName}.pr`.

### Runtime

The .pr file is then used by the plang runtime to execute the step:

1. Plang runtime loads the .pr file.
2. Plang runtime uses reflection to load `PLang.Modules.TerminalModule`.
3. The .pr file contains a "Function" property, which specifies the C# method to call.
4. Parameters are provided if the method requires them.

### plang example to csharp

Here's how a plang code example maps to a .pr file:

```plang
Terminal
- run 'ls' with parameters '-la', write to %fileList%
```

This plang step would map to a .pr file like this:

```json
{
  "Action": {
    "FunctionName": "RunTerminal",
    "Parameters": [
      {
        "Type": "string",
        "Name": "appExecutableName",
        "Value": "ls"
      },
      {
        "Type": "List<string>",
        "Name": "parameters",
        "Value": ["-la"]
      }
    ],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "fileList"
    }
  }
}
```

## Created

This documentation was created on 2024-01-02T22:31:56.