# Code vs Goals — When to Drop Down

> **Everything is goals, except where you need code.**

PLang is a goal-first language. The expectation is that almost every piece of behaviour you write lives in a `.goal` file: natural-language steps, composed and orchestrated through other goals. The C# escape hatch — the `app.modules.code` registry and the `code/` folders inside each module — exists for the cases where goals can't reach.

This page is about that boundary: when goals are the right tool, when to reach for code, and how the two surfaces meet.

## Why goals first

A goal is text. Text is auditable, diffable, signable, and readable by humans and LLMs alike. Steps are commitments — what should happen, in what order, with what data. The runtime is responsible for *how*. That separation is the whole point of PLang: developers describe intent, the system handles mechanics.

If you can express it as a sequence of steps calling existing modules (`file.read`, `http.request`, `llm.query`, `set`, `if`, `foreach`, `output.write`), it should be a goal. Not because goals are easier — sometimes they aren't — but because goals carry the trust, observability, and replay properties the runtime is built to give you. A signed `.pr` rides those rails. A C# DLL doesn't.

## When you actually need code

There are four honest reasons to drop into C#:

1. **Wire protocols that don't have a goal-level equivalent.** Talking to a TLS endpoint, parsing a binary file format, integrating with a SDK that owns its own request loop. The default `IHttp`, `ILlm`, `ISigning`, `ICrypto` implementations are all examples — they translate goal-shaped intent (sign this, fetch that) into wire-shaped operations (curve25519 math, `HttpClient` calls, OpenAI tool loops).
2. **Hot paths the runtime owns.** Things called from inside the runtime — type conversion, condition evaluation, template rendering — can't bootstrap themselves out of goals. `IEvaluator` (condition normalisation), `ITemplate` (Liquid rendering), and `IBuilder` (build-time goal parsing and `.pr` merging) all live in `code/` for this reason.
3. **Algorithms whose semantics live in code.** `keccak256`, `ed25519`, `sha256`. There's no goal-level expression of "hash these bytes with this curve" — the operation *is* the C#.
4. **Pluggability the user wants.** A PLang developer who needs to swap a default for a custom one (Postgres-backed identity, in-house signing HSM, an alternative LLM transport) drops a DLL implementing the relevant `ICode` interface and runs `code.load`. This is the user-sovereign extension point.

If your reason isn't one of those four, write a goal.

## The `app.modules.code` registry

`app.modules.code` (`PLang/app/modules/code/this.cs`) is the runtime's named code-implementation registry — `ConcurrentDictionary<Type, ConcurrentDictionary<string, ICode>>`. Each module interface (`ISigning`, `IHttp`, `ILlm`, etc.) can have multiple named implementations registered against it. First registered for a type becomes the default.

**Resolution at the call site:**

```csharp
var http = app.Code.Get<IHttp>();           // default
var rsa  = app.Code.Get<ISigning>("rsa");   // by name
```

Action handlers usually don't call this directly — they declare `[Code] IHttp Http` on the action record and the source generator emits the eager `app.Code.Get<T>()` lookup at the start of `ExecuteAsync`.

**Where the implementations live:**

```
PLang/app/modules/<module>/code/
    Default.cs            # the built-in implementation
    I<Name>.cs            # the interface
    <Variant>.cs          # alternative impls (e.g. Ed25519.cs, OpenAi.cs)
```

That `code/` folder is the C# half of every module. The action records (`sign.cs`, `verify.cs`, `query.cs`) are thin one-line delegates that call into the implementation — the `code/` folder owns the logic.

## The `code.*` PLang verbs

PLang developers manage the registry from goals via the `code` module:

```plang
- load code 'plugins/RsaSigning.dll'
- list signing code, write to %impls%
- set default signing code to 'rsa'
- remove code 'rsa' from signing
```

`code.load` scans the assembly for `ICode` implementations, registers each for its derived interfaces, and returns the registered names. The default `IBuilder` and the rest of the runtime see the new implementation immediately — no restart, no rewrite of consuming goals.

Reference: [`docs/modules/code.md`](../../docs/modules/code.md).

## The `[Code]` attribute

When an action handler needs an `ICode` implementation, the property carries the `[Code]` attribute:

```csharp
[Action("query")]
public partial class query : IContext
{
    [Code] public required ILlm Llm { get; init; }
    public partial Data.@this<List<LlmMessage>>? Messages { get; init; }
    // ...
}
```

The source generator emits eager resolution: `Llm = app.Code.Get<ILlm>().Value` at the top of `ExecuteAsync`. If the registry has no `ILlm`, the handler fails fast before any user-visible work begins.

This is the only sanctioned shape for an action property that wants infrastructure. Action property kinds are gated at build time by `PLNG001`: a property must be `Data<T>` or `[Code] T`. Anything else fails the build.

## Boundary contract

When you write code in a `code/` folder, you take on three responsibilities:

1. **Return `Data` or `Data<T>`.** Errors as `Data.Fail(...)`, success as `Data.Ok(...)`. No exceptions across the boundary — the runtime expects every failure to be inspectable as `data.Error`.
2. **Honour `Context`.** Every implementation method takes a `Context` (or an action record that carries one). Cancellation, debug routing, channel writes — all of it goes through `Context`.
3. **Implement `ICode` properly.** `Name`, `IsDefault`, `IsBuiltIn`, `Source`. The snapshot system uses these to decide whether your implementation can be reconstructed at App boot or whether it needs a DLL on disk to come back.

In return, the runtime gives you:

- A registered place in the snapshot graph — you participate in `app.Snapshot()` and `Restore`.
- Free swappability — your default can be replaced via `code.load` without anyone editing call sites.
- Lifetime management — `IAsyncDisposable` / `IDisposable` are honoured by `app.modules.code.DisposeAsync()`.

## Decision flow

```
Have a thing to do.
   │
   ├── Can it be a sequence of existing module actions?
   │      └── Yes → write a goal. Stop.
   │
   ├── Is it a wire protocol, hot-path runtime concern, or specific algorithm?
   │      └── Yes → write/extend an ICode implementation in app/modules/<m>/code/.
   │              Expose it as actions (thin delegate records under modules/<m>/).
   │
   └── Is it a user-extension a PLang developer should be able to swap in?
          └── Yes → ship it as a DLL, document the ICode interface, point at code.load.
```

If the goal-route ever feels "too verbose" for something that should be a one-liner, that's a signal the missing piece is a *new module action*, not a C# escape. Add the action under `app/modules/<m>/`, route it through the existing `code/` implementation, and goals can use it. The escape hatch should stay an escape, not the highway.

## See also

- [`docs/modules/code.md`](../../docs/modules/code.md) — the user-facing `code` module reference.
- [`good_to_know.md`](good_to_know.md) — `app.modules.code — Pluggable Module Implementations` covers the registry mechanics in depth.
- [`architecture.md`](architecture.md) — `Property kinds (PLNG001 build-time gate)` covers the `[Code]` attribute contract.
- `PLang/app/modules/code/this.cs` — the registry itself; `PLang/app/modules/code/ICode.cs` — the marker interface.
