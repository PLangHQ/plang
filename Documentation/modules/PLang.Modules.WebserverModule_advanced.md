# Webserver

## Introduction

The Webserver module in plang is designed to provide a straightforward way to set up a basic web server for handling HTTP requests. This module is particularly useful for developers who need to quickly prototype APIs or serve static content without the overhead of a full-fledged web server setup.

In plang, natural language steps are translated into C# methods through a process involving the Builder and Runtime components. The Builder interprets the steps and maps them to corresponding C# methods, while the Runtime executes the generated instructions. This documentation will guide you through the process of using the Webserver module in plang and how it integrates with C#.

## Plang code examples

For simple documentation and examples, please refer to [PLang.Modules.WebserverModule.md](./PLang.Modules.WebserverModule.md). The repository for additional examples can be found at [plang Webserver Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Webserver).

### Start a Basic Webserver

This is a common use case where a user wants to start a web server listening on the default HTTP port.

```plang
Webserver
- start webserver
- write out 'Webserver started on http://localhost:8080'
```

**C# Method Signature:**
```csharp
Task StartWebserver(string scheme = "http", string host = "localhost", int port = 8080, int maxContentLengthInBytes = 4194304, string defaultResponseContentEncoding = "utf-8", bool signedRequestRequired = false, List<string> publicPaths = null)
```

For more detailed documentation and all examples, please refer to [PLang.Modules.WebserverModule.md](./PLang.Modules.WebserverModule.md) and the [plang Webserver Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Webserver). For a deeper understanding of the implementation, examine the Program.cs source code at [PLang.Modules.WebserverModule/Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.WebserverModule/Program.cs).

## Source code

The runtime code for the Webserver module, Program.cs, can be found at [PLang.Modules.WebserverModule/Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.WebserverModule/Program.cs).

## How plang is mapped to C#

### Builder

When a user runs `plang build`, the .goal file is processed as follows:

1. Each step in the goal file (line starts with `-`) is parsed.
2. For each step, a query is sent to LLM, along with a list of all available modules. The LLM suggests a module to use, in this case, `PLang.Modules.WebserverModule`.
3. The Builder sends all the methods in the Webserver module to LLM along with the step.
4. This is done either with `Builder.cs` (see source code) or `BaseBuilder.cs` (depending on availability).
5. LLM returns a JSON that maps the step text to a C# method with the required parameters.
6. The `Builder.cs` or `BaseBuilder.cs` creates a hash of the response to store with the instruction file.
7. An instruction file with the .pr extension is saved at the location `.build/{GoalName}/01. {StepName}.pr`.

### Runtime

The .pr file is then used by the plang runtime to execute the step:

1. The plang runtime loads the .pr file.
2. The runtime uses reflection to load the `PLang.Modules.WebserverModule`.
3. The .pr file contains a "Function" property, which specifies the C# method to call.
4. Parameters are provided if the method requires them.

### plang example to csharp

Here's how a plang code example maps to a method in the Webserver module and the corresponding .pr file:

**plang code example:**
```plang
Webserver
- start webserver
- write out 'Webserver started on http://localhost:8080'
```

**Example Instruction .pr file:**
```json
{
  "Action": {
    "FunctionName": "StartWebserver",
    "Parameters": [
      {
        "Type": "string",
        "Name": "scheme",
        "Value": "http"
      },
      {
        "Type": "string",
        "Name": "host",
        "Value": "localhost"
      },
      {
        "Type": "int",
        "Name": "port",
        "Value": 8080
      }
    ]
  }
}
```

## Created

This documentation was created on 2024-01-02T22:36:08.