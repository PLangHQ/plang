# Stage 5: `getstatic-shim-drop`

**Read first:**
- `plan/principles.md` — OBP discipline, especially the "smells" section.
- `plan/scope-map.md` — Statics is App-level (shared per app); same as today, no scope change.

**Goal:** Delete the `App.GetStatic(string)` internal shim that delegates to `Statics.GetBag(key)`. Update its single caller to use `app.Statics.GetBag(key)` directly.

**Scope:**
- *Included:* delete the one-line `internal ConcurrentDictionary<string, object?> GetStatic(string key) => Statics.GetBag(key);` at App.this.cs:115; update the single caller at Actor/Context/this.cs:248.
- *Excluded:* anything else. This is a one-line deletion + one-line caller update.

**Deliverables:**
- `PLang/App/this.cs` — delete line 115 (`internal ConcurrentDictionary<string, object?> GetStatic(string key) => Statics.GetBag(key);`) and the doc-comment block immediately above it if there is one.
- `PLang/App/Actor/Context/this.cs:248` — change `"app" => App.GetStatic(key)` to `"app" => App.Statics.GetBag(key)`.
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. Independent of all other stages.

## Design

### The smell this closes

A back-compat shim that no longer earns its keep. `App.GetStatic` was probably introduced when `Statics` didn't exist as a public surface, or as a one-line convenience. Today `app.Statics.GetBag(key)` is the public navigation path, and one caller still goes through the shim instead. Two paths to the same destination; the shim is dead weight.

### The new shape

**`Actor/Context/this.cs`** (line 248):

```csharp
// Today:
"app" => App.GetStatic(key),

// After:
"app" => App.Statics.GetBag(key),
```

**`App.this.cs`** — line 115 deleted:

```csharp
// Today:
internal ConcurrentDictionary<string, object?> GetStatic(string key) => Statics.GetBag(key);

// After: deleted.
```

### Files touched + caller propagation

**Files modified (2):**
- `PLang/App/this.cs` — one line deleted (and any leading doc-comment).
- `PLang/App/Actor/Context/this.cs` — one expression updated on line 248.

**Caller verification:**
- `App.GetStatic` is `internal` — only same-assembly code can see it.
- Grep `\.GetStatic(\|app\.GetStatic` across `PLang/`, `PLang.Tests/`, `Tests/` returns exactly one hit: Actor/Context/this.cs:248. After this stage, the method has zero references.

### Risk + dependencies

**Risk: very low.** One caller, source-compatible migration to a longer access path that does the same thing.

Possible failure modes:
- A grep miss on `GetStatic` callers — unlikely; the method is `internal` and the grep was thorough.
- Build break on the caller line — caught immediately.

**Dependencies: none.** Independent.

### Tests

**No new tests required.** Behavior unchanged.

**Existing test coverage to verify:**
- `PLang.Tests/App/Statics/` (if it exists) — static state behavior.
- `Tests/` — full PLang suite.

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -n "GetStatic\b" PLang/` — zero hits.

### Watch for (coder eyes-on)

- **Other shim-shaped one-liners on App.this.cs.** While reading the file you may see other "delegate to subsystem" methods on App that exist only as legacy convenience. They're not stage 5; flag in the commit message if you see one worth a future stage.
- **Doc comment drift** — if the deleted method had an XML doc comment referencing it from elsewhere, that reference is now stale. Unlikely, but worth a glance.

### Stages that follow this one

- **Stage 6** (`app-data-inheritance-drop`) — same Tier 1 batch as stage 5; both touch App.this.cs in different sections; either order works.

### Out of scope

- Any other refactor of `App.Statics` — separate stage if needed.
- Any caller migration outside the one site at `Actor/Context/this.cs:248`.

## Commit plan

```
runtime2-cleanup stage 5: drop App.GetStatic shim

App.GetStatic(string) was a one-line internal shim that delegated to
Statics.GetBag(key). One caller in Actor/Context/this.cs:248 went
through it; everyone else used app.Statics.GetBag directly.

Deletes the shim. Updates the single caller to use the public
navigation path: App.Statics.GetBag(key).
```
