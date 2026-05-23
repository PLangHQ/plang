# Strongly typed action return types

**Goal:** every action class declares its runtime return type through its `Run()` method signature, not through an attribute. Source of truth = the method itself. No second declaration to keep in sync.

The compiler enforces correctness — if the body returns `Data<path>` somewhere and `Data<string>` elsewhere, it doesn't compile. Reflection at catalog-build time then reads the static `Task<Data<T>>` signature and surfaces `T` to the LLM. No drift possible.

## What we're trying to accomplish

When the builder LLM compiles a step like:

```
check if file 'test_output.txt' exists, write to %info%
```

It emits a chain:

```
file.exists(Path="test_output.txt") | variable.set(Name=%info%, Value=%!data%, Type=???)
```

Today the LLM picks `Type=string` for that trailing `variable.set` because the catalog can't tell it that `file.exists` returns a `path`. At runtime the variable then carries a `path` value with a `string` type tag — and downstream property access (`%info.Exists%`, `%info.Extension%`) misbehaves.

The fix shape we want:

1. The catalog row for `file.exists` includes `→ returns path` — derived statically from the action's `Run()` return type, not declared separately.
2. The Compile.llm rule becomes: "the trailing `variable.set` Type MUST match the prior action's `→ returns T` from the catalog. No exceptions, no fallbacks."
3. `goal.getTypes` (the per-step variables-in-scope walker) picks up the same info for free — every `%var%` carries a real type, never `object`.

## Why not `[Returns(typeof(T))]`

I sketched a `[Returns(typeof(path))]` attribute first. Ingi rejected it: any time the Run() body's actual return shape changes, the attribute would silently drift out of sync. We'd be back in "LLM was told a lie, .pr looks fine, runtime explodes later" territory — same class of bug we've been hand-patching for two weeks.

Method signature == truth. Compiler enforces it. That's the property we want.

## Concrete: what `file.exists` looks like today vs. after

**Today** (`PLang/app/modules/file/exists.cs`):

```csharp
public partial class Exists : IContext
{
    public partial data.@this<path> Path { get; init; }
    public Task<data.@this> Run() => Task.FromResult<data.@this>(Path);
}
```

`Task<data.@this>` — bare `Data`. Reflection cannot see that the runtime value is a `path`.

**After:**

```csharp
public partial class Exists : IContext
{
    public partial data.@this<path> Path { get; init; }
    public Task<data.@this<path>> Run() => Task.FromResult(Path);
}
```

Reflection at catalog-build time reads `Task<Data<path>>` → surfaces `path` as the action's return type. The `<data.@this>` cast (which was throwing the info away) goes away.

## The Data.FromError problem

The reason most actions today return bare `Task<Data>` is the error path. Action handlers do things like:

```csharp
return Task.FromResult(global::app.data.@this.FromError(err));
```

`Data.FromError(...)` returns base `Data`. If the Run() signature is `Task<Data<path>>`, the error-path return doesn't compile.

The clean fix: `Data.FromError<T>(error) → Data<T>`. Same for any other static-factory entry point that creates "empty / error" Data instances. Then the action's error path becomes:

```csharp
return Task.FromResult(global::app.data.@this<path>.FromError(err));
// or with a generic helper:
return Task.FromResult(data.@this.FromError<path>(err));
```

Pick whichever ergonomics read best; the constraint is just "every entry point that produces a Data must have a typed variant so handlers can use it without losing T".

## Scope

- 69 action classes currently return `Task<data.@this>`. 1 already returns `Task<Data<T>>`. The migration is mechanical: pick the T, change the signature, ensure error-path factories have a typed variant.
- For actions that are genuinely polymorphic (rare — generic helpers that bounce input back as output), `Task<Data<object>>` is the explicit declaration of polymorphism. There's no `T` = "unknown"; either the action has a stable return shape or it explicitly returns `object`.

## What changes on the builder/LLM side once C# is done

These are the builder bot's follow-ups, listed here so coder can see the whole picture:

1. `Modules.Describe()` already has `DescribeReturnType` that reflects `Run().ReturnType`. It currently bails out for bare `Data` returning `null`. After the migration, every action gets a non-null typed return, so this path activates without changes.
2. `summary.md` template (the compiler's catalog rendering) gains one line per action: `→ returns <T>`.
3. `Compile.llm` gets a rule: trailing `variable.set` after a producer action MUST carry `Type=<the producer's → returns T>`. The "default Type=object for %var% references" rule we added recently goes away — every reference now has a concrete type.
4. `goal.getTypes` already does the same reflection in its `DetermineReturnType` helper, with the same bare-Data → "object" fallback. After migration, the fallback disappears; per-step variables-in-scope all carry real types.

## Out of scope

- Whether to make the catalog generator fail-build when an action's Run() return type is unresolvable. Useful eventually, but lower priority than getting the 69 actions migrated.
- Whether/how to surface the return type's properties (e.g. `path.Exists`, `path.Extension`) in the LLM prompt. Separate question.

## Suggested ordering

1. Add typed factory variants on `Data` (`FromError<T>`, plus any others handlers actually use).
2. Migrate one canonical action end-to-end as a model — `file.exists` is a good pick since it triggered the conversation. Verify the catalog now renders `→ returns path` and Tests/Simple still builds.
3. Migrate the remaining 68 actions in a single mechanical pass. Per action: pick T (read the body to identify what's actually being returned on the success path), change the signature, update any FromError sites.
4. Hand the resulting `Task<Data<T>>` everywhere over to the builder bot. We'll add the catalog rendering + Compile.llm rule + drop the "Type=object default" teaching.

Treat this as a structural improvement, not a one-bug fix — once it's done, the entire "LLM picks the wrong Type for variable.set after an action" class of regression goes away by construction.
