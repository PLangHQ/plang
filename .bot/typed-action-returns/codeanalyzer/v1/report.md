# codeanalyzer v1 — typed-action-returns Stage 0

Six coder commits land Stage 0 of the typed-action-returns branch:
infrastructure for per-action `Build()`, named channels with no-op fallback,
slimmed `[PlangType]` attribute, and a public `Data.As(string)` materializer.
30/30 Stage 0 tests green per handoff.

This review covers the diff `c4404b9c5..3c8285760`. The work is mostly clean —
the new pieces are small, well-shaped, and earn their place. Findings below are
LOW / MEDIUM polish; no blocker.

---

## Pass 1a — OBP rule violations

No violations.

- `IClass` is a single role contract (Build + SetAction).
- `noop/this.cs` is a concrete subclass of `channel.@this` — owns its own three Cores, no leakage.
- `builder.warning.@this` is a sealed record carrying `(IClass Action, string Message)` — payload only, no behaviour.

## Pass 1b — Shape smells

Run the four-item checklist against the new code:

| # | Smell | Hit? |
|---|---|---|
| 1 | Public mutable collection with rules enforced from outside | **No** (no new collection-typed fields) |
| 2 | Cross-file `lock(other.X)` | **No** |
| 3 | Same logical thing stored twice across types | **No** |
| 4 | Allocate-here / mutate-there / clean-up-elsewhere | **No** |

`StampOnTerminalVariableSet` mutates `action.Parameters` from `Default.Validate` — that is the existing validate-pass mutation pattern (`NormalizeParameterTypes`, `ResolveGoalCallsInAction`), not a new smell. The action object already owns its parameter list as a public `List<data.@this>`; whether *that* baseline is OBP-shaped is a branch-orthogonal question.

## Pass 2 — Simplification

### `PLang/app/types/Registry.cs:126–144` — dead-code loop after attribute slim. **LOW**

`PlangTypeAttribute` now declares `AllowMultiple = false` (PlangTypeAttribute.cs:30). But Registry.cs still treats multiple attrs as aliases:

```csharp
// Registry.cs:13–14
///   1. [PlangType("name")] on the class — declared name wins. Multiple
///      [PlangType] attributes act as aliases; the first non-null Name is canonical.
```

```csharp
// Registry.cs:126, 136-144
var attrs = type.GetCustomAttributes<PlangTypeAttribute>(inherit: false).ToList();
...
if (attrs.Count > 0)
{
    foreach (var attr in attrs)   // ← loop iterates at most once
    {
        var name = attr.Name ?? InferName(type);
        if (name == null) continue;
        _nameToType.TryAdd(name, type);
        canonical ??= name;
    }
}
```

The for-loop is dead — `AllowMultiple = false` means `attrs.Count ≤ 1`. Either drop the loop (one attribute = one branch) and the stale doc, or restore `AllowMultiple = true` if aliases were intentionally kept on the table. Pick one — the contradiction will cost the next reader time.

### `PLang/app/modules/builder/code/Default.cs:543` — discarded error. **LOW**

```csharp
var (handler, err) = modules.GetCodeGenerated(a);
if (handler is not modules.IClass classified) continue;
```

`err` is unused. Validate's earlier `modules.Contains(a.Module, a.ActionName)` guard guarantees `err == null` here, but the variable still bleeds the IDE warning. Pattern is `(handler, _)`.

### `PLang/app/modules/IClass.cs:22–23` — fully-qualified Task. **LOW**

```csharp
System.Threading.Tasks.Task<data.@this> Build()
    => System.Threading.Tasks.Task.FromResult(data.@this.Ok());
```

A `using System.Threading.Tasks;` would let this read `Task<data.@this> Build() => Task.FromResult(data.@this.Ok())`. Every neighbouring file in `app/modules/` uses the short form. Pure readability.

## Pass 3 — Readability

### `PLang/app/modules/builder/code/Default.cs:564–580` — inconsistent stamp shape. **LOW**

`StampOnTerminalVariableSet` has two branches:

```csharp
var existing = a.Parameters.FirstOrDefault(p =>
    string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
if (existing != null)
    existing.Value = typeName;                              // replace path: Value only
else
    a.Parameters.Add(new data.@this("Type", typeName)
        { Type = new data.type("string") });                // insert path: Value + Type
```

The replace path leaves `existing.Type` untouched; the insert path sets it to `"string"`. If the existing Type slot had a different `Type` (e.g. an LLM-emitted typo or schema drift), it survives. In practice the LLM and the schema always type the `Type` slot as `string`, so this isn't a bug today — but it's an unexplained asymmetry. Either set `existing.Type = new data.type("string")` in the replace branch too, or doc the assumption ("Type's own type-slot is always `string`; we only refresh Value").

### `PLang/app/channels/this.cs:111–114` — name vs. method collision. **LOW**

```csharp
public channel.@this Channel(string name)
    => _channels.TryGetValue(name, out var channel) ? channel : NoOp;
```

Local `channel` shadows the namespace alias / parameter slot expectation. Compiles, but `out var ch` reads with less squinting. Trivial.

### `PLang/app/data/this.cs:471–488` — `As(string)` drops Properties/events. **LOW**

