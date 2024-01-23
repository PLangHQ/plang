
# CultureInfo

## Introduction
The `CultureInfo` module in plang is designed to handle cultural information in a program, such as language, date formats, number formats, and currency symbols. This module is particularly useful for applications that need to support multiple cultures or locales, ensuring that data is presented in a format that is familiar to the user.

In plang, cultural settings are defined using natural language steps that are then mapped to corresponding C# methods. This mapping is facilitated by a Language Learning Model (LLM) which interprets the steps and translates them into executable code during the build process.

## Plang code examples
For simple documentation and examples, please refer to [PLang.Modules.CultureInfoModule.md](./PLang.Modules.CultureInfoModule.md). The repository for additional examples can be found at [PLang CultureInfo Tests](https://github.com/PLangHQ/plang/tree/main/Tests/CultureInfo).

### Set Culture Language Code
This is a common usage scenario where the culture of the program is set to match a specific locale.

#### plang code example:
```plang
CultureInfo
- set culture to en-US
- write to %currentCulture%
```

#### C# method signature:
```csharp
Task SetCultureLanguageCode(string code)
```

For more detailed documentation and all examples, please refer to [PLang.Modules.CultureInfoModule.md](./PLang.Modules.CultureInfoModule.md) and explore the examples repository at [PLang CultureInfo Tests](https://github.com/PLangHQ/plang/tree/main/Tests/CultureInfo). Additionally, you can look at the `Program.cs` source code for the CultureInfo module [here](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/CultureInfoModule/Program.cs).

## Source code
The runtime code for the `CultureInfo` module, `Program.cs`, can be found at the [PLang CultureInfo Program](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/CultureInfoModule/Program.cs).

## How plang is mapped to C#
Modules in plang are utilized through a build and runtime process that translates natural language steps into executable C# code.

### Builder
When a user runs `plang build`, the `.goal` file is processed as follows:
1. Each step in the goal file (lines starting with `-`) is parsed.
2. For each step, a query is sent to LLM, along with a list of all available modules.
3. LLM suggests a module to use, in this case, `PLang.Modules.CultureInfoModule`.
4. The builder sends all methods in the `CultureInfo` module to LLM along with the step.
5. This is done using `Builder.cs` ([source code](https://github.com/PLangHQ/plang/blob/main/PLang/Building/StepBuilder.cs)) or `BaseBuilder.cs` ([source code](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/BaseBuilder.cs)) depending on the availability of `Builder.cs`.
6. LLM returns a JSON that maps the step text to a C# method with the necessary parameters.
7. The `Builder.cs` or `BaseBuilder.cs` creates a hash of the response to store with the instruction file.
8. An instruction file with the `.pr` extension is saved in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime
The `.pr` file is then used by the plang runtime to execute the step:
1. The plang runtime loads the `.pr` file.
2. Reflection is used to load the `PLang.Modules.CultureInfoModule`.
3. The `.pr` file contains a "Function" property, which specifies the C# method to call.
4. Parameters are provided if the method requires them.

### plang example to csharp
Here's how a plang code example maps to a `.pr` file:

#### plang code example:
```plang
CultureInfo
- set culture to en-US
- write to %currentCulture%
```

#### Example Instruction .pr file:
```json
{
  "Action": {
    "FunctionName": "SetCultureLanguageCode",
    "Parameters": [
      {
        "Type": "string",
        "Name": "code",
        "Value": "en-US"
      }
    ],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "currentCulture"
    }
  }
}
```

## Created
This documentation was created on 2024-01-02T21:46:33.
