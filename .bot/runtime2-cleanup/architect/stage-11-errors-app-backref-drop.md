# Stage 11: `errors-app-backref-drop`

**Read first:**
- `plan/principles.md` — OBP discipline, especially the smells around back-refs (god-bag pattern, post-construction injection).
- `plan/scope-map.md` — `app.Errors` is shared (App-level); ctor-injection is the right shape for App-level subsystems that need to reach App.

**Goal:** Eliminate the post-construction injection `Errors.App = this;` at `App.this.cs:297`. `Errors.@this` takes App via constructor; the back-ref becomes a `private readonly` field set at construction. Same effect as today (Push uses App.CallStack and App.Variables); cleaner shape.

**Scope:**
- *Included:* add ctor `public @this(App.@this app)` to `Errors.@this`; drop the `internal App.@this? App { get; set; }` settable property in favor of a `private readonly` field; update `App.this.cs:165` from `Errors { get; } = new();` to `Errors { get; }` + ctor allocation `Errors = new(this);`; delete `App.this.cs:297`'s post-construction line.
- *Excluded:* the `Error.@this` back-ref (`e.App = App` inside Errors.Push, at line 63) — that's a separate concern. The plan one-liner mentioned "probably moves Error.Callback materialisation off Error itself so the back-ref isn't needed," but that's a deeper refactor (Error.Callback uses app.Snapshot()). Stage 11 does the smaller post-construction fix only; the Error.App back-ref stays for now and is a candidate for a future stage if anyone takes it on.

**Deliverables:**
- `PLang/App/Errors/this.cs`:
  - Add ctor: `public @this(App.@this app) { _app = app; }`.
  - Add private field: `private readonly App.@this _app;`.
  - Delete the `internal App.@this? App { get; set; }` property at line 27.
  - Update internal references inside Push: `App?.CallStack` → `_app.CallStack`; `App!.Variables` → `_app.Variables`; `App` (in `e.App = App`) → `_app`.
- `PLang/App/this.cs`:
  - Line 165: change `public global::App.Errors.@this Errors { get; } = new();` to `public global::App.Errors.@this Errors { get; }`.
  - Inside App's ctor (find the right partial — likely `App.this.cs` itself where line 297 is), add `Errors = new global::App.Errors.@this(this);` somewhere appropriate (after `this` is fully usable; the same place line 297 used to do the back-ref injection is reasonable).
  - Delete line 297: `Errors.App = this;`.
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. Independent of stage 12.

## Design

### The smell this closes

**Post-construction injection.** App's ctor allocates `Errors` with a parameterless ctor (field-init `new()`), then later in the same ctor flow does `Errors.App = this;` to wire the back-ref. That's two-step construction — Errors exists in a state where `App` is null until the line at 297 runs, and any code path that hits Errors before that line reads `App?.CallStack` returning null silently. Brittle.

The fix: pass App at construction. Errors goes from "constructed-then-fixed-up" to "constructed correctly" in one step.

### The new shape

**`Errors.@this`:**

```csharp
// Today (line 27):
internal App.@this? App { get; set; }

// After: deleted. Replaced by:
private readonly App.@this _app;

public @this(App.@this app) { _app = app; }
```

Internal usages inside `Push` (lines ~60–80):

```csharp
// Today:
if (error is Error e && e.App == null) e.App = App;
var stack = App?.CallStack;
// ...
stack.EnableDiffStream(App!.Variables);

// After:
if (error is Error e && e.App == null) e.App = _app;
var stack = _app.CallStack;
// ...
stack.EnableDiffStream(_app.Variables);
```

Note: `_app` is non-null (ctor enforced), so the `?` and `!` operators go away. The `App?.CallStack` becomes `_app.CallStack`. Cleaner null story.

**`App.this.cs`:**

```csharp
// Today (line 165):
public global::App.Errors.@this Errors { get; } = new();

// After:
public global::App.Errors.@this Errors { get; }

// Inside App's ctor (replacing line 297):
Errors = new global::App.Errors.@this(this);
```

The line 297 (`Errors.App = this;`) deletes; the `Errors = new(...)` allocation happens earlier in the same ctor.

### Files touched + caller propagation

**Files modified (2):**
- `PLang/App/Errors/this.cs` — add ctor, add private field, drop public property, update Push internals.
- `PLang/App/this.cs` — Errors property declaration; ctor allocation; line 297 deletion.

**Caller verification:**
- `Errors.App` external readers — grep `\.Errors\.App\b\|\bErrors\.App\b` outside `Errors/this.cs` itself. The earlier grep showed only line 297 of App.this.cs (the post-construction setter). Re-run after the change to confirm no external code reads `app.Errors.App`.

### Risk + dependencies

**Risk: very low.** Pure construction-shape change. Same data, same logic, just delivered via ctor instead of post-construction injection.

Possible failure modes:
- A reader of the now-deleted `Errors.App` property — caught by build break.
- Boot-order issue: Errors construction now happens later in App's ctor (at the line where 297 used to be) rather than at field-init time. If anything before that line in App's ctor tries to use `app.Errors`, it fails. Verify by reading App's ctor end-to-end. Most boot code uses Errors at runtime (not during App construction), so this should be fine.

**Dependencies: none.**

### Tests

**No new tests required.** Behavior unchanged.

**Existing test coverage to verify:**
- `PLang.Tests/App/Errors/` — error push/pop, Trail.
- `Tests/` — full PLang suite.

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -n "Errors\.App\s*=" PLang/App/this.cs` — zero hits (the post-construction setter line is gone).
- `grep -n "internal App\.@this\? App" PLang/App/Errors/this.cs` — zero hits.

### Watch for (coder eyes-on)

- **The `e.App = App` line** (Push, line 63 today) — the `e.App` is a *different* back-ref on `Error.@this` itself. Stage 11 only fixes the Errors-collection back-ref; Error's own App back-ref stays. If you find a way to drop both cleanly, flag for a future stage but don't expand stage 11's scope.
- **App's ctor call ordering** — Errors must be constructed before any code that uses `app.Errors`. If App's ctor has lines that reach `app.Errors.X` between field-init time and the new construction line, those break. Read App.this.cs's ctor end-to-end.
- **Other subsystems with the same post-construction-injection pattern** — while reading App.this.cs you may see other `Subsystem.App = this;` lines or similar. Flag for future stages but don't fix in stage 11.
- **`Errors` references in other partials of App** — App is `partial`. Other partials may construct or reach Errors during their own bootstrapping. Verify.

### Stages that follow this one

- **Stage 12** (`build-branch-to-build-this`) — same Tier 3 batch; independent (different concern).
- A potential future stage to drop the `Error.@this.App` back-ref (would require moving Error.Callback materialisation) — not in this plan.

### Out of scope

- Error.@this's own `App` property — separate stage if anyone takes it on.
- The diff-flipping logic in Errors.Push — stays as today; just reads from `_app` instead of `App?`.

## Commit plan

```
runtime2-cleanup stage 11: Errors takes App at construction

App.this.cs:297 had `Errors.App = this;` — post-construction injection
of the App back-ref into Errors.@this. The Errors property was
allocated with parameterless `new()` at field-init time; the back-ref
got patched in mid-ctor. Two-step construction with a window where
`Errors.App` was null.

After: Errors.@this takes App in its ctor. App's ctor allocates
Errors = new(this) once. The internal App?.CallStack and App!.Variables
references become _app.CallStack and _app.Variables — null-coalescing
operators go away.

Out of scope: Error.@this's own App back-ref (line 63's
`e.App = App` reach into Error). That requires moving Error.Callback
materialisation; separate stage.
```
