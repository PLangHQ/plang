
# Blockchain

## Introduction
The Blockchain module in plang provides a seamless interface for developers to interact with blockchain technologies directly from plang code. This module encapsulates functionalities such as wallet management, transaction handling, smart contract interaction, and cryptographic operations. Advanced users with programming experience will appreciate the ease with which plang abstracts complex blockchain operations into simple, natural language steps that are then mapped to C# methods using a Large Language Model (LLM).

## Plang code examples
For simple documentation and examples, please refer to [PLang.Modules.BlockchainModule.md](./PLang.Modules.BlockchainModule.md). The repository for additional examples can be found at [plang blockchain tests](https://github.com/PLangHQ/plang/tree/main/Tests/Blockchain).

### Example: Get Current Address
This is a common operation to retrieve the current blockchain address from the wallet.
```plang
- get current address, write to %currentAddress%
```
**C# Method Signature:**
```csharp
Task<string> GetCurrentAddress();
```

### Example: Sign a Message
Signing a message is a standard way to prove ownership of an address.
```plang
- sign "Hello, blockchain!", write to %signature%
```
**C# Method Signature:**
```csharp
Task<string> SignMessage(string message);
```

### Example: Transfer Ether
Transferring cryptocurrency is a fundamental operation on the blockchain.
```plang
- transfer to 0xRecipientAddress, ether amount 0.1, write to %transactionHash%
```
**C# Method Signature:**
```csharp
Task<string> Transfer(string to, decimal etherAmount);
```

For more detailed examples and documentation, please refer to [PLang.Modules.BlockchainModule.md](./PLang.Modules.BlockchainModule.md) and explore the [plang blockchain tests](https://github.com/PLangHQ/plang/tree/main/Tests/Blockchain). Additionally, the source code for the Blockchain module can be found in [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.BlockchainModule/Program.cs).

## Source code
- [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.BlockchainModule/Program.cs) - Runtime code for the Blockchain module.
- [Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.BlockchainModule/Builder.cs) - Code for building steps in the Blockchain module.
- [ModuleSettings.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.BlockchainModule/ModuleSettings.cs) - Settings for the Blockchain module.

## How plang is mapped to C#

### Builder
When the user runs `plang build`, the .goal file is processed as follows:
1. Each step in the goal file (line starts with `-`) is parsed.
2. For each step, a query is sent to LLM along with a list of all available modules.
3. LLM suggests the appropriate module, in this case, `PLang.Modules.BlockchainModule`.
4. Builder sends all the methods in the Blockchain module to LLM along with the step.
5. This is done using `Builder.cs` if available, otherwise `BaseBuilder.cs`.
6. LLM returns a JSON mapping the step text to a C# method with the required parameters.
7. The Builder creates a hash of the response and saves a JSON instruction file with the `.pr` extension in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime
The .pr file is then used by the plang runtime to execute the step:
1. The plang runtime loads the .pr file.
2. The runtime uses reflection to load the `PLang.Modules.BlockchainModule`.
3. The .pr file contains a "Function" property, which specifies the C# method to call.
4. Parameters are provided if the method requires them.

### plang example to csharp
Here's how a plang code example maps to a .pr file:

**Plang Code Example:**
```plang
- get current address, write to %currentAddress%
```

**Mapped C# Method in Blockchain:**
```csharp
Task<string> GetCurrentAddress();
```

**Example Instruction .pr file:**
```json
{
  "Action": {
    "FunctionName": "GetCurrentAddress",
    "Parameters": [],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "currentAddress"
    }
  }
}
```

## Created
This documentation was created on 2024-01-02T21:25:53.
