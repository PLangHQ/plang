# Message Module in PLang

## Introduction

The Message module in PLang is designed to facilitate the sending and receiving of messages within a PLang application. This module provides a robust set of functionalities that are essential for applications requiring communication capabilities, such as chat applications or automated notification systems. 

PLang integrates seamlessly with C# through a sophisticated mapping system that translates PLang steps into C# method calls. This integration leverages the power of C# while maintaining the simplicity and readability of PLang.

## Plang code examples

For a comprehensive list of simple documentation and examples, please refer to [PLang.Modules.MessageModule.md](./PLang.Modules.MessageModule.md) and explore the [repository for examples](https://github.com/PLangHQ/plang/tree/main/Tests/Message).

### Example: Sending a Private Message to Myself
This is a common use case where a user wants to send a message to themselves, perhaps as a reminder or a test message.
```plang
- send my self message, 'Reminder: Meeting at %Now%'
```
**C# Method Signature:**
```csharp
void SendPrivateMessageToMyself(string content)
```

### Example: Listening for New Messages
Setting up a listener for incoming messages is crucial for real-time applications that need to respond to incoming data.
```plang
- listen for new message, call !NewMessage, write content to %content%, %sender% for sender address
```
**C# Method Signature:**
```csharp
void Listen(string goalName, string contentVariableName = "content", string senderVariableName = "sender")
```

For more detailed examples and documentation, visit [PLang.Modules.MessageModule.md](./PLang.Modules.MessageModule.md) and the [Message module test repository](https://github.com/PLangHQ/plang/tree/main/Tests/Message). Additionally, the source code for this module can be found in [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.MessageModule/Program.cs).

## Source code

- **Program.cs** is the runtime code, available at [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.MessageModule/Program.cs).
- **Builder.cs** is used for building steps, available at [Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.MessageModule/Builder.cs).
- **ModuleSettings.cs** contains settings for the module, available at [ModuleSettings.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.MessageModule/ModuleSettings.cs).

## How plang is mapped to C#

### Builder
1. During the build process, the `.goal` file is parsed where each step (line starting with `-`) is identified.
2. Each step is sent to the LLM along with a list of all available modules for module suggestion.
3. Based on the step description, the LLM suggests using the `PLang.Modules.MessageModule` and identifies the appropriate method along with required parameters.
4. Depending on the availability, either `Builder.cs` or `BaseBuilder.cs` is used to generate a JSON instruction file with the `.pr` extension, which includes the method name, parameters, and a hash of the response for verification.

### Runtime
1. The `.pr` file is loaded by the PLang runtime.
2. PLang runtime uses reflection to load the `PLang.Modules.MessageModule`.
3. The "Function" property in the `.pr` file dictates which C# method to invoke.
4. If parameters are specified, they are passed to the method accordingly.

### Example Instruction .pr file
```json
{
  "Action": {
    "FunctionName": "SendPrivateMessageToMyself",
    "Parameters": [
      {
        "Type": "string",
        "Name": "content",
        "Value": "Reminder: Meeting at %Now%"
      }
    ]
  }
}
```

## Created
This documentation was created on 2024-07-18T10:50:54.