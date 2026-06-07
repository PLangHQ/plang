# Build validation reads the typed handler, not a raw dict

> **Note for coder / test-designer:** every snippet, signature, method name, and file path below is a suggestion that captures architect intent — not a contract. You own the final shape. Reshape, rename, restructure, or replace anything as the real constraints of implementation demand. Push back if the design itself looks wrong.

## Why

Build-time validation and runtime read the **same** LLM-produced parameter dict, but through two different code paths.

At runtime the handler is constructed and each parameter is a lazy `Data<T>` property that materializes itself once through `As<T>` — `Run()` just reads `this.Value`, `this.Type`. At build time the handler's `ValidateBuild` is handed the raw `List<Data>` bag, digs into it by string (`parameters.FirstOrDefault(p => p.Name == "Value")`), and re-derives types by hand. So the same type / strict-kind logic gets written twice — typed-and-lazy in `Run()`, untyped-and-static in `ValidateBuild` — and the two can drift.

`As<T>` is already the "dict in, typed value out" transform. The fix is not to add a transform; it's to let build validation use the one that already runs at runtime, by reading the handler's own typed properties.

There's a precedent for exactly this **in the same file**: `IClass.Build()` is an instance method the builder constructs and calls at build time to read the handler's typed props (`file.read` reads `.Path` to infer `csv`, `llm.query` infers `json`). `ValidateBuild` is the odd one out — `static`, reflection-invoked, fed the raw bag. This branch makes `ValidateBuild` match `Build()`.

## The picture

Today — one dict, two readers, type logic written twice:

```
LLM output (a dict)            Action.Parameters = List<Data>
  set %x% = 5      ───────►    [ {Name:"Value", Value:"5"}, {Name:"Type", Value:null} ]
                                        │
            ┌───────────────────────────┴────────────────────────────┐
            ▼                                                          ▼
  BUILD                                                       RUN
  static ValidateBuild(List<Data> parameters)                class Set {
    parameters.FirstOrDefault(p=>p.Name=="Value")  ← string     Value = As<T>   ← lazy, typed
    manual TryConvert(...)                            digging    Type  = As<T>
  reflection-invoked from the registry                          Run(){ uses this.Value, this.Type }
                                                              }
```

After — construct once, both readers read the same typed props through the same `As<T>`:

```
  Parameters ──► class Set { Value = As<T>, Type = As<T> }      ← constructed once (SetAction)
                        │
                        ├──► ValidateBuild()  reads this.Value, this.Type
                        └──► Run()            reads this.Value, this.Type
                                  same typed props · same As<T> · no string digging · no reflection
```

## What changes

`IBuildValidatable.ValidateBuild` goes from a `static` method fed the raw param list to an **instance method** on the constructed handler that reads its own lazy typed properties — mirroring `IClass.Build()`.

Before (`PLang/app/module/Attributes.cs:69`):
```csharp
public interface IBuildValidatable {
    static abstract string? ValidateBuild(List<data.@this> parameters);
}
```

After:
```csharp
public interface IBuildValidatable {
    // reads this.Value, this.Type, this.Messages, ... — the same lazy props Run() reads
    string? ValidateBuild();
}
```

`SetAction(action, context)` already exists on `IClass` and already wires the lazy getters. `ValidateBuild` rides that same construction — no new wiring, no generator change. Because `Build()` and `ValidateBuild()` now share construction and a build pass, **`ValidateBuild` could just fold into `IClass`** (one optional compile-time hook surface instead of two). Coder's call — both are fine; folding is tidier, keeping `IBuildValidatable` separate keeps the roles named. State the choice in the handoff.

`return string?` stays: `null` = valid, non-null = the human/LLM-readable message that drives self-correction. (If aligning with `IClass.Build()`'s `Data` return reads cleaner, that's the coder's call — but the string is purpose-clear here and feeds the re-prompt directly.)

## Leaf trace + call-site disposition

**Construction seam** (already exists, shared by Build / Run / now ValidateBuild): `modules.GetCodeGenerated(a)` → `classified.SetAction(a, context)`. After this, lazy props resolve via `__ResolveData(name).As<T>(Context)`. Nothing new to build — `RunBuildPass` (`Default.cs:609`) already does exactly this for `Build()`.

**Validation seam** (what this branch changes): `ValidateBuild` reads the handler's typed props instead of dredging the raw bag.

| # | Call site | Disposition |
|---|---|---|
| 1 | `Default.cs:568-579` — `GetMethod("ValidateBuild", Static)` + `Invoke(null, [a.Parameters])` | **Delete the reflection invoke.** Call `instance.ValidateBuild()` in the build pass — either fold into the `RunBuildPass` loop (construct once, run ValidateBuild then Build) or a sibling loop using the same `GetCodeGenerated`+`SetAction`. If folded, run ValidateBuild **before** Build and short-circuit on error so a bad mapping doesn't reach type-stamping. |
| 2 | `set.cs` `static ValidateBuild(List<Data>)` | → instance `ValidateBuild()` reading `this.Value`, `this.Type`. The `HasVariableReference` guard stays (see below). |
| 3 | `llm/query.cs` `static ValidateBuild(List<Data>)` | → instance `ValidateBuild()` reading `this.Messages`. |
| 4 | `Default.cs:538-565` — required-param check (reflect over props vs emitted names) | **Stays.** This is a cross-cutting schema check, not handler-specific behavior — it doesn't need the handler instance. Leave it where it is. |

`validateResponse.cs:193` uses `GetActionType` for response-level validation — **out of scope**, untouched.

## The `%var%` wall (unchanged, state it so no one is surprised)

At build time most parameter values are `%var%` placeholders with no value yet. Build validation can only check **literals**; anything carrying a `%var%` reference defers to `Run`. The instance `ValidateBuild` keeps the same guard, just typed:

```csharp
if (this.Value.HasVariableReference) return null;   // defers to runtime, as today
```

Reading a typed property does **not** escape this — `As<T>` cannot turn `%filePath%` into a real `path` before the variable exists. So `ValidateBuild` is, and stays, literal + shape validation. The win is not "validate more at build"; it's "stop writing the literal-validation logic twice."

## Optional follow-on (not required for this branch)

`set.ValidateBuild` and `set.Run` currently hold near-duplicate literal type / strict-kind logic (resolve the `Type` entity → `ClrType` → `TryConvert` / `IKindValidatable` probe). Once both read the same typed `Type` / `Value`, that logic can be pulled onto the type entity and shared once. Worth doing, but it's a second step — keep this branch to the seam change so the diff stays legible. Note it for whoever picks it up.
