# Caching

## Introduction
Caching is a critical aspect of modern software development, allowing for the temporary storage of data to improve performance and reduce latency. The `plang` programming language offers a Caching module that seamlessly integrates with C# methods to facilitate caching operations within your application. This documentation provides an advanced overview of the Caching module, its purpose, and how it maps to C# methods for efficient data caching.

## Plang code examples
For simple documentation and examples, refer to [PLang.Modules.CachingModule.md](./PLang.Modules.CachingModule.md). The repository for additional examples can be found at [plang Caching Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Caching).

### Set Cache with Sliding Expiration
This is a common use case where data is cached and set to expire after a period of inactivity.

```plang
- cache 'userSessionData' for 10 minutes, to 'sessionKey'
```

Maps to C# method signature:
```csharp
void SetForSlidingExpiration(string key, object value, TimeSpan slidingExpiration)
```

### Get Cached Item
Retrieving a cached item is a frequent operation to avoid unnecessary computations or data fetching.

```plang
- get cache 'sessionKey', write to %cachedSession%
```

Maps to C# method signature:
```csharp
object Get(string key)
```

### Remove Cached Item
Removing a cached item is necessary when the data is no longer needed or has become stale.

```plang
- remove cache 'obsoleteDataKey'
```

Maps to C# method signature:
```csharp
void RemoveCache(string key)
```

For more detailed documentation and all examples, refer to [PLang.Modules.CachingModule.md](./PLang.Modules.CachingModule.md) and the [plang Caching Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Caching). Additionally, inspect the Program.cs source code at [PLang.Modules.CachingModule Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/CachingModule/Program.cs).

## Source code
Program.cs is the runtime code for the Caching module and can be found at [PLang.Modules.CachingModule Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/CachingModule/Program.cs).

## How plang is mapped to C#
Modules in plang are utilized through a build and runtime process.

### Builder
When a user runs `plang build`, the .goal file is read:
1. Each step in the goal file (line starts with -) is parsed.
2. For each step, a question is sent to LLM, along with a list of all available modules.
3. LLM suggests a module to use, in this case, `PLang.Modules.CachingModule`.
4. Builder sends all the methods in the Caching module to LLM along with the step.
5. This is done either with `Builder.cs` (see source code) or `BaseBuilder.cs` (if `Builder.cs` is not available).
6. LLM returns a JSON that maps the step text to a C# method with the required parameters.
7. The `Builder.cs` or `BaseBuilder.cs` creates a hash of the response to store with the instruction file.
8. An instruction file with the .pr extension is saved at `.build/{GoalName}/01. {StepName}.pr`.

### Runtime
The .pr file is then used by the plang runtime to execute the step:
1. Plang runtime loads the .pr file.
2. Plang runtime uses reflection to load the `PLang.Modules.CachingModule`.
3. The .pr file contains a "Function" property.
4. The Function property tells the runtime which C# method to call.
5. Parameters are provided if the method requires them.

### plang example to csharp
Here's how a plang code example maps to a C# method and the corresponding .pr file:

```plang
- cache 'userSessionData' for 10 minutes, to 'sessionKey'
```

Maps to C# method in `Caching`:
```csharp
SetForSlidingExpiration("sessionKey", userSessionData, TimeSpan.FromMinutes(10));
```

Example Instruction .pr file:
```json
{
  "Action": {
    "FunctionName": "SetForSlidingExpiration",
    "Parameters": [
      {
        "Type": "string",
        "Name": "key",
        "Value": "sessionKey"
      },
      {
        "Type": "object",
        "Name": "value",
        "Value": "userSessionData"
      },
      {
        "Type": "TimeSpan",
        "Name": "slidingExpiration",
        "Value": "00:10:00"
      }
    ]
  }
}
```

## Created
This documentation was created on 2024-01-02T21:29:09.