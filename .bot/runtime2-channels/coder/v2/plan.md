# v2 — `[Choices]` standardization + unblock channel test builds

## Problem (high level)

Build-time validator rejects `Actor="system"` literals on every `channel.*`
action with a contradictory error: *"cannot be converted to type 'actor'.
Valid values: user, system."* — i.e. `system` is in the valid list but the
validator's conversion path doesn't use the list, it tries to *construct*
an `Actor` object from the string and fails.

The deeper issue: there are two separate concerns being conflated.

- **Vocabulary** — what strings the LLM may emit for a slot. Build-time concern.
- **Resolution** — how the runtime turns the string into a usable object.

Right now they share one half-formed convention (`static ValidValues` +
`IObject` interface). `Operator` fits because it's a value wrapper.
`Actor` doesn't — it's a stateful runtime object that only `App.GetActor`
can produce, but the validator doesn't know that.

## Design

One declarative convention, used by everything that has a closed set of
strings the LLM picks from: a `[Choices]`-attributed static method on the
type, returning `string[]` and taking `Context` (so dynamic vocabularies
like channel names work the same as static ones).

```csharp
public sealed class @this   // Actor
{
    [Choices]
    public static string[] Choices(Actor.Context.@this ctx) => ["user", "system"];
}
```

Built once at startup into a `Type → Choices(Context)` registry.
`Describe()` uses it to inline the vocabulary into the LLM prompt.
`validateResponse` uses it to membership-check what the LLM emitted.

Resolution stays where it lives — `App.GetActor(name)` for Actor, the
operator registry for Operator. **The language layer only cares about the
vocabulary, not how each type materializes from its name.**

## Implementation order

1. **Add `[Choices]` attribute + registry.** New file
   `PLang/App/Choices/this.cs` with `@this.Get(Type, Context)` returning
   `string[]?`. Reflection scan deferred to first call (cheap, cached).
2. **Migrate Actor + Operator.** Replace `static ValidValues` with
   `[Choices] static Choices(Context)`. Drop the `IObject` interface
   entirely (only Operator implements it). Remove `IObject.cs`. Adjust
   `TypeConverter.IObject branch` accordingly — Operator still has its
   string ctor, the existing "single string ctor with optional rest"
   branch covers it.
3. **Wire `Describe()` + `validateResponse`.** Both go through the
   Choices registry. `TypeMapping.GetValidValues` becomes a thin shim
   over the registry (or gets renamed) so existing callers
   (`DefaultEvaluator` fix-suggestions, `Types.this.ValidValues`)
   continue working.
4. **Update tests** — the existing
   `ValidValues_ContainsAllOperators` /
   `ActorValidValues_DropsToUserAndSystem` /
   `GetValidValues_DataOfActor_ReturnsValues` tests are written against
   the old surface; they get updated to call the new convention.
5. **Initialize `Tests/Channels/`.** `plang '--app={"create":true}' build`
   from the directory. Verify per-scenario `.pr` files. Run
   `plang --test`. Stale should drop 18 → 4.

## Out of scope

- Channel names as a parameter type (future use of `[Choices]` for
  dynamic vocabularies).
- Step-splitting noise the LLM occasionally produces — separate concern
  from validation.
- Cross-device migration transport.

## Tests

- C# baseline 2745 → must stay 2745+ after migration. New tests added
  for the Choices registry.
- PLang stale 18 → 4 (the 4 pre-existing Callback stales aren't this
  branch's scope).
