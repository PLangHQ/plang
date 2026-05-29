# Nullability — `app` and `context` are never null

The invariant: `app.@this` and `actor.context.@this` are non-null at runtime. Every defensive `?.` on them and every static fallback that exists only because they *might* be null comes out. A null-ref after this is a **stamping bug** — fix it where the stamp was missed, not by guarding the read.

Doing this surfaces real bugs. Some `data` is read for its `.Type`/`.Kind` before the pipeline stamped its context; today that silently takes the static fallback branch, after it throws. That throw is the bug, now visible at its site. Expect a handful — that is the work, and it is good work.

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
- **`ctx?.` → `ctx.`** — 6 sites (`data/this.cs:489,517,604,629`, `errors/Error.cs:292,295`).

Note: some of these chain into properties that are themselves legitimately nullable (e.g. `Context.App.Debug?.MaxLength` — `Debug` may be off). Only strip the `?` on `App`/`Context`/`ctx`; leave `?` on genuinely-optional downstream members. The coder reads each site, doesn't blanket-replace.

## Static fallbacks

| Symbol | Disposition |
|---|---|
| `GetPrimitiveOrMime` — 4 external call sites (`data/this.cs:30,489`, `modules/variable/set.cs:30`, `modules/settings/Sqlite.cs:321`) | The `?? GetPrimitiveOrMime(...)` fallbacks come **out** — once context is non-null, `context.app.type[Value]` resolves primitives too (the registry holds them). |
| `GetTypeNameStatic` — external fallbacks at `modules/this.cs:308,473,506` (`App?.Types... ?? GetTypeNameStatic`) | Come **out** — `App` is non-null, so the `App.Types.GetTypeName(...)` (→ `app.type[...].name` after the accessor/entity work) is enough. |
| `getTypes.cs:172` — direct `GetTypeNameStatic(returnType)` (no app in scope) | Route through app instead — confirm app is reachable here. |
| `GetTypeNameStatic` — recursive internal calls inside `types/this.cs:133-166` | **Stay** for now — they are the static method's own implementation. The type-entity work may fold them into the entity; until then, leave the static method intact, only remove the *external nullable fallbacks*. |

## Interaction with the other pieces

- This pass is independent of the rename's *folder* moves, but it touches `types/this.cs` and `data/this.cs` which the rename also renames — sequence it **after** the rename so namespaces are stable, **before** the accessor reshape so the accessor work doesn't have to thread `App?.`.
- `data.Type => context.app.type[Value]` (the clean one-line form) lands fully with the type-entity work when `type[...]` returns the entity; in this pass the form is `data.Type => context.App.Types.Clr(Value)` with the `?.` and fallback removed. The non-null invariant is the prerequisite; the entity is the payoff.
