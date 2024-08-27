# Llm

## Introduction
The `Llm` module in the plang programming language serves as an interface between natural language instructions and C# methods. It leverages a Language Learning Model (LLM) to interpret user-defined steps and map them to executable C# code. This documentation is tailored for advanced users who are familiar with programming concepts and are looking to understand the intricacies of how plang integrates with C#.

plang is a unique language that allows users to define their goals and steps in a natural language format. These steps are then translated into C# methods through a process involving the LLM, which interprets the steps and suggests the appropriate C# methods to execute. This process is facilitated by the Builder during the build phase and by the Runtime during execution.

## Plang code examples
For simple documentation and examples, please refer to [PLang.Modules.LlmModule.md](./PLang.Modules.LlmModule.md). The repository containing a comprehensive set of examples can be found at [plang Llm tests](https://github.com/PLangHQ/plang/tree/main/Tests/Llm).

### Example: Asking a Question to LLM
This is a common use case where a user wants to ask a question to the LLM and receive an answer. The plang code example below demonstrates how to set up a step to ask a question and store the response in a variable.

```plang
LLM
- set %question% as 'What is the capital of France?'
- [llm] system: answer the user question
        user: %question%
        write to %capital%
- write out 'The capital of France is: %capital%'
```

C# method signature:
```csharp
Task<string> AskLlm(string system, string user, string model = "gpt-4", double temperature = 0, double topP = 0, double frequencyPenalty = 0.0, double presencePenalty = 0.0, int maxLength = 4000, bool cacheResponse = true, string llmResponseType = null);
```

For more detailed documentation and all examples, please visit [PLang.Modules.LlmModule.md](./PLang.Modules.LlmModule.md) and the [plang Llm tests repository](https://github.com/PLangHQ/plang/tree/main/Tests/Llm). Additionally, the source code for the Llm module can be found in [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/LlmModule/Program.cs).

## Source code
The runtime code for the Llm module, `Program.cs`, is available at [plang Llm Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/LlmModule/Program.cs).
The builder code for the Llm module, `Builder.cs`, can be found at [plang Llm Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/LlmModule/Builder.cs).

## How plang is mapped to C#
The integration of plang with C# methods occurs through a two-phase process: the Builder phase and the Runtime phase.

### Builder
During the build process, the following steps are taken:
1. The Builder reads the .goal file, parsing each step (lines starting with `-`).
2. For each step, a question is formulated and sent to the LLM, along with a list of all available modules.
3. The LLM suggests the appropriate module to use, in this case, `PLang.Modules.LlmModule`.
4. The Builder sends all methods in the Llm module to the LLM, along with the step.
5. The LLM returns a JSON object that maps the step text to a C# method with the required parameters.
6. The Builder, using either `Builder.cs` or `BaseBuilder.cs`, creates a hash of the response and stores it with the instruction file.
7. An instruction file with the `.pr` extension is saved in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime
During execution, the .pr file is utilized as follows:
1. The plang runtime loads the .pr file.
2. The runtime uses reflection to load the `PLang.Modules.LlmModule`.
3. The .pr file contains a "Function" property, which indicates the C# method to call.
4. If required, parameters are provided as specified in the .pr file.

### plang example to csharp
Here is how a plang code example is mapped to a method in the Llm module and subsequently to a .pr file:

#### plang code example:
```plang
- [llm] system: answer the user question
        user: 'What is the capital of France?'
        write to %capital%
```

#### Example Instruction .pr file:
```json
{
  "Action": {
    "FunctionName": "AskLlm",
    "Parameters": [
      {
        "Type": "string",
        "Name": "system",
        "Value": "answer the user question"
      },
      {
        "Type": "string",
        "Name": "user",
        "Value": "What is the capital of France?"
      }
    ],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "capital"
    }
  }
}
```

## Created
This documentation was created on 2024-01-02T21:57:24.