The new public materializer constructs a fresh Data without aliasing `Properties` or the three event lists from `this`:

```csharp
return new @this(Name, converted, new type(typeName), Parent) { Context = ctx };
```

Every other type-converting path in this file routes through `ConstructWrap`, which aliases Properties + OnCreate/OnChange/OnDelete by ref. `As(string)` diverges. Likely intentional — explicit cross-type coercion is a "fresh start" semantically — but the XML doc doesn't call it out. A one-liner clarifying "does not propagate subscribers/properties; if you need identity, use the `.Value`/`As<T>` path" would close the surprise.

## Pass 4 — Behavioral reasoning

### `IClass.SetAction` — required by interface, only provided by source generator. **LOW (design choice)**

```csharp
void SetAction(
    global::app.goals.goal.steps.step.actions.action.@this action,
    global::app.actor.context.@this context);
```

`Build()` carries a default body; `SetAction` doesn't. Every `IClass` implementation must provide it. Production handlers get it from `EmitSetAction`. A hand-rolled `IClass` (test fixture without the generator) would have to author SetAction by hand — and almost any such implementation would just stash the args or no-op.

Stage 0's five test handlers (`Handlers.cs`) live in `app.modules.typedreturns.*` so the generator picks them up, so it works today. But the moment someone writes a non-generated IClass for a unit test, the contract bites. Consider a default `void SetAction(...) { }` on the interface — the override on the generator side stays the source of truth for property-resolution-state plumbing; the default just unblocks the test-only case.

Alternatively, doc the requirement on the interface: "implementations not produced by `PLang.Generators` must implement SetAction (typically a no-op)."

### `EmitSetAction` skips `IChannel` / `IEvent` / `[Code]` / `[IsNotNull]`

The comment above `EmitSetAction` lists what's intentionally omitted vs. `ExecuteAsync`. A handler that overrides `Build()` and tries to read its `Channel` capability would NRE (Channel field still default-null from a prior Run). This is documented and probably correct — Build() is compile-time, has no actor/channels — but the consequence is that the kinds of Build() bodies that can legitimately do work are narrow (read declared parameters, return Type). If a future Build() body grows to need the channel (e.g. to write a BuildWarning), the wiring story will need to change. **No action — flag for future Stage 4 builders writing warnings.**

### Build() output channel for `BuildWarning` is unwired in this PR

`builder.warning.@this` exists; the noop fallback exists; `channels.Channel(name)` returns it on miss. But there's no Stage 0 production code that writes a BuildWarning anywhere — the wiring lands later. So today, a Build() body that calls `Context.App.User.Channels.Channel("builder").WriteAsync(new BuildWarning(this, "msg"))` would hit the noop sink (because Build() runs from `RunBuildPass`, which executes outside the `system/builder/*.goal` PLang flow that registers the "builder" channel). The end-to-end path is deferred per handoff §5. **Not a finding — noted for the next reviewer.**

### `StampOnTerminalVariableSet` silent no-op when no `variable.set`

If `Build()` returns `Ok(typeName)` for a step that has no terminal `variable.set` (e.g. an output.write step), the stamp silently drops. This matches the architect's spec — type hint only applies when there's a write target. **No action.**

## Pass 5 — Deletion test

Walked through the new lines:

- `IClass.cs:22–34` — both methods reachable from tests; deleting either fails Stage 0.
- `noop/this.cs:23–34` — all three Cores invoked from `Channels.Channel` callers.
- `channels/this.cs:111–114` — covered by Stage0_NamedChannelsTests.
- `builder/code/Default.cs:519–580` — covered by Stage0_BuildMethodTests (5 paths).
- `data/this.cs:471–488` (`As(string)`) — covered by Stage0_DataMaterializationTests.
- `EmitSetAction` — required by `RunBuildPass`.

One small dead-code candidate: the `foreach (var attr in attrs)` loop in `Registry.cs:138` runs ≤1 iteration due to `AllowMultiple = false`. Flagged under Pass 2.

## Test code observations (out-of-scope but worth a glance)

### `PLang.Tests/App/TypedReturnsTests/Handlers.cs:49` — shared mutable test state. **LOW**

```csharp
public static readonly List<string> InvocationLog = new();
```

`BuildOrdered.InvocationLog` is a static across all tests. The `Setup` clears it, so single-threaded sequential runs are fine. If TUnit parallelises tests in this class in the future, the assertion at `Stage0_BuildMethodTests.cs:80` races. Either confine to a fresh handler per test, or document the serial assumption.

## Verdict

**NEEDS WORK — LOW-severity polish only.**

- One real cleanup: the `[PlangType]` `AllowMultiple = false` vs. multi-attr loop contradiction. Pick a story (Registry.cs:126–144 + Registry.cs:13–14 vs. PlangTypeAttribute.cs:30).
- Five minor polishes (discarded `err`, fully-qualified Task, Stamp asymmetry, name shadowing in `Channel`, `As(string)` Properties/event aliasing).
- One design call to surface: `IClass.SetAction` requires hand-coded impls — default to no-op or doc the rule.

None of these block Stage 1 work. They can all land as a follow-up commit, or be folded into the next coder pass that touches these files.
