# to architect — area-1b `Info → Warning` / `error.list`: three decisions before I build

Branch `goal-graph-singular`. Picking up the plan's **area-1b property promotions**. The two clean
renames landed (`goal.Goals → Child` D4, `action.Modifiers → Modifier` — both wire-neutral, pushed
`5c571b9e0`). The remaining 1b piece is the plan line:

> `step.Error`/`action.Error` as `error.list` (`Add(IError)`, callers hand the error they hold — no
> flattening); `step.Warning`/`action.Warning` as `warning.list` (**`Info` replaced by `Warning`**);
> shared error/warning face = an interface only if a uniform consumer exists — coder checks.

I traced it before touching code. The trace changes the picture enough that I want your ruling on
three forks rather than guessing.

## What the trace found

**1. `Info` today** — a lossy `{Key, Message}` pair (`app/Info.cs`), `[PlangType]`, used for BOTH
errors and warnings:

```csharp
public sealed class Info { [LlmBuilder] string Key; [LlmBuilder] string Message; }
```

**2. Graph `.Errors` is VESTIGIAL.** `goal/step/action` each carry `[Debug] List<Info> Errors` — but
nothing ever populates them. The only sites:
- `step.Merge`: `Errors.Clear(); Errors.AddRange(from.Errors)` (empty → empty)
- `CallChainRenderer`: `head.Errors.Count == 0` (always true — the node never carried errors)

No `.Errors.Add(...)` on any graph node, anywhere. So "promote to `error.list<IError>`" would be
typing an always-empty slot.

**3. Graph `.Warnings` is the REAL channel.** Populated with build diagnostics:
- `step/this.cs:81` `Warnings.Add(new Info{...})`
- `build/code/Default.cs:503/512/596` `a.Warnings.Add(new Info{...})`
- carried through `Merge`.

**4. Naming collision.** `error.list` already exists — `app.error.list.@this` — but it's **App-scoped**
(instance `AsyncLocal<IError?> _current`, run-wide `Trail`, holds `app.@this _app`; this is `App.Errors`,
the `%!error%` scope). A *per-node* error collection is a fundamentally different thing. Reusing the
name/namespace would conflate two concepts. (`error.trail.@this` is the run-wide `IReadOnlyList<IError>`
with freeze semantics — also not a per-node bag.)

**5. Shared face — checked, answer is NO.** The only consumers of `.Errors`/`.Warnings` read `.Count`
(`CallChainRenderer`, `Merge`). No uniform *behavioral* consumer → no shared `IDiagnostic` interface;
two independent types (your "coder checks, doesn't pre-build").

**6. Other `List<Info>` holders** (outside the graph): `Data.Result.Warnings` (`List<Info>?`),
`BuildResponse.Errors`/`Warnings` (`List<Info>?`), plus local `List<Info>` accumulators in `build.code`.
`BuildResponse` is a wire shape — converting it changes bytes.

## The three decisions

### D-A — graph `.Errors` (vestigial): delete or promote?
- **Delete** (my lean): remove `goal/step/action.Errors`; fix the ~4 `.Count == 0` read sites
  (`CallChainRenderer`, `Merge`). No new per-node error type, no collision with `app.error.list`.
  Warnings become the node's only diagnostic channel. Faithful to reality (the slot is dead).
- **Promote**: keep `.Error` as a per-node `list<IError>`. Needs a NEW type under `app.goal.*` (name
  ≠ `error.list`, which is taken). More faithful to the plan *text*, but types an always-empty slot
  and adds a type mainly to sidestep the name clash.

### D-B — where does `warning` (renamed `Info`) live, and its collection shape?
`Info` is used by graph nodes + `Data.Result` + `BuildResponse` — a general diagnostic, not a
graph-only concept. Proposal: `app/warning/this.cs` (`warning.@this`, the `{Key, Message}` pair) +
`app/warning/list/this.cs` (`warning.list.@this`). `.Warning` on the node = `warning.list`. OK, or do
you want it homed under `app.goal.*`?

### D-C — scope of this pass
- **Graph-only**: convert the 6 graph slots; leave `Data.Result.Warnings` + `BuildResponse.*` as
  `List<Info>` for now → `Info` survives until a later pass. Smaller, avoids the `build.code`/recovery
  blast radius (that area is already blocked on the recovery round-trip).
- **All at once**: convert everything, DELETE `Info` (the demolition-list item). Bigger — touches
  `build.code` and changes `BuildResponse`'s wire bytes.

## My recommendation (for a fast ruling)
**D-A: delete** the vestigial graph `.Errors`. **D-B:** `app/warning/{this,list/this}.cs`. **D-C:
graph-only** now (Data.Result + BuildResponse ride a later pass so `Info` deletion doesn't drag
`build.code` into this while recovery is blocked). Net: `Info` shrinks to those two non-graph holders,
graph nodes carry only `warning.list Warning`, one grep-gate (`.Warnings`) clears, no name collision.

If you'd rather stay literal to the plan (promote `.Error` to hold `IError`), tell me the type
name/namespace and I'll build it — but flag whether an always-empty typed slot earns its keep.
