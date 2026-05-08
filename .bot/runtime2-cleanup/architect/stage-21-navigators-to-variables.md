# Stage 21: `navigators-to-variables`

**Read first:**
- `plan/principles.md` — OBP discipline.
- `plan/scope-map.md` — Navigators is App-level; the placement question (under App/Data/ vs App/Variables/) was settled 2026-05-08 in favor of Variables, since Data is stored and retrieved via Variables.

**Goal:** Move `App/Data/Navigators/` to `App/Variables/Navigators/`. Namespace `App.Data.Navigators` → `App.Variables.Navigators`. Property `app.Navigators` → `app.Variables.Navigators` (a sub-property of Variables). Boot registration `Navigators.RegisterDefaults()` moves to where Variables is allocated. Pure folder relocation + namespace rename + caller sweep; no behaviour change.

**Scope:**
- *Included:* relocate the 7 files in the folder; rename namespace; rename App property to a Variables sub-property; update boot registration; sweep callers (~2 sites: 1 production at App.this.cs:126 + 1 test).
- *Excluded:* anything inside the navigators themselves — INavigator, JsonStringNavigator, DictionaryNavigator, ListNavigator, ObjectNavigator, ValueNavigators, Navigators/this.cs internals all stay as-is.

**Deliverables:**

### Folder relocation

```
App/Data/Navigators/                  →  App/Variables/Navigators/
├── INavigator.cs                       (namespace: App.Data.Navigators → App.Variables.Navigators)
├── JsonStringNavigator.cs              (same)
├── DictionaryNavigator.cs              (same)
├── ListNavigator.cs                    (same)
├── ObjectNavigator.cs                  (same)
├── ValueNavigators.cs                  (same)
└── this.cs                             (same)
```

All 7 files: namespace declaration changes from `App.Data.Navigators` to `App.Variables.Navigators`.

### Property relocation

`PLang/App/this.cs:126` — today:

```csharp
public Data.Navigators.@this Navigators { get; } = new();
```

After: this property goes away. Variables.@this gains a Navigators property (allocated at field-init or in Variables ctor).

`PLang/App/Variables/this.cs` — gain:

```csharp
public Navigators.@this Navigators { get; } = new();
```

(The exact placement depends on how Variables.@this is structured; pick a spot near other shared sub-properties.)

### Boot registration relocation

`PLang/App/this.cs:313` — today calls `Navigators.RegisterDefaults();` during App's ctor.

After: this call moves to wherever Variables.Navigators is constructed. If Variables' ctor allocates `Navigators = new Navigators.@this()`, the `RegisterDefaults()` call should follow that line in Variables' ctor (or be invoked by Navigators' own ctor — coder's choice; today's pattern is "App calls RegisterDefaults explicitly," same pattern moves to Variables).

Note: Variables is constructed per-actor (`new Variables.@this()` in each Actor's Context). If Variables.Navigators is `new()` per Variables instance, that's per-actor — *but the navigators are app-level* (registered once at boot, used by all). Two options:

- **(a) Per-Variables Navigators with auto-registered defaults** — each Variables gets its own Navigators with the same defaults. Simple but duplicates the registration.
- **(b) Shared Navigators owned by App, exposed under Variables namespace.** App allocates one Navigators instance; Variables.Navigators is a property that returns that shared instance. The folder/namespace move is just structural; the instance is still one-per-app.

**Architect's lean: (b).** Navigators is shared infrastructure (per the scope-map's shared list). The folder move shouldn't change scope from shared to per-actor. The Navigators @this stays one-per-app on App; the access path moves from `app.Navigators` to `app.Variables.Navigators`. Boot registration stays in App.

**Concretely:**

```csharp
// App.this.cs (replaces line 126):
public Variables.Navigators.@this Navigators { get; } = new();

// Variables.@this gains a property that's just a delegate to App's instance:
// (Each per-actor Variables has access to App via _context.App after
//  earlier stages.)
public Variables.Navigators.@this Navigators => _context!.App.Navigators;
```

That's the minimum change. The instance lives on App; Variables exposes it; the namespace migration is the structural fix; the access path becomes `app.Variables.Navigators` for callers that reach via the property chain (or `app.Navigators` still works since it's directly on App).

