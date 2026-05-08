# Stage 9: `catalog-dissolve-to-modules-schema`

**Read first:**
- `plan/principles.md` — OBP discipline, especially **Rule E** (decomposed parameters that should navigate). This stage is the worked example for Rule E.
- `plan/scope-map.md` — Modules is App-level (shared); Schema as a sub-property of Modules is also App-level.
- `plan/post-cleanup-tree.md` — the destination tree for `App/Modules/Schema/`.

**Goal:** Dissolve `App/Catalog/` entirely into `App/Modules/Schema/`. The Catalog concept was always "Modules describing themselves to the LLM"; Modules is the owner. Folder, namespace, and types relocate. `Build()` and `Render(spec)` become instance methods on `Modules.Schema.@this`, navigating `this.Modules` internally (Rule E — callers stop passing modules in). The static `ExampleHelpers.cs` deletes; the 12 action handlers using it migrate from `Example("intent", chain)` to `new Example("intent", chain)`.

**Scope:**
- *Included:* folder + namespace relocation; type renames (drop "Spec" suffix on records); Rule E conversion of `Build()` and `Render(spec)`; new `Modules.@this.Schema` property; deletion of `ExampleHelpers.cs`; sweep of all `App.Catalog.*` consumers.
- *Excluded:* the static formatters in `modules/builder/providers/DefaultBuilderProvider.cs` (`FormatValue`, `RenderActionFormal`) and `modules/ui/providers/FluidProvider.cs` (`FormatFormalValue`). The plan one-liner mentioned absorbing them into `Schema.Render`, but they're shaped differently from Schema.Render (lower-level value-to-token rendering, not example-to-string), and the unification needs its own design pass. Flagged for a follow-up stage — out of scope here.

**Deliverables:**

### Folder + file changes

**New folder structure:**
```
App/Modules/Schema/
├── Spec/
│   ├── Action.cs              ← Catalog/ActionSpec.cs (rename, drop "Spec" suffix)
│   └── Example.cs             ← Catalog/ExampleSpec.cs
├── Entry.cs                   ← Catalog/TypeEntry.cs
├── Render.cs                  ← Catalog/ExampleRenderer.cs (instance method now, navigates this.Modules)
└── this.cs                    ← Catalog/this.cs (instance Build(), navigates this.Modules)
```

**Deletions:**
- `App/Catalog/ExampleHelpers.cs` — the static `Example(intent, chain)` helper. Records' positional ctors cover the same use case; callers switch to `new Example(intent, chain)`.

### Namespace rename

`App.Catalog.*` → `App.Modules.Schema.*`. Plus the type renames inside that namespace:
- `Catalog.@this` → `Schema.@this`
- `ActionSpec` → `Action` (under `Spec/`)
- `ExampleSpec` → `Example` (under `Spec/`)
- `TypeEntry` → `Entry`
- `ExampleRenderer` → `Render` (instance method on `Schema.@this` if folded into `this.cs`, or as a separate file `Render.cs` containing partial implementation)

### Rule E — Build() and Render(spec) become instance

**Today (static, take modules as parameter):**
```csharp
// Catalog/this.cs:98
public static @this Build(App.Modules.@this? modules) { ... }

// Catalog/ExampleRenderer.cs (signature):
public static string Render(ExampleSpec spec, App.Modules.@this modules) { ... }
```

**After (instance, navigate this.Modules):**
```csharp
// Modules/Schema/this.cs
public sealed class @this
{
    private readonly App.Modules.@this _modules;

    public @this(App.Modules.@this modules) { _modules = modules; }

    public @this Build() { /* uses _modules instead of parameter */ }
    public string Render(Example spec) { /* uses _modules */ }
    // (or whatever shape the file lands in — see "New shape" below)
}
```

The Schema-as-instance is constructed by Modules and exposed as `app.Modules.Schema`. Schema reaches Modules via the `_modules` field set at construction.

### `Modules.@this.Schema` property

```csharp
// Add to Modules/this.cs:
public Schema.@this Schema { get; }

// Initialize in Modules ctor (today's ctor at line 18 takes no args):
public @this()
{
    Schema = new Schema.@this(this);
    Discover(typeof(@this).Assembly, "App.modules");
}
```

### Caller sweep

