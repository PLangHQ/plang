# Module Structure Guide

How to build a Runtime2 module. Uses the `crypto` module as reference implementation.

## Directory Layout

```
PLang/Runtime2/modules/{modulename}/
    hash.cs                         # Action handler — one file per action
    verify.cs                       # Action handler
    providers/
        ICryptoProvider.cs          # Provider interface (if pluggable)
        DefaultProvider.cs          # Default implementation
    Config.cs                       # Module config class (if configurable)
    configure.cs                    # configure action handler (if configurable)
```

- Module name = folder name, lowercase: `crypto`, `file`, `http`
- One action per file, named after the action: `hash.cs`, `verify.cs`, `read.cs`
- Provider interfaces and implementations go in a `providers/` subfolder

## Action Handler

An action handler is a `partial class` with `[Action]`, `IContext`, and a `Run()` method. The source generator creates the other half — parameter resolution, validation, provider injection, error handling.

```csharp
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.modules.crypto;

[Example("hash %content%, write to %hash%", "Data=%content%, Algorithm=keccak256")]
[Example("hash %data% with sha256, write to %hash%", "Data=%data%, Algorithm=sha256")]
[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    [IsNotNull]
    public partial Data Data { get; init; }

    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    [Provider]
    public partial ICryptoProvider Crypto { get; }

    public async Task<Data> Run() => Crypto.Hash(this);
}
```

### Rules

- **`[Example(plang, mapping)]`** on the class. Shows PLang syntax and how it maps to properties. Multiple allowed. Machine-readable — builder prompt and doc tools extract these.
- **`[Action("name")]`** on the class. Name is the action name in .pr files (lowercase). `Cacheable = false` for non-deterministic actions.
- **`: IContext`** — gives the handler a `Context` property (PLangContext) set by the generator.
- **`partial class`** — required. The source generator creates the other half implementing `ICodeGenerated`.
- **`Run()` returns `Task<Data>`** — always. This is the only method the handler implements.
- **Properties are `partial`** with `{ get; init; }` — the generator creates the backing implementation with lazy resolution from .pr parameters and MemoryStack variables.
- **Pass `this` to the provider** — OBP rule. The provider navigates the action record for what it needs. Never decompose the handler into parameters: `Crypto.Hash(this)` not `Crypto.Hash(bytes, algorithm)`.
- **Handlers relay, not repackage** — if the provider returns `Data`, relay it directly. Don't crack open `.Value`, transform it, and create a new `Data`.

### What the Generator Creates

For each `[Action]` partial class, the generator creates:

1. **`ICodeGenerated` implementation** — `CodeGeneratedExecuteAsync` wires Context, resolves parameters from .pr Data list, resolves `%variables%` from MemoryStack
2. **Partial property implementations** — lazy getters that resolve from parameters, with `%var%` interpolation
3. **Validation** — non-nullable string/reference checks (`MissingParameter`), `[IsNotNull]`/`[IsInitiated]` checks
4. **Provider resolution** — `[Provider]` properties resolved from `engine.Providers.Get<T>()`
5. **Error wrapping** — exceptions in `Run()` caught and returned as `Data.FromError`

## Class Attributes

### `[Example(plang, mapping)]`
Shows how PLang developers write steps that use this action, and how the natural language maps to properties. Multiple allowed per class. The builder prompt and documentation tools can extract these at build time.

```csharp
[Example("hash %content%, write to %hash%", "Data=%content%, Algorithm=keccak256")]
[Example("hash %data% with sha256, write to %hash%", "Data=%data%, Algorithm=sha256")]
[Action("hash", Cacheable = false)]
public partial class Hash : IContext { ... }
```

Every action handler MUST have at least one `[Example]`. Show the most common usage first, then variations.

## Property Attributes

### `[Default(value)]`
Provides a default when the parameter isn't in the .pr file.

```csharp
[Default("keccak256")]
public partial string Algorithm { get; init; }
```

### `[IsNotNull]`
Generator validates `Value != null` (and `IsInitialized`) before `Run()`. Returns `ValueRequired` error if violated.

```csharp
[IsNotNull]
public partial Data Data { get; init; }

[IsNotNull]
public partial string Hash { get; init; }
```

### `[IsInitiated]`
Generator validates `Data.IsInitialized` before `Run()`. Allows null values (the parameter was passed, but its value is null). Returns `ParameterRequired` error if violated.

```csharp
[IsInitiated]
public partial Data OptionalValue { get; init; }
```

### `[Provider]`
Auto-resolves from `engine.Providers.Get<T>()`. Lazy — works both via engine pipeline and direct test usage. Returns `ProviderNotFound` error if no provider registered.

```csharp
[Provider]
public partial ICryptoProvider Crypto { get; }
```

Note: `{ get; }` not `{ get; init; }` — providers are resolved, not set from parameters.

### `[VariableName]`
Strips `%` markers instead of resolving the variable. Used when the action needs the variable name, not its value.

```csharp
[VariableName]
public partial string? VariableName { get; init; }
```

## Data-Typed Properties

