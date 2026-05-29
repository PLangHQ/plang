# Nullability — `app` and `context` are never null

The invariant: `app.@this` and `actor.context.@this` are non-null at runtime. Every defensive `?.` on them and every static fallback that exists only because they *might* be null comes out. A null-ref after this is a **stamping bug** — fix it where the stamp was missed, not by guarding the read.

Doing this surfaces real bugs. Some `data` is read for its `.Type`/`.Kind` before the pipeline stamped its context; today that silently takes the static fallback branch, after it throws. That throw is the bug, now visible at its site. Expect a handful — that is the work, and it is good work.

## Post-merge findings (plang-types)

The merge confirmed three things that make this stage *easier* than the original draft assumed:

- **`module.@this` always has an App** (Ingi). So the `App?.Types.GetTypeName(...) ?? @this.GetTypeNameStatic(...)` fallbacks at `modules/this.cs:308,473,506` and the direct `GetTypeNameStatic` at `modules/goal/getTypes.cs:172` are dead defensiveness — they come **out clean** once `module.@this.App` is non-null, with no behavior to preserve.
- **The only context-free mint left is deserialization, and it's already plumbed.** The `type` entity is minted context-free in `data/Json.cs` (`new type(value)` during System.Text.Json read) — but `Data`'s `Context` setter propagates into it: `data/this.cs:144` → `if (_type != null) _type.Context = value;`. So a deserialized `type` gets its context the moment its owning `Data` is stamped. The invariant Stage 2 needs is just *"stamp the `Data` before reading `.Type.ClrType`"* — the plumbing to honor it exists. A read-before-stamp becomes a visible throw, exactly as intended.
- **Newtonsoft is gone**, so `data/Converter.cs` (the `[TypeConverter]` on `type`) is dead — delete it with the Stage 4 entity move; it has no bearing on the non-null work.

Net: the static fallbacks all come out, and the deserialization path holds the invariant via the existing `Context`-setter propagation rather than via a guard.

> **Line numbers below are pre-merge** and drifted with plang-types. Treat them as locators, not coordinates — the coder re-finds each site. The shape of the work is unchanged.

## Nullable App back-references → make non-null

| File:line | Declaration | Action |
|---|---|---|
| `goals/goal/this.cs:178` | `public app.@this? App { get; set; }` | → non-null (`= null!`, late-set) |
| `modules/this.cs:20` | `public global::app.@this? App { get; internal set; }` | → non-null (`= null!`) |
| `errors/Error.cs:44` | `internal global::app.@this? App { get; set; }` | → non-null (`= null!`) |
| `this.cs:81` | `public app.@this? Parent { get; set; }` | **STAYS nullable** — the root app legitimately has no parent |

Contrast (already correct, no change): `actor/context/this.cs:38`, `actor/this.cs:46`, `tester/this.cs:47` are non-null get-only; `goals/this.cs:24` already uses `= null!`.

## Nullable Context fields → make non-null

Nine declarations across the tree:

| File:line | Declaring type |
|---|---|
| `data/this.cs:23` (`Context`), `:89` (`_context`), `:138` (`Context` prop) | `data` + nested `type` |
| `variables/this.cs:25` (`_context`), `:58` (`Context`) | `variable` |
| `errors/Error.cs:80` | `Error` |
| `types/path/this.JsonConverter.cs:26`, `types/path/this.cs:75` | `path` |
| `goals/goal/steps/step/this.cs:16` | `step` |

`data`'s context is the load-bearing one — it's the late-bound holder (`Context { get; internal set; }`, inherited from parent at `data/this.cs:210`). Make it `= null!` and treat any read-before-stamp as the bug to fix at the producer.

## Defensive sites to remove (~39)

