# Delete Step.Context

## The trace — why it exists

`Step.Context` (`goal/steps/step/this.cs:16`, `[JsonIgnore] public actor.context.@this? Context`) has exactly one behavioral consumer: `Step.Disabled` (`:24-38`).

```
public bool Disabled
{
    get { if (Context == null) return false; return Context.Get<bool>(DisabledKey); }
    set { if (value) Context?.Set(DisabledKey, true); else Context?.Set<bool>(DisabledKey, default); }
}
private string DisabledKey => $"step:{Goal?.PrPath}:{Index}:disabled";
```

`Disabled` stores per-execution disabled state in the running context's data bag, keyed by `(PrPath, Index)`, "so concurrent executions don't interfere." `Step.Context` is just the handle it uses to reach that bag. A Step is a shared build entity — the same Step runs under a different context on every request — so AnchorScope swaps the handle to the running context each dispatch:

- `context/this.cs:278` — `Step.Context = this` (set to the running context).
- `context/this.cs:299` — `_previousStepContext = action.Step?.Context` (save the prior value).
- `context/this.cs:307` — restore on Dispose.

That save/restore at `:299` is the *only* non-`Disabled` read of the field anywhere — and it reads the field only to restore the field. The build-time stamps at `steps/this.cs:53,128` (`step.Context = Context`) write it ahead of the `Disabled` reads on the next lines.

## The smell

Every caller of `Disabled` already holds the context as a local, one line above:

```
// steps/this.cs:53-58
step.Context = Context;          // stash the context on the shared step…
if (step.Disabled)               // …only to read it straight back off the bag
    step.Disabled = false;

// steps/this.cs:128-129
_items[i].Context = context;     // context is right here as a local
_items[i].Disabled = disabled;
```

The field smuggles a context the caller already has. AnchorScope then carries the cost of keeping that smuggled pointer correct per dispatch. The disabled state is genuinely `(step-identity, context) → bool`; it should be reached by passing the context, not by mutating a back-pointer on a shared object.

## The fix — delete the field, parameterize Disabled

- `Disabled` becomes context-parameterized — e.g. `step.IsDisabled(context)` / `step.SetDisabled(context, value)`. Same key (`step:{PrPath}:{Index}:disabled`), same bag; the step receives the context instead of holding it.
- `Step.Context` field is removed.
- AnchorScope keeps setting `context.Step = action.Step` (the current-step pointer behind `%!step%`) but drops the `Step.Context` set at `:278`, the `_previousStepContext` capture at `:299`, and its restore at `:307`.
- The `step.Context = …` stamps at `steps/this.cs:53,128` disappear; the `Disabled` calls there pass the local `Context`/`context`.

This is the third option from the design conversation — neither "source from owning actor context" nor "carve out as nullable." `Step.Context` is removed, so it is not a `Context?` field to flip at all. It also lines up with the invariant: the disabled check runs during execution, the running context is in hand, so pass it.

## Watch for

`AnchorScope` also sets `context.Goal` and `context.Event` and anchors `Step.Context` for parallel-dispatch safety. Only the `Step.Context` strand is removed; `context.Step`/`Goal`/`Event` save/restore stay. Confirm no other reader assumes `action.Step.Context` is populated (the trace says none does, but re-grep after the field is gone).

## You own the final shape

`step.IsDisabled(context)` is the intended shape; the coder picks the method names and whether the disabled state reads more naturally on `Step` or on the steps collection. The contract is: `Step` no longer holds a `Context` field.
