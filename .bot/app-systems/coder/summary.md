# coder — app-systems

**Version:** v3

## What this is
app-systems makes every `app.X` the type X (a generic `type<X>` over its concept's `list<X>`), so the
plang path, the C# path and the file path agree. The architect's plan
(`.bot/app-systems/architect/plan.md`) lays it out in 13 stages.
- v1: plan review and stage 0.
- v2: stage 1, `Start` as the entry verb.
- v3: stage 3, one set of types.

## What was done
- **v1, v2**: see `v1/result.md` and `v2/plan.md`; stage 1 is commit `8c135f519`.
- **v3, stage 3** (details and commits in `v3/result.md`):
  - The registry is one list of types. Each type owns its name, `Alias`, item class (`ClrType`), owned C# shapes and facts, and answers `Match(key)` (`IMatch<TSelf>`).
  - `Add` is the one way in, answering errors as results. Types are born through the list.
  - Kinds own their type. Number's precisions, hash's algorithms, choice's sets and path's schemes are all kinds; `kind.list(context)` lists a type's kinds.
  - `type.kind` is never null (the empty kind).
  - Results: the six suites are at or under the baseline, the prompt twins are byte-equal, and `plang --test` is unchanged.
  - Files: `PLang/app/type/{this.cs, list/this.cs, list/Registry.cs, kind/**, item/**}`, `code/load.cs`, `variable/set.cs`, `Formal.cs`, `data/this.cs`, plus ~90 test files.
- **Next:** stage 4, the collected type: `type.@this<T>`, `app.type` as `type<type>`, `item.history`, the strict `list<T>`, and the kind store moving under item.

## Code example
A type answers for itself, and the list walks it:
```csharp
// type/this.cs
public ValueTask<@this?> Match(string key) => new(Names(key) ? this : null);
internal bool Names(string key)
    => string.Equals(Name, key, StringComparison.OrdinalIgnoreCase)
       || Alias.Contains(key, StringComparer.OrdinalIgnoreCase);

// type/list/this.cs
public app.type.@this this[string name]
    => Array.Find(Types, t => t.Names(name))
       ?? throw new KeyNotFoundException($"No PLang type registered under name '{name}'.");
```
A kind declares the type it is a kind of:
```csharp
// type/item/number/kind/this.cs
protected internal override string Owner => "number";
```