**Type references (App.Catalog.* → App.Modules.Schema.*):**
- `PLang/App/Types/this.cs:372` — `Dictionary<string, App.Catalog.TypeEntry>` → `Dictionary<string, App.Modules.Schema.Entry>`.
- `PLang/App/Utils/TypeMapping.cs:351, 353, 412, 415, 447, 454, 476, 479, 495, 498` — multiple `App.Catalog.TypeEntry`, `App.Catalog.TypeKind.*`, `App.Catalog.Field` references rename to `App.Modules.Schema.Entry`, `App.Modules.Schema.EntryKind.*` (or whatever it ends up named — check the type), `App.Modules.Schema.Field`.

**Static method calls become instance navigations (Rule E):**
- `PLang/App/Modules/this.cs:289-294` — the `typeof(App.Catalog.ExampleSpec[])` checks rename to `typeof(App.Modules.Schema.Spec.Example[])`. The `App.Catalog.ExampleRenderer.Render(s, this)` call becomes `this.Schema.Render(s)` (Modules' own Schema property; Modules IS the parent).
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:37` — `App.Catalog.@this.Build(action.Context.App.Modules)` becomes `action.Context.App.Modules.Schema.Build()`. Caller stops decomposing.

**Action handlers using ExampleHelpers (12 sites):**
- `PLang/App/modules/error/handle.cs`, `math/subtract.cs`, and 10 others (per `grep -rn "using static App\.Catalog\.ExampleHelpers" PLang/App/modules/`).
- Each needs:
  - `using static App.Catalog.ExampleHelpers;` line removed.
  - `using App.Modules.Schema.Spec;` (or similar) added if needed for `Example` and `Action` records.
  - Each `Example("intent", chain)` call becomes `new Example("intent", chain)`.
  - Each `chain` (which uses `Action(...)` from ExampleHelpers) — verify the migration. If `Action(...)` was a helper too, it becomes `new Action(...)`. Read `ExampleHelpers.cs` for the full surface before sweeping.

**Plus the global alias if one exists** (check `PLang/App/GlobalUsings.cs`).

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `find PLang/App/Catalog -type f 2>/dev/null` — empty (folder gone).
- `grep -rn "App\.Catalog\." PLang/ PLang.Tests/ Tests/ --include='*.cs'` — zero hits.
- `grep -rn "ExampleHelpers" PLang/ PLang.Tests/ Tests/ --include='*.cs'` — zero hits.
- `app.Modules.Schema.Build()` and `app.Modules.Schema.Render(spec)` exist and work.
- Modules.this.cs shrinks toward the plan's "~150 lines" target (it's at 464 today; the `App.Catalog.ExampleSpec[]` reflection block at lines 289-294 stays, just renamed).

**Dependencies:** None on stage 7 or 8 specifically. Tier 2 stage; ordering within Tier 2 doesn't matter for correctness.

## Design

### The smell this closes

Two smells:

1. **Smell #3** — same logical thing stored twice across types. The Catalog concept *is* "Modules describing itself to the LLM." Modules already has the action data; Catalog walks Modules to build a parallel representation. After this stage, Schema is a *property of Modules* — single owner, single navigation point.

2. **Rule E** — decomposed parameters that should navigate. Today's `Catalog.Build(modules)` and `ExampleRenderer.Render(spec, modules)` decompose: the caller has a Modules reference and chops it off to pass in. After: the caller has `app.Modules.Schema.Build()` — Schema lives under Modules and navigates `this.Modules` internally. The parameter is gone; the navigation is internal.

### The new shape

**`App/Modules/Schema/this.cs`** (new — moves from `Catalog/this.cs`):

```csharp
namespace App.Modules.Schema;

using Spec;

/// <summary>
/// "What every action looks like, for the LLM." Owned by Modules; describes
/// the registered actions' types, parameter schemas, and authored Examples.
/// </summary>
public sealed class @this
{
    private readonly App.Modules.@this _modules;

    public @this(App.Modules.@this modules) { _modules = modules; }

    [LlmBuilder]
    public IReadOnlyList<string> PrimitiveNames { get; private init; } = System.Array.Empty<string>();

    [LlmBuilder]
    public IReadOnlyList<Entry> Types { get; private init; } = System.Array.Empty<Entry>();

    public string TypeNames => string.Join(", ", PrimitiveNames);
    public string TypeSchemas { get { /* ... */ } }

    /// <summary>Builds the schema by walking _modules' action parameter types.</summary>
    public @this Build()
    {
        var primitives = TypeMapping.GetBuilderTypeNames();
        var types = TypeMapping.BuildTypeEntries(_modules);
        return new @this(_modules) { PrimitiveNames = primitives, Types = types };
    }

    public string Render(Example spec) { /* uses _modules */ }
}
```

(Adapt the exact shape; this is a sketch. The `init`-only properties and the rebuild pattern in `Build()` may want a different shape — read `Catalog/this.cs:98+` for the current logic and translate faithfully.)

**`Modules.@this`** gains:

```csharp
// Modules/this.cs:
public Schema.@this Schema { get; }

public @this()
{
    Schema = new Schema.@this(this);
    Discover(typeof(@this).Assembly, "App.modules");
}
```

The `this` passed to Schema's ctor is Modules itself — Schema's parent reference for `this.Modules` navigation.

### Files touched + caller propagation

**Files moved (5):**
- `App/Catalog/this.cs` → `App/Modules/Schema/this.cs`
- `App/Catalog/ActionSpec.cs` → `App/Modules/Schema/Spec/Action.cs`
- `App/Catalog/ExampleSpec.cs` → `App/Modules/Schema/Spec/Example.cs`
- `App/Catalog/TypeEntry.cs` → `App/Modules/Schema/Entry.cs`
- `App/Catalog/ExampleRenderer.cs` → `App/Modules/Schema/Render.cs` (or fold into `this.cs`)

**Files deleted (1):**
- `App/Catalog/ExampleHelpers.cs`

**Files modified (~16):**
- `PLang/App/Modules/this.cs` — gain `Schema` property + ctor allocation; rename internal type refs.
- `PLang/App/Types/this.cs` — one type ref renamed.
- `PLang/App/Utils/TypeMapping.cs` — multiple type refs renamed.
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — Build call site (Rule E migration).
- 12 action handler files (`modules/error/handle.cs`, `modules/math/subtract.cs`, and 10 more — full list via `grep -rn "using static App\.Catalog\.ExampleHelpers"`).

**Caller verification (after sweep):**
- `grep -rn "App\.Catalog\." PLang/ PLang.Tests/ Tests/` — zero hits.
- `grep -rn "ExampleHelpers" PLang/ PLang.Tests/ Tests/` — zero hits.
- `grep -rn "ExampleRenderer\|ActionSpec\|ExampleSpec\|TypeEntry\b" PLang/ PLang.Tests/ Tests/` — zero references to the old names.

### Risk + dependencies

**Risk: medium.** This is the largest stage of the cleanup so far — folder relocation + namespace rename + 16-file modification + 12-handler migration. Build break is the safety net for caller misses; the test suite verifies behavior preservation.

Possible failure modes:
1. **A type reference grep miss.** Multiple distinct names to track (`Catalog.@this`, `ActionSpec`, `ExampleSpec`, `TypeEntry`, `ExampleRenderer`, `ExampleHelpers`, `TypeKind`, `Field`). Build catches these.
2. **The `Action` rename collision.** `App.Modules.Schema.Spec.Action` (the renamed ActionSpec) coexists with `App.Goals.Goal.Steps.Step.Actions.Action.@this` (the runtime Action). Disambiguation via namespace; verify no test or production file uses unqualified `Action` in a context where the new Schema.Spec.Action could shadow the runtime one.
3. **`Example` similarly** — could shadow other Example types if they exist. Less likely but verify.
4. **Schema needs to be built lazily or eagerly?** Today's `Catalog.Build(modules)` is called on-demand by the builder provider. After the rework, `app.Modules.Schema` is a property — when does it build? Two choices:
   - **Eager**: Modules ctor calls `Schema = new Schema.@this(this).Build();` — Schema is fully built at boot.
   - **Lazy**: `Schema` property returns the unbuilt Schema; callers call `.Build()` explicitly when they want the catalog.
   
   Today's pattern is lazy (the builder provider explicitly calls Build). Preserve that — `app.Modules.Schema.Build()` returns a *new* fully-built Schema. The Schema property itself is the un-built shell. Less attractive than eager but matches today's call pattern. **Architect's note: this is a real design choice.** If `Build()` always returns a new Schema, you could argue Schema-as-property doesn't need a `_modules` field — it's just `app.Modules.Schema.Build()` as a "produce a catalog" operation. Simpler. Worth the coder's read.
5. **The `Modules.this.cs` block at lines 289-294** is *inside* `Modules.Describe()` — a method on Modules itself. After the migration, the call becomes `this.Schema.Render(s)` — but if `this.Schema` is a stateful (built) Schema, that's not what's happening today. Today's call is `App.Catalog.ExampleRenderer.Render(s, this)` — a static call passing the Modules. After: `this.Schema.Render(s)` only works if Schema is a Render-host that knows about Modules. So either:
   - Make `Render(spec)` an instance method on a Schema that holds `_modules`, OR
   - Keep `Render` as a static method on `App.Modules.Schema.Render` that takes Modules — but then Rule E isn't fully closed for Render.
   
   **Architect's lean: Schema holds `_modules` and exposes `Render(spec)` as instance.** Cleaner Rule E adherence; Modules.Describe calls `this.Schema.Render(s)` where `this.Schema` is the unbuilt Schema instance held by Modules.

**Dependencies: none.** Tier 2 stage; independent of 7 and 8.

### Tests

**No new tests required.** Behavior is preserved (same renderings, same Build output).

**Existing test coverage to verify:**
- `PLang.Tests/App/Modules/` and any tests that exercise the catalog or Examples — sweep their references.
- Any `App.Catalog.*` references in test files — same rename.
- `Tests/` — full PLang suite (the LLM builder pipeline exercises the rendered catalog).

**Definition of done — see "Deliverables" above.**

### Watch for (coder eyes-on)

- **The grep across action handlers for ExampleHelpers** is the long pole. Each handler authors its own examples; the `Example("intent", chain)` calls are scattered. Before deleting `ExampleHelpers.cs`, run the grep, confirm 12 (or whatever the count is today) handlers, and migrate each.
- **The `Action` record ctor migration** — if `ExampleHelpers.cs` had `Action(module, name, params)` as a helper, calls become `new Action(module, name, params)`. Verify the record's positional ctor matches.
- **Lazy vs eager Schema build** — see Risk #4 above. Read `Catalog.Build` callers carefully; preserve laziness if today's flow depends on it.
- **Rename collisions** with `Action` and `Example` — verify by reading each modified file's resolution after the rename.
- **The static formatters absorption (FormatValue, FormatFormalValue, RenderActionFormal)** — explicitly out of scope; if you see a clean opportunity to fold them, flag for a follow-up stage but don't do it here.
- **Modules.this.cs line count** — plan target is ~150 lines (currently 464). Stage 9 alone won't get there; the rest comes from later stages. Don't scope-creep to make the number.

### Stages that follow this one

- Tier 3 stages (10, 11, 12) — `app-run-redesign`, `errors-app-backref-drop`, `build-branch-to-build-this`. Independent of stage 9.
- The static-formatter absorption flagged here as out-of-scope is a candidate for a future small stage if anyone takes it on.

### Out of scope

- Static formatters in builder/Fluid providers — flagged above; future stage.
- Any further reshape of Modules.@this — stage 9 only adds the Schema property; the rest of Modules' surface stays as-is.
- Any LLM-builder behavior change — this stage preserves rendering exactly.

## Commit plan

```
runtime2-cleanup stage 9: Catalog dissolves into Modules.Schema

The Catalog concept was always "Modules describing itself to the
LLM" — Modules had the action data; Catalog walked Modules to build
a parallel representation. After today, Schema is a property of
Modules. Single owner. Navigation matches structure.

Folder relocations:
  App/Catalog/this.cs           → App/Modules/Schema/this.cs
  App/Catalog/ActionSpec.cs     → App/Modules/Schema/Spec/Action.cs
  App/Catalog/ExampleSpec.cs    → App/Modules/Schema/Spec/Example.cs
  App/Catalog/TypeEntry.cs      → App/Modules/Schema/Entry.cs
  App/Catalog/ExampleRenderer.cs → App/Modules/Schema/Render.cs

Deletions:
  App/Catalog/ExampleHelpers.cs (records' positional ctors replace
  the static Example(intent, chain) and Action(module, name, params)
  helpers — 12 action-handler call sites migrate to `new Example(...)`
  and `new Action(...)`).

Rule E refactor (decomposed parameters → navigation):
  Catalog.@this.Build(modules)         → app.Modules.Schema.Build()
  ExampleRenderer.Render(spec, modules) → schema.Render(spec)

Schema holds a _modules ref set at construction. Modules.@this gains
a Schema { get; } = new Schema.@this(this); property. Render becomes
an instance method that navigates this.Modules internally; callers
stop passing modules in.

Type-ref sweeps across Types/this.cs, Utils/TypeMapping.cs (multiple
sites), Modules/this.cs, DefaultBuilderProvider.cs, and 12 action
handlers in modules/.

Out of scope: the static formatters in DefaultBuilderProvider
(FormatValue, RenderActionFormal) and FluidProvider (FormatFormalValue).
Plan one-liner mentioned absorbing them into Schema.Render but they
serve a different layer (value-token rendering, not example-string
rendering); their unification needs its own design pass.
```