**Either way, the namespace move and folder relocation happen.** The instance-scoping question is the design call to make in this stage — flag if the simpler delegate-to-App route doesn't fit.

### Caller sweep

External callers of `App.Data.Navigators`:
- `PLang/App/this.cs:126` — declaration; replaced.
- `PLang.Tests/App/DataTests/Navigators/JsonObjectNavigationTests.cs:1` — `using global::App.Data.Navigators;` becomes `using global::App.Variables.Navigators;`.
- Any other `using App.Data.Navigators` or `App.Data.Navigators.X` — sweep with grep across PLang/ and PLang.Tests/.

After sweep: `grep -rn "App\.Data\.Navigators\|using.*Data\.Navigators" PLang/ PLang.Tests/ --include='*.cs'` returns zero hits.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild (note: stage 17's CLI rename if it lands first; otherwise `--test`).
- `find PLang/App/Data/Navigators -type f` — empty (folder gone).
- `find PLang/App/Variables/Navigators -type f` — 7 files (relocated).
- `grep -rn "App\.Data\.Navigators" PLang/ PLang.Tests/ --include='*.cs'` — zero hits.

**Dependencies:** None. Independent of stage 17.

## Design

### The smell this closes

`app.Navigators` was at App root; the folder was under `App/Data/Navigators/`. The placement disagreed: navigators are Data navigation, but Data is stored and retrieved via Variables. Folder under Data is residue from when Navigators was first added; the natural home is under Variables since that's where the data they navigate lives.

Single navigation point: `app.Variables.Navigators` (via the property chain).

## Risk + dependencies

**Risk: low.** Mechanical relocation. Build catches misses.

Possible failure modes:
1. The grep on caller sites missed something — build break at the call site.
2. The navigation-instance scoping (per-actor vs shared) — keep as shared per the scope-map; the delegate-from-Variables approach keeps the instance App-level.

**Dependencies: none.** Independent of stage 17.

## Watch for (coder eyes-on)

- **The `Navigators.RegisterDefaults()` call timing** — must happen after App is constructed and before any code uses navigators at runtime. Today's call is at App.this.cs:313 (in App ctor); preserve placement.
- **The `using App.Data.Navigators;` lines in tests** — sweep with grep.
- **Whether Variables.@this needs to take any new ctor arg** — if you go with the delegating-property route, Variables needs an App reference. Variables today has `_context` (per stage-13's RegisterNavigable work); through `_context.App.Navigators` you reach the navigators. No new ctor arg needed.

## Stages that follow this one

- **Stage 17** (`builder-tester-rename`) — same Tier 4 batch; independent.
- Stages 15, 16, 18, 19, 22 still to land.

## Out of scope

- Any change to navigator implementations.
- Renaming individual navigator files (e.g., dropping the `Navigator` suffix) — Rule A consideration; out of scope for stage 21.
- Changing the navigators' instance scope (per-actor vs shared) — keep shared per scope-map.

## Commit plan

```
runtime2-cleanup stage 21: Navigators move from Data/ to Variables/

Navigators were under App/Data/Navigators/ — placement disagreed
with usage. Data is stored and retrieved via Variables; navigators
are Data navigation. The natural home is under Variables.

Folder: App/Data/Navigators/ → App/Variables/Navigators/.
Namespace: App.Data.Navigators → App.Variables.Navigators.
Property: app.Navigators stays on App (the instance is shared); a
delegating property app.Variables.Navigators added for the new
canonical access path.

Boot registration (Navigators.RegisterDefaults) stays in App's ctor
since the instance is App-allocated.

7 files relocated; 1 production caller updated; 1 test caller updated.
Pure structural move; no behaviour change.
```
