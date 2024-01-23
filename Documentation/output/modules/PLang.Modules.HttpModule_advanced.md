
# Http Module in plang

## Introduction

The Http module in plang is designed to provide a straightforward and powerful way to perform HTTP requests within the plang programming language. This module abstracts the complexity of making HTTP calls and allows users to focus on the logic of their applications. Advanced users with programming experience will appreciate the seamless integration of plang with C# methods, enabling them to leverage the full power of the .NET framework's HTTP capabilities.

plang's natural language processing capabilities are used to map user-defined steps to corresponding C# methods in the Http module. This mapping is facilitated by the Builder during the build process and executed by the Runtime when running the plang code.

## Plang code examples

For simple documentation and examples, refer to [PLang.Modules.HttpModule.md](./PLang.Modules.HttpModule.md). The repository containing a comprehensive set of examples can be found at [PLangHQ/plang Http Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Http).

### GET Request Example

The most common usage of the Http module is to perform a GET request to retrieve data from a specified resource.

```plang
Http
- GET https://httpbin.org/get, write to %getResponse%
- write out 'UserAgent: %getResponse.headers.User-Agent%, ip: %getResponse.origin%'
```

C# method signature:
```csharp
public object Get(string url, object data = null, bool doNotSignRequest = false, Dictionary<string, object> headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 100)
```

For more detailed documentation and examples, visit [PLang.Modules.HttpModule.md](./PLang.Modules.HttpModule.md) and the [PLangHQ/plang Http Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Http). Additionally, inspect the Program.cs source code at [PLang.Modules.HttpModule Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/HttpModule/Program.cs).

## Source code

The runtime code for the Http module, Program.cs, can be found at [PLang.Modules.HttpModule Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/HttpModule/Program.cs). The Builder.cs, responsible for the construction of steps, is available at [PLang.Modules.HttpModule Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/HttpModule/Builder.cs).

## How plang is mapped to C#

### Builder

During the build process initiated by `plang build`, the following occurs:

1. The .goal file is read, and each step (line starting with `-`) is parsed.
2. For each step, a query is sent to the LLM, along with a list of all available modules, to suggest the appropriate module to use.
3. If Builder.cs is available, it is used; otherwise, BaseBuilder.cs is utilized to send all methods in the Http module to the LLM along with the step.
4. The LLM returns a JSON mapping the step text to a C# method with the required parameters.
5. The Builder.cs or BaseBuilder.cs creates a hash of the response and saves a JSON instruction file with the .pr extension in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime

The .pr file is executed by the plang runtime as follows:

1. The .pr file is loaded by the plang runtime.
2. Reflection is used to load the PLang.Modules.HttpModule.
3. The "Function" property in the .pr file indicates which C# method to call.
4. If required, parameters are provided to the method.

### plang example to csharp

Here is how a plang code example maps to a .pr file:

#### plang code example:
```plang
Http
- GET https://httpbin.org/get, write to %getResponse%
```

#### Corresponding .pr file:
```json
{
  "Action": {
    "FunctionName": "Get",
    "Parameters": [
      {
        "Type": "string",
        "Name": "url",
        "Value": "https://httpbin.org/get"
      }
    ],
    "ReturnValue": {
      "Type": "object",
      "VariableName": "getResponse"
    }
  }
}
```

## Created

This documentation was created on 2024-01-02T21:55:20.
