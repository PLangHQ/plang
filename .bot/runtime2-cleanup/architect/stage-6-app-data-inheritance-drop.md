# Stage 6: `app-data-inheritance-drop`

**Read first:**
- `plan/principles.md` — OBP discipline. Inheritance vs composition is foundational; PLang pivoted to `Data<T>` as composition (every consumer wraps via `new Data<T>(value)`), and App is the only `@this` type still inheriting from `Data.@this<>`.
- `plan/scope-map.md` — App is the bootstrap root (no Context, no parent); this stage doesn't change that.

**Goal:** Drop `Data.@this<@this>` from App's class declaration. App becomes a plain class. Removes the `new string Path => "/"` shadow that exists only because App inherited a `Path` property from Data. Verifies that nothing reads inherited Data surface (`Properties`, `Type`, `Error`, `Success`, `OnChange`, `OnCreate`, `OnDelete`) on app.

**Scope:**
- *Included:* change line 19 of App.this.cs from `public sealed partial class @this : Data.@this<@this>, IAsyncDisposable` to `public sealed partial class @this : IAsyncDisposable`. Resolve the `new string Path => "/"` shadow at line 63 (drop the `new` keyword OR delete the property — see Design below). Confirm `%!app%` resolution still works through its existing `DynamicData("!app", () => app)` registration.
- *Excluded:* everything else. This stage is the inheritance removal and its immediate fallout. No surface re-organization, no caller migration of inherited members (zero such callers — verified).

**Deliverables:**
- `PLang/App/this.cs:19` — class declaration loses `Data.@this<@this>` from its base list. Becomes `public sealed partial class @this : IAsyncDisposable`.
- `PLang/App/this.cs:63` — the `public new string Path => "/"` line. Two acceptable resolutions; pick whichever the build prefers:
  - **(a) Drop the `new` keyword** if the property is referenced anywhere I didn't catch: `public string Path => "/"`.
  - **(b) Delete the property entirely** if the grep stays empty (verified: zero `app.Path` consumers across `PLang/`, `PLang.Tests/`, `Tests/` outside `FileSystem.Path` — different concept).
  Lean: **(b)**, but if the build breaks on a missing reader, fall back to (a).
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. Independent of all other stages.

## Design

### The smell this closes

App is the only `@this` in the runtime that *inherits* from `Data.@this<>` rather than wrapping via composition. Every other site uses `Data<T>` as a composition pattern: `new Data<int>(value)`, `Data<App>` returned from a method, etc. — never inheritance. The inheritance form on App is a vestige from before that pattern settled.

The signal that the inheritance is dead weight:
- Zero `app.Properties`, `app.Type`, `app.Error`, `app.Success`, `app.OnChange`, `app.OnCreate`, `app.OnDelete` calls anywhere in the codebase (verified by grep).
- Zero casts of `app` to `Data.@this<App>`.
- Zero `app.Path` consumers (the `new string Path` shadow on line 63 covers a base property nobody reads on app).

App carries the inherited surface but exposes nothing through it. Removing the inheritance shrinks App's public API to what's actually used.

The `%!app%` PLang variable resolution doesn't go through the inherited Data interface — it goes through `Context.Variables.Set("!app", new Data.DynamicData("!app", () => app))` (registered in Actor.this.cs:136). The runtime wraps `app` in a `DynamicData` to expose it as a `%var%`-resolvable; the wrapper is what gives it Data semantics in PLang code. App itself doesn't need to *be* a Data.

### The new shape

**App.this.cs:19** changes from:

```csharp
public sealed partial class @this : Data.@this<@this>, IAsyncDisposable
```

to:

```csharp
public sealed partial class @this : IAsyncDisposable
```

**App.this.cs:63** — the `public new string Path => "/"`. The `new` keyword exists because base `Data.@this<@this>` had a `Path` property that App was shadowing. Once the inheritance goes, the `new` is a compile error — remove it. Whether to keep `Path` at all depends on whether anything reads `app.Path`. Verified zero consumers; lean delete:

```csharp
// Today:
public new string Path => "/";

// After: deleted entirely.
// Fallback if the grep missed a reader: public string Path => "/";
```

### Files touched + caller propagation

**Files modified (1):**
- `PLang/App/this.cs` — class declaration line; possibly the Path line (delete or `new`-drop).

