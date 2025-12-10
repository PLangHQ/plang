# Llm Module Documentation

## Introduction
The Llm module is a powerful component of the plang programming language, designed to facilitate seamless interaction with large language models (LLMs). It allows users to leverage the capabilities of LLMs to generate responses, manage conversations, and perform various tasks through natural language input. The integration of plang with C# methods enables developers to create dynamic applications that can interpret and execute user-defined goals effectively.

In plang, each goal is defined in a .goal file, where users can specify multiple steps that the LLM will process. These steps are then mapped to corresponding C# methods, allowing for a smooth transition from natural language to executable code. This documentation provides an overview of common usage patterns, code examples, and insights into how plang translates user input into C# method calls.

## Plang Code Examples
For a comprehensive understanding of the Llm module, users can refer to the simple documentation and examples found at [PLang.Modules.LlmModule.md](./PLang.Modules.LlmModule.md). Additionally, the repository for examples can be found at [GitHub - PLangHQ/plang](https://github.com/PLangHQ/plang/tree/main/Tests/Llm).


**C# Method Signature:**
```csharp
public (IReturnDictionary?, IError?) AskLlm(List<LlmMessage> promptMessages, string? scheme, string model = "gpt-4o-mini", double temperature = 0, double topP = 0, double frequencyPenalty = 0.0, double presencePenalty = 0.0, int maxLength = 4000, bool cacheResponse = true, string? llmResponseType = null, string loggerLevel = "trace", bool continuePrevConversation = false);
```

### Example 2: Append to System, Assistant and user

If you like to include some system command to all llm for that context, you can use the append actions

```plang
Start
- append to system 'Your name is Lucy, working for Achme. You should answer as Shakespear'
- [llm] system: "Analyze request from user"
        user: "Tell me a story about your company"
        write to %result%
- write out %result%
```

This is usefull for example when you want set basic information such about your company or the style of writing for all llm request in that context. You could set this in an event on app start or when specific goals run.

**C# Method Signature:**
```csharp
public void AppendToSystem(string system);
public void AppendToAssistant(string assistant);
public void AppendToUser(string user);
```


Not all methods are demonstrated here. For more details, users can refer to the simple documentation and all examples found at [PLang.Modules.LlmModule.md](./PLang.Modules.LlmModule.md) and the repository for examples at [GitHub - PLangHQ/plang](https://github.com/PLangHQ/plang/tree/main/Tests/Llm). Additionally, users are encouraged to look at the source code in [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.LlmModule/Program.cs).

## Source Code
The runtime code can be found at [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.LlmModule/Program.cs). The Builder code, which is responsible for building steps, can be found at [Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.LlmModule/Builder.cs).

## How plang is mapped to C#
This section explains how modules are utilized in plang.

### Builder
When the user runs plang build, the .goal file is read:
1. Each step in the goal file (lines starting with -) is parsed.
2. For each step, a question is sent to the LLM.
3. The StepBuilder sends a list of all available modules to the LLM.
4. The LLM returns a suggestion of the module to use, in this case, PLang.Modules.LlmModule.
5. The builder sends all the methods in the Llm to the LLM along with the step.

This process is executed using either Builder.cs or BaseBuilder.cs, depending on the availability of Builder.cs. The LLM returns a JSON response that maps the step text to a C# method and its required parameters. The Builder.cs or BaseBuilder.cs:
- Creates a hash of the response to store with the instruction file.
- Saves a JSON instruction file with the .pr extension at the location .build/{GoalName}/01. {StepName}.pr.

### Runtime
The .pr file is then utilized by the plang runtime to execute the step:
1. The plang runtime loads the .pr file.
2. It uses reflection to load the PLang.Modules.LlmModule.
3. The .pr file contains a "Function" property, indicating which C# method to call.
4. Parameters may be provided if the method requires them.

### Plang Example to C#
**Plang Code Example:**
```plang
- set %userQuestion% as 'What are the benefits of using AI in healthcare?'
- [llm] system: provide a detailed answer to the user question
        user: %userQuestion%
        scheme: {benefits:string[]}
```

**Mapped C# Method:**
- This plang code maps to the `AskLlm` method in the Llm module.

**Example Instruction .pr file:**
```json
{
  "Action": {
    "FunctionName": "AskLlm",
    "Parameters": [
      {
        "Type": "List<LlmMessage>",
        "Name": "promptMessages",
        "Value": "[{ \"role\": \"user\", \"content\": \"What are the benefits of using AI in healthcare?\" }]"
      },
      {
        "Type": "string?",
        "Name": "scheme",
        "Value": "{benefits:string[]}"
      }
    ],
    "ReturnValue": {
      "Type": "object",
      "VariableName": "benefits"
    }
  }
}
```

## Created
This documentation is created on 2024-08-27T14:56:47.