- **`App?.` → `App.`** — 15 sites (e.g. `data/this.Navigation.cs:271`, `builder/Types/this.cs:113-114`, `builder/Types/Render.cs:181`, `modules/debug/this.cs:493,523,529,589`, `modules/this.cs` schema sites).
- **`Context?.` → `Context.`** — 18 sites (e.g. `data/this.cs:30,35,40`, `data/Wire.cs:161,361`, `types/path/this.Authorize.cs:33,110`, `modules/variable/set.cs:29`, `modules/settings/Sqlite.cs:320`).
- **`ctx?.` → `context.`** — 6 sites (`data/this.cs:489,517,604,629`, `errors/Error.cs:292,295`). While here, **rename the local `ctx` → `context` everywhere** — one name across the codebase (214 identifiers in 36 files). Mechanical, folded into this pass (Ingi's call).

Note: some of these chain into properties that are themselves legitimately nullable (e.g. `Context.App.Debug?.MaxLength` — `Debug` may be off). Only strip the `?` on `App`/`Context`/`ctx`; leave `?` on genuinely-optional downstream members. The coder reads each site, doesn't blanket-replace.

## Static fallbacks

| Symbol | Disposition |
|---|---|
| `GetPrimitiveOrMime` — 4 external call sites (`data/this.cs:30,489`, `modules/variable/set.cs:30`, `modules/settings/Sqlite.cs:321`) | The `?? GetPrimitiveOrMime(...)` fallbacks come **out** — once context is non-null, `context.app.type[Value]` resolves primitives too (the registry holds them). |
| `GetTypeNameStatic` — external fallbacks at `modules/this.cs:308,473,506` (`App?.Types... ?? GetTypeNameStatic`) | Come **out** — `App` is non-null, so the `App.Types.GetTypeName(...)` (→ `app.type[...].name` after the accessor/entity work) is enough. |
| `getTypes.cs:172` — direct `GetTypeNameStatic(returnType)` | Route through `App.Types` — `module.@this` always has an App (Ingi confirmed), so the "no app in scope" justification is false; the static call is unnecessary. |
| `GetTypeNameStatic` — recursive internal calls inside `types/this.cs` (the `GetTypeNameStatic` body) | **Stay** for now — they are the static method's own implementation. Stage 4's entity work may fold them onto `type.@this`; until then, leave the static method intact and only remove the *external nullable fallbacks*. |
| `GetPrimitiveOrMime` / `GetPrimitiveName` static surface (`types/this.cs`) | **Stay as a method**, but the *external `?? ...` fallbacks* come out. The static path still backs the no-context fallback the static method itself documents; Stage 2 only removes the callers that lean on it because `App`/`Context` *might* be null. |

## Other over-nullable back-references (checked, per Ingi's ask)

Beyond `App` and `Context`, the same late-set-but-never-null smell appears on these structural back-references. Each is set by a parent/registrar and is non-null in normal operation — flip them too:

| File:line | Back-ref | Disposition |
|---|---|---|
| `goals/goal/steps/this.cs:18` | `steps → goal.@this? Goal` | non-null — steps always belong to a goal |
| `goals/goal/steps/step/this.cs:121` | `step → goal.@this? Goal` | non-null — same |
| `channels/channel/this.cs:72` | `channel → actor.@this? Actor` | non-null once registered |
| `channels/channel/this.cs:80` | `channel → channels.@this? Channels` | non-null once registered |
| `channels/this.cs:34` | `channels → actor.@this? Actor` | non-null once owned by an actor |

Judgment-needed — do **not** blanket-flip; these may be legitimately optional by lifecycle:

| File:line | Back-ref | Why hold |
|---|---|---|
| `goals/goal/GoalCall.cs:36` | `GoalCall → action.@this? Action` | init-only; a call may exist before it's bound to an action — confirm |
| `modules/IEvent.cs:23` | `IEvent → step.@this? Step` | init-only; an event binding may have no step — confirm |

The rule is the same as App/Context: never-null-in-runtime → make it non-null and let a null-ref be the bug; null during a real transient (pre-registration, unbound call) → stays nullable. The coder confirms each against its lifecycle. The structural five above are the clear wins. (`app.Parent` is the explicit *legitimate* nullable — the root app has no parent.)

## Interaction with the other pieces

- This pass is independent of the rename's *folder* moves, but it touches `types/this.cs` and `data/this.cs` which the rename also renames — sequence it **after** the rename so namespaces are stable, **before** the accessor reshape so the accessor work doesn't have to thread `App?.`.
- `data.Type => context.app.type[Value]` (the clean one-line form) lands fully with the type-entity work when `type[...]` returns the entity; in this pass the form is `data.Type => context.App.Types.Clr(Value)` with the `?.` and fallback removed. The non-null invariant is the prerequisite; the entity is the payoff.
