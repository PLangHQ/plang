# Message Module in plang

## Introduction

The Message Module in plang is designed to facilitate the sending and receiving of private messages using the Nostr protocol within the plang programming language. This documentation is tailored for advanced users who are familiar with programming concepts and are looking to understand how plang integrates with C# methods to perform messaging operations.

plang is a natural language-driven programming language that leverages a Language Learning Model (LLM) to interpret user-defined steps and map them to corresponding C# methods. This process involves two main components: the Builder and the Runtime. The Builder is responsible for parsing the plang code and generating instructions, while the Runtime executes these instructions using the appropriate C# methods from the Message Module.

## Plang code examples

For simple documentation and examples, please refer to the [Message Module Documentation](./PLang.Modules.MessageModule.md). The repository containing a comprehensive set of examples can be found at [plang Message Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Message).

### Get Public Key
Retrieve your public key for messaging. This is a common operation when initiating communication in the Nostr protocol.

```plang
Message
- get my public key for messages, write to %myPublicKey%
```

**C# Method Signature:**
```csharp
Task<string> GetPublicKey();
```

### Send a Message to Another User
Send a private message to another user using their public key. This is a frequent use case for sending secure messages.

```plang
Message
- send message to %recipientPublicKey%, 'Hello, this is a private message!', write to %messageStatus%
```

**C# Method Signature:**
```csharp
Task SendPrivateMessage(string content, string npubReceiverPublicKey);
```

For additional methods and detailed examples, please refer to the [Message Module Documentation](./PLang.Modules.MessageModule.md) and the [plang Message Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Message). To understand the implementation details, examine the Program.cs source code at [PLang.Modules.MessageModule/Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/MessageModule/Program.cs).

## Source code

The source code for the Message Module is divided into several parts:

- **Program.cs**: The runtime code for the Message Module, available at [Program.cs source](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/MessageModule/Program.cs).
- **Builder.cs**: The builder code for constructing steps, available at [Builder.cs source](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/MessageModule/Builder.cs). If Builder.cs is not available, BaseBuilder.cs is used instead.
- **ModuleSettings.cs**: The settings for the Message Module, available at [ModuleSettings.cs source](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/MessageModule/ModuleSettings.cs).

## How plang is mapped to C#

### Builder

When a user runs `plang build`, the .goal file is processed as follows:

1. Each step in the goal file (lines starting with `-`) is parsed.
2. For each step, a query is sent to the LLM, along with a list of all available modules.
3. The LLM suggests a module to use, in this case, `PLang.Modules.MessageModule`.
4. The builder sends all the methods in the Message Module to the LLM along with the step.
5. The LLM returns a JSON that maps the step text to a C# method with the required parameters.
6. The Builder.cs or BaseBuilder.cs creates a hash of the response and saves a JSON instruction file with the .pr extension in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime

The .pr file is then used by the plang runtime to execute the step:

1. The plang runtime loads the .pr file.
2. The runtime uses reflection to load the `PLang.Modules.MessageModule`.
3. The .pr file contains a "Function" property, which specifies the C# method to call.
4. If the method requires parameters, they are provided in the .pr file.

### plang example to csharp

Here is how a plang code example is mapped to a C# method in the Message Module:

**plang code example:**
```plang
Message
- get my public key for messages, write to %myPublicKey%
```

**Mapped to C# Method:**
```csharp
Task<string> GetPublicKey();
```

**Example Instruction .pr file:**
```json
{
  "Action": {
    "FunctionName": "GetPublicKey",
    "Parameters": [],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "myPublicKey"
    }
  }
}
```

## Created

This documentation was created on 2024-01-02T22:08:19.