PLang variables are `Data` objects. When an action property is typed as `Data`, the generator passes the `Data` object through without unwrapping `.Value`. This preserves Type, Properties, and all metadata.

```csharp
[IsNotNull]
public partial Data Data { get; init; }
```

- `Data` is never null — it's either uninitiated (`IsInitialized == false`) or has a value
- Use `Data` type for inputs that come from PLang variables
- Use `string`, `int`, etc. for simple scalar parameters the builder fills in

## Provider Pattern

Providers make module behavior pluggable. A PLang user can replace the default implementation by loading a DLL.

### Interface

```csharp
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.crypto.providers;

public interface ICryptoProvider : IProvider
{
    Data Hash(Hash action);
    Data Verify(Verify action);
}
```

- Extends `IProvider` (requires `Name` and `IsDefault`)
- Methods accept the **action record**, not decomposed parameters — OBP rule
- Methods return `Data` — success or error, never throw

### Default Implementation

```csharp
public class DefaultCryptoProvider : ICryptoProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    public Data Hash(Hash action)
    {
        var value = action.Data.Value;                    // Navigate the action
        var bytes = value is byte[] raw ? raw             // Provider decides how to handle types
            : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
        var algorithm = action.Algorithm.ToLowerInvariant();

        byte[]? hashBytes = algorithm switch
        {
            "keccak256" => new Sha3Keccack().CalculateHash(bytes),
            "sha256" => SHA256.HashData(bytes),
            _ => null
        };

        if (hashBytes == null)
            return Data.FromError(new ActionError(...));

        return Data.Ok(hashBytes, Type.FromName(algorithm));  // Return natural type
    }
}
```

- Provider navigates `action.Data.Value`, `action.Algorithm` — it reaches into the action for what it needs
- Returns natural types (`byte[]` for hash, not base64 string) — conversion happens at boundaries (serialization, display)
- Returns `Data.FromError` on failure — never throws
- `Data.Ok(value, Type)` — set the PLang Type on the result

### Registration

#### PLang user registration

PLang developers replace providers by loading a DLL that implements the provider interface:

```plang
- load provider myprovider.dll
```

The DLL contains a class implementing `ICryptoProvider` (or whichever provider interface). The engine discovers it, registers it, and all actions using `[Provider]` automatically pick it up.

#### Runtime developer registration

Built-in default providers are registered in `Engine/Providers/this.cs`, `RegisterDefaults()`:

```csharp
Register<ICryptoProvider>(new DefaultCryptoProvider());
```

Also add the type name mapping in `ResolveProviderType()` so providers can be resolved by name:

```csharp
"crypto" or "icryptoprovider" => typeof(ICryptoProvider),
```

## Return Values

- **Return natural types** — `byte[]` for binary data, `bool` for true/false, `string` for text. Don't pre-convert for display (no base64 in providers).
- **Set `Type`** on the result when meaningful — `Data.Ok(hashBytes, Type.FromName("keccak256"))` tells downstream code what algorithm produced it.
- **Relay Data** — if you call another action or provider and get `Data` back, return it directly. Don't unwrap and repackage.

## Testing

### C# Tests

Test both the action handler (through `Run()`) and through the generated path (`CodeGeneratedExecuteAsync`) for validation tests.

```csharp
// Normal test — direct Run()
var action = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = "keccak256" };
var result = await action.Run();
await Assert.That(result.Value is byte[]).IsTrue();

// Validation test — through generator
var action = new Hash { Data = new Data(""), Algorithm = "keccak256" };
var result = await action.CodeGeneratedExecuteAsync(new List<Data>(), engine, ctx);
await Assert.That(result.Error!.Key).IsEqualTo("ValueRequired");
```

### Mock Providers

Mock providers implement the interface with minimal logic:

```csharp
private class FailingCryptoProvider : ICryptoProvider
{
    public string Name => "failing";
    public bool IsDefault { get; set; }
    public Data Hash(Hash action) => Data.FromError(new ActionError("fail", "ProviderError", 500));
    public Data Verify(Verify action) => Data.FromError(new ActionError("fail", "ProviderError", 500));
}
```

Register and set as default before the test:

```csharp
_engine.Providers.Register<ICryptoProvider>(new FailingCryptoProvider());
_engine.Providers.SetDefault<ICryptoProvider>("failing");
```

## Checklist for New Modules

1. Create `PLang/Runtime2/modules/{name}/` folder
2. One `[Action]` partial class per action, implementing `IContext`
3. Add `[Example]` attributes — at least one per action, showing PLang syntax and property mapping
4. Properties: `partial` with `{ get; init; }`, use attributes for validation/defaults
5. If pluggable: create `providers/` with `I{Name}Provider : IProvider` and `Default{Name}Provider`
6. Provider methods accept action records (`this`), return `Data`
7. Register default provider in `Providers.RegisterDefaults()`
8. Add type name mapping in `Providers.ResolveProviderType()`
9. Handler's `Run()` should be minimal — delegate to provider, relay results
10. C# tests in `PLang.Tests/Runtime2/Modules/{name}/`
11. PLang integration tests in `Tests/Runtime2/{Name}/`