**Caller verification (already done):**
- `app.Properties`, `app.Type`, `app.Error`, `app.Success`, `app.OnChange`, `app.OnCreate`, `app.OnDelete`: zero hits.
- `app.Path` (filtered to exclude `FileSystem.Path` which is unrelated): zero hits.
- Casts `(Data.@this<App>)`: zero hits. The `Data.@this<>` references in `TypeMapping.cs`, `ExampleRenderer.cs`, `Choices/this.cs`, etc. are *generic type-meta* checks (`typeof(Data.@this<>)`) — they inspect generic type identity, not App's instance.
- `%!app%` PLang resolution: goes through `DynamicData("!app", () => app)` (Actor.this.cs:136) — wrapping pattern, not inheritance.

After this stage, App is a plain class with `IAsyncDisposable`. Its public surface narrows to what App.this.cs explicitly defines.

### Risk + dependencies

**Risk: medium.** Class hierarchy change. The build is the safety net — if any caller does depend on App being a `Data.@this<App>`, the compiler tells you exactly where. None expected; greps were thorough but not exhaustive.

Possible failure modes:
1. **A grep miss on inherited-surface usage** — unlikely (six member names searched, all empty), but the build catches it.
2. **A reflection-based check** that looks up `Data.@this<>` and expects App to satisfy it — `TypeMapping.cs` and similar files do these checks, but they're querying generic type definitions (e.g., "is this T a Data<>?"), not "is App a Data<App>?". Stage 6 doesn't change anything those files care about.
3. **The Path property** — if a grep miss surfaces a reader, switch resolution from delete to `new`-drop.
4. **The `partial class` declaration in another file** — App is `sealed partial`, so there may be other partials (`App.this.Statics.cs`, `App.this.Snapshot.cs`, etc.). The base list goes on the *primary* partial only (App.this.cs:19). Other partials don't repeat the base; they shouldn't need changes.

**Dependencies: none.** Independent.

### Tests

**No new tests required.** Behavior unchanged for everything that doesn't depend on inherited Data surface (verified zero such consumers).

**Existing test coverage to verify:**
- `PLang.Tests/App/Core/EngineTests.cs` — App lifecycle, construction.
- `Tests/` — full PLang suite, especially anything exercising `%!app%` (verifies the DynamicData path still resolves).

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -n ": Data.@this<@this>" PLang/App/this.cs` — zero hits.
- `grep -n "public new string Path" PLang/App/this.cs` — zero hits.

### Watch for (coder eyes-on)

- **Other partials of `App.@this`** (`App.this.Statics.cs`, `App.this.Snapshot.cs`, `App.this.Boot.cs`, etc. — whatever exists). The base list change goes on the primary partial only; secondary partials shouldn't repeat the base. If a secondary partial happens to have the base list too (irregular but possible), update it identically.
- **Inherited members the grep didn't catch.** Six member names searched (`Properties`, `Type`, `Error`, `Success`, `OnChange`, `OnCreate`, `OnDelete`). If you find another inherited member being read on `app`, build break catches it; resolve by either keeping the property locally on App or by sweeping the caller.
- **Reflection or attribute scanning that walks App's full public surface.** Unlikely but possible. Tests will surface it.
- **The `new` keyword discipline elsewhere.** If you see other `public new` shadows on App or its partials, those may be related to the inheritance and need attention. None expected from my reading; flag if you find one.

### Stages that follow this one

- **Stage 5** (`getstatic-shim-drop`) — same Tier 1 batch; either order works. Independent.
- After stages 1–6 land, App.this.cs has shrunk meaningfully — less inherited surface, less manual cleanup, no shim. That's the Tier 1 endpoint.

### Out of scope

- Any reorganization of App's properties — stage 7+ territory if any.
- Any rename of inherited concepts that survive on Data — separate concern.
- Removing `Data.@this<>` from the runtime (it stays — it's the universal Data type used by composition everywhere except App).

## Commit plan

```
runtime2-cleanup stage 6: App stops inheriting Data.@this<App>

App was the last @this in the runtime to use Data<T> as inheritance
rather than composition. Every other consumer wraps Data via
composition (`new Data<int>(v)`, `Data<App> result = ...`); only App
declared `: Data.@this<@this>`. Vestige from before the composition
pattern settled.

Verified zero callers depend on inherited Data surface on app:
- Zero hits on app.Properties, app.Type, app.Error, app.Success,
  app.OnChange/OnCreate/OnDelete.
- Zero hits on app.Path (the `new string Path => "/"` shadow at
  line 63 had no readers).
- Zero casts of (Data.@this<App>)app.
- %!app% PLang resolution goes through DynamicData("!app", () => app)
  in Actor.this.cs:136 — wrapping pattern, doesn't depend on App
  being Data.

App.this.cs:19 drops `Data.@this<@this>` from base list. Path shadow
at line 63 deleted (zero readers; would have been a compile error
otherwise once `new` becomes invalid).

App's public surface narrows to what App.this.cs explicitly defines.
```
