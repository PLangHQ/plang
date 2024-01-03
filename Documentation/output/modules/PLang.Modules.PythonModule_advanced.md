# Python Module in plang

## Introduction

The Python module in plang is designed to provide a seamless interface for executing Python scripts from within the plang environment. This module is particularly useful for users who wish to leverage the power of Python's extensive libraries and capabilities alongside plang's natural language processing features. Advanced users will appreciate the flexibility and ease with which plang maps natural language steps to C# methods, enabling complex scripting and automation tasks to be described in a more intuitive way.

## Plang code examples

For simple documentation and examples, please refer to [PLang.Modules.PythonModule.md](./PLang.Modules.PythonModule.md). The repository for additional examples can be found at [plang Python Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Python).

### Running a Python Script Without Parameters

This is a common use case where a user wants to execute a Python script without any additional parameters.

```plang
Python
- call main.py, write to %output%
- write out 'Script output: %output%'
```

**C# Method Signature:**
```csharp
Task<Dictionary<string, object>> RunPythonScript(string fileName, string[] parameterValues = null, string[] parameterNames = null, string[] variablesToExtractFromPythonScript = null, bool useNamedArguments = false, bool useTerminal = false, string pythonPath = null, string stdOutVariableName = null, string stdErrorVariableName = null)
```

For more detailed documentation and all examples, please refer to [PLang.Modules.PythonModule.md](./PLang.Modules.PythonModule.md) and the [plang Python Tests repository](https://github.com/PLangHQ/plang/tree/main/Tests/Python). Additionally, users can look at the Program.cs source code for a deeper understanding of the module's implementation: [PLang.Modules.PythonModule/Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.PythonModule/Program.cs).

## Source code

Program.cs is the runtime code for the Python module and can be found at [PLang.Modules.PythonModule/Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.PythonModule/Program.cs).

## How plang is mapped to C#

### Builder

When a user runs `plang build`, the .goal file is processed as follows:

1. Each step in the goal file (line starts with `-`) is parsed.
2. For each step, a query is sent to the LLM, which can be found in [StepBuilder.cs](https://github.com/PLangHQ/plang/blob/main/PLang/Building/StepBuilder.cs).
3. Along with the query, the StepBuilder provides LLM with a list of all available modules.
4. LLM suggests a module to use, in this case, `PLang.Modules.PythonModule`.
5. The builder then sends all the methods in the Python module to LLM along with the step.
6. This is done using either `Builder.cs` (see source code) or `BaseBuilder.cs` (https://github.com/PLangHQ/plang/blob/main/PLang/Modules/BaseBuilder.cs), depending on the availability of `Builder.cs`.
7. LLM returns a JSON that maps the step text to a C# method with the required parameters.
8. The `Builder.cs` or `BaseBuilder.cs` creates a hash of the response to store with the instruction file.
9. An instruction file with the .pr extension is saved at the location `.build/{GoalName}/01. {StepName}.pr`.

### Runtime

The .pr file is then used by the plang runtime to execute the step:

1. The plang runtime loads the .pr file.
2. The runtime uses reflection to load the `PLang.Modules.PythonModule`.
3. The .pr file contains a "Function" property.
4. The Function property instructs the runtime on which C# method to call.
5. Parameters are provided if the method requires them.

### plang example to csharp

Here is how a plang code example maps to a C# method and the corresponding .pr file:

**plang Code Example:**
```plang
Python
- call main.py, write to %output%
```

**Mapped C# Method in Python Module:**
```csharp
RunPythonScript("main.py")
```

**Example Instruction .pr file:**
```json
{
  "Action": {
    "FunctionName": "RunPythonScript",
    "Parameters": [
      {
        "Type": "string",
        "Name": "fileName",
        "Value": "main.py"
      }
    ],
    "ReturnValue": {
      "Type": "Dictionary<string, object>",
      "VariableName": "output"
    }
  }
}
```

Default values do not need to be defined in the JSON .pr file. The "ReturnValue" is only included if the C# method returns a value.

## Created

This documentation was created on 2024-01-02T22:16:39.