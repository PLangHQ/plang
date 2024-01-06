
# Cryptographic

## Introduction
The Cryptographic module in plang is designed to provide developers with a suite of tools for handling various cryptographic operations such as encryption, decryption, hashing, and token management. This documentation is tailored for advanced users who are familiar with programming concepts and are looking to understand how plang's natural language steps are mapped to C# methods.

plang integrates with C# by using a Language Learning Model (LLM) to interpret natural language steps and map them to corresponding C# methods. This process involves parsing the steps defined in a .goal file, sending them to the LLM along with a list of available modules, and receiving a JSON response that maps the steps to C# methods with the necessary parameters.

## Plang code examples
For simple documentation and examples, refer to [PLang.Modules.CryptographicModule.md](./PLang.Modules.CryptographicModule.md). The repository for additional examples can be found at [PLang Cryptographic Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Cryptographic).

### Encrypting Text
Encrypting text is a common operation in cryptographic practices. The following plang code example demonstrates how to encrypt a string and store the result in a variable.

```plang
- set var %text% to 'Secret Message'
- encrypt %text%, write to %encryptedText%
```
C# Method Signature: `Task<string> Encrypt(object content)`

### Decrypting Text
After encryption, the ability to decrypt the text back to its original form is essential. Below is a plang code example for decrypting text.

```plang
- decrypt %encryptedText%, write to %decryptedText%
```
C# Method Signature: `Task<object> Decrypt(string content)`

### Hashing a Password
Creating a secure hash of a password is a fundamental security measure. Here's how you would hash a password in plang.

```plang
- set var %password% as 'MySecurePassword'
- create salt, write to %salt%
- hash %password% using salt %salt%, write to %hashedPassword%
```
C# Method Signature: `Task<string> HashInput(string input, bool useSalt = false, string salt = null, string hashAlgorithm = "keccak256")`

For more detailed documentation and examples:
- Refer to [PLang.Modules.CryptographicModule.md](./PLang.Modules.CryptographicModule.md).
- Explore the [Cryptographic Test Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Cryptographic).
- Review the Program.cs source code at [PLang Cryptographic Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CryptographicModule/Program.cs).

## Source code
- Program.cs (runtime code): [PLang Cryptographic Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CryptographicModule/Program.cs)
- Builder.cs (step building): [PLang Cryptographic Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CryptographicModule/Builder.cs)
- ModuleSettings.cs (module settings): [PLang Cryptographic ModuleSettings.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CryptographicModule/ModuleSettings.cs)

## How plang is mapped to C#

### Builder
When a user runs `plang build`, the .goal file is read and processed as follows:
1. Each step in the goal file (line starts with `-`) is parsed.
2. A question is sent to LLM for each step, along with a list of all available modules.
3. LLM suggests a module to use, in this case, `PLang.Modules.CryptographicModule`.
4. Builder.cs or BaseBuilder.cs (if Builder.cs is not available) sends all the methods in the Cryptographic module to LLM along with the step.
5. LLM returns a JSON that maps the step text to a C# method with the required parameters.
6. Builder.cs or BaseBuilder.cs creates a hash of the response to store with the instruction file.
7. An instruction file with the .pr extension is saved at the location `.build/{GoalName}/01. {StepName}.pr`.

### Runtime
The .pr file is then used by the plang runtime to execute the step:
1. plang runtime loads the .pr file.
2. plang runtime uses reflection to load the `PLang.Modules.CryptographicModule`.
3. The .pr file contains a "Function" property, which specifies the C# method to call.
4. Parameters are provided if the method requires them.

### plang example to csharp
Here's how a plang code example is mapped to a method in the Cryptographic module and the resulting .pr file:

```plang
- generate bearer token for %uniqueString%, expires in 1 hour, write to %bearerToken%
```

C# Method Signature: `Task<string> GenerateBearerToken(string uniqueString, string issuer = "PLangRuntime", string audience = "user", int expireTimeInSeconds = 3600)`

### Example Instruction .pr file
```json
{
  "Action": {
    "FunctionName": "GenerateBearerToken",
    "Parameters": [
      {
        "Type": "string",
        "Name": "uniqueString",
        "Value": "user123"
      },
      {
        "Type": "int",
        "Name": "expireTimeInSeconds",
        "Value": "3600"
      }
    ],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "bearerToken"
    }
  }
}
```

## Created
This documentation was created on 2024-01-02T21:43:15.
