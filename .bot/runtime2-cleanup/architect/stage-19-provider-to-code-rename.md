# Stage 19: `provider-to-code-rename`

**Read first:**
- `plan/principles.md` — OBP discipline. The driver here is **language coherence**: PLang's narrative is "everything is goals (plang), except where you need code." Calling the runtime escape hatch `Provider` was DI-flavored and PLang-foreign; renaming to `Code` aligns the runtime's vocabulary with the language.
- `plan/scope-map.md` — Code is shared (App-level); per-module Code is per-module-but-app-scoped (one default impl per module, swappable via DLL).
- `plan/post-cleanup-tree.md` — destination tree shows the final shape.

**Goal:** End-to-end rename: `Provider` concept becomes `Code` throughout. Folder, marker interface, per-module folders, per-module interfaces (suffix dropped), implementations (suffix dropped). Plus rename of the user-facing `modules/provider/` → `modules/code/` (the action handlers that load DLLs and manage registrations).

**Scope:**
- *Included:* all the renames listed below — 12 folders, ~22 files, namespace updates, all caller sweeps, `IProvider` → `ICode` marker interface (fields preserved), `app.Providers` property → `app.Code`.
- *Excluded:* anything inside the renamed types — methods, fields, internal logic stay. The marker interface fields (`Name`, `IsDefault`, `IsBuiltIn`, `Source`) stay (they're load-bearing for the developer-DLL-registration flow).

**Deliverables:**

### Folder relocations (12)

```
App/Providers/                       → App/Code/                       (the central registry + marker)
App/Data/Providers/                  → App/Data/Code/
App/modules/assert/providers/        → App/modules/assert/code/
App/modules/builder/providers/       → App/modules/builder/code/
App/modules/condition/providers/     → App/modules/condition/code/
App/modules/crypto/providers/        → App/modules/crypto/code/
App/modules/file/providers/          → App/modules/file/code/
App/modules/http/providers/          → App/modules/http/code/
App/modules/identity/providers/      → App/modules/identity/code/
App/modules/llm/providers/           → App/modules/llm/code/
App/modules/signing/providers/       → App/modules/signing/code/
App/modules/ui/providers/            → App/modules/ui/code/
```

Plus the user-facing actions module: `App/modules/provider/` → `App/modules/code/` (if not too disruptive — verify no PLang `.goal` files reference `- call provider.X` actions; if so, the rename is breaking and may need versioned migration).

### Central marker interface

`App/Providers/IProvider.cs` → `App/Code/ICode.cs`

```csharp
namespace App.Code;

/// <summary>
/// Marker interface for runtime-overridable C# implementations. The fields
/// support the developer-DLL-registration flow: `- add 'claudeapi.dll' on
/// llm module, set as default` populates Name, IsDefault, IsBuiltIn, Source
/// on the registered ICode.
/// </summary>
public interface ICode
{
    string Name { get; }
    bool IsDefault { get; set; }
    bool IsBuiltIn { get; set; }
    string? Source { get; set; }
}
```

Same fields as today's IProvider; just the type name changes.

### Per-module interface renames

Drop the `Provider` suffix on the per-module interfaces:

| Today | After |
|-------|-------|
| `IGrepProvider` | `IGrep` |
| `ISigningProvider` | `ISigning` |
| `IKeyProvider` | `IKey` |
| `IFileProvider` | `IFile` |
| `IAssertProvider` | `IAssert` |
| `ILlmProvider` | `ILlm` |
| `IHttpProvider` | `IHttp` |
| `IIdentityProvider` | `IIdentity` |
| `IBuilderProvider` | `IBuilder` |
| `ICryptoProvider` | `ICrypto` |
| `ITemplateProvider` | `ITemplate` |
| `IEvaluator` | `IEvaluator` (already correct — no change) |

Each interface declaration changes `: IProvider` to `: ICode`.

### Implementation renames (drop both `Default` and `Provider` suffixes)

The rule: name after the variant when meaningful; use `Default.cs` when the parent path already says the role.

| Today | After | Reason |
|-------|-------|--------|
| `OpenAiProvider.cs` | `OpenAi.cs` | variant name |
| `FluidProvider.cs` | `Fluid.cs` | variant name |
| `Ed25519Provider.cs` | `Ed25519.cs` | variant name |
| `DefaultGrepProvider.cs` | `Grep.cs` (variant = Grep, the role) | folder = Code/, but data/Code distinguishes via name |
| `DefaultBuilderProvider.cs` | `Default.cs` | parent path = builder/code; one impl |
| `DefaultHttpProvider.cs` | `Default.cs` | same |
| `DefaultIdentityProvider.cs` | `Default.cs` | same |
| `DefaultAssertProvider.cs` | `Default.cs` | same |
| `DefaultFileProvider.cs` | `Default.cs` | same |
| `DefaultProvider.cs` (crypto) | `Default.cs` | already mostly there; remove namespace path duplication |
| `DefaultEvaluator.cs` | `Default.cs` | parent = condition/code; one impl |

### App property rename

`PLang/App/this.cs` — `app.Providers` becomes `app.Code`:

```csharp
// Today (somewhere in App's properties):
public Providers.@this Providers { get; }

// After:
public Code.@this Code { get; }
```

The constructor allocation similarly: `Providers = new Providers.@this(...)` → `Code = new Code.@this(...)` (or whatever the existing allocation pattern is).

### Caller sweep

Every site:
- `app.Providers` → `app.Code`
- `IProvider` (the interface) → `ICode`
- `IXProvider` interface refs → `IX` per the table above
- `using App.Providers;` → `using App.Code;`
- `using App.modules.X.providers;` → `using App.modules.X.code;`
- `App.Providers.@this` → `App.Code.@this`
- `App.modules.X.providers.IXProvider` → `App.modules.X.code.IX`

Total caller hit count is substantial — likely 100+ sites. Build catches every miss; the work is mechanical.

### `IsBuiltIn` / `IsDefault` semantics — stay

The existing semantics on `IProvider` (now `ICode`) carry through:
- `Name` — registration key, set by the implementation.
- `IsDefault` — default implementation flag. Settable by `provider.setDefault` action.
- `IsBuiltIn` — true for boot-time defaults; reconstructed on App boot, not snapshotted.
- `Source` — DLL path for snapshot/restore; null for in-process built-ins.

All four stay. Just the type name changes.

### `App.modules.provider.X` action handlers

The PLang-facing actions for loading/managing providers live at `App/modules/provider/{load,setDefault,remove,list}.cs`. The folder `provider/` (singular, action-name namespace) would rename to `code/`. **But:** PLang `.goal` files invoking `- load 'foo.dll' on llm module` go through these actions by name. The action namespace is `provider`; the action names are `load`, `setDefault`, etc.

After the rename:
- C# folder: `App/modules/provider/` → `App/modules/code/`
- C# namespace: `App.modules.provider` → `App.modules.code`
- PLang invocations: `- code.load 'foo.dll' ...` (the action namespace shifts).

This is breaking for any existing PLang code that uses `provider.load` etc. Check `Tests/` for `.goal` files that invoke `provider.X` actions; those need rewriting or a transitional alias.

**Architect's lean: do the rename; sweep `.goal` files in Tests/.** Coder verifies via `grep -rn 'provider\.\(load\|setDefault\|remove\|list\)' Tests/` before/after.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- `find PLang/App -type d -name "providers" -o -name "Providers"` returns empty (other than `provider` action-folder if not renamed).
- `grep -rn "IProvider\|IXProvider\b" PLang/ PLang.Tests/ Tests/ --include='*.cs'` — only `ICode` and `IX` matches now.
- `grep -rn "app\.Providers\|App\.Providers" PLang/ PLang.Tests/ --include='*.cs'` — zero hits.
- `app.Code` property exists; works for built-in lookups and DLL-registered overrides.

**Dependencies:** None on stages 15/16 specifically. Independent.

## Design

### The smell this closes

The Provider name was DI-flavored — imported from .NET conventions. PLang's vocabulary is goals/code: "everything is goals, except where you need code." The runtime's escape-hatch concept should match. Renaming to Code makes the runtime's vocabulary load-bearing for the language's narrative.

The fields on the marker (`Name`, `IsDefault`, `IsBuiltIn`, `Source`) survive — they encode the load-via-DLL flow that PLang users actually see. The rename is about *naming*, not *behavior*.

### Files touched

**Folders renamed (12+):** all provider folders + possibly modules/provider/.

**Files renamed (~22):** every `*Provider.cs` and `IXProvider.cs` in the rename table.

**Files modified for namespace + caller updates:** every renamed file + likely 50-100+ caller files across PLang/ and PLang.Tests/.

### Risk + dependencies

**Risk: medium-high.** The volume is the issue — biggest sweep on the branch. Each individual change is mechanical; the count of places to update is large.

Possible failure modes:
1. **Naming collisions.** `Default.cs` will appear in many module subfolders simultaneously; C# resolves via namespace, but if any namespace conflicts arise, the build catches them.
2. **`Default.cs` everywhere** can be confusing when reading code without context. The fully-qualified name `App.modules.builder.code.Default` disambiguates; flat-file references inside the namespace use just `Default`.
3. **PLang `.goal` files** invoking `provider.X` actions break if the namespace renames. Sweep Tests/ and decide: rename actions in `.goal` files, or keep the action namespace as `provider` (action layer can stay even when implementations are at `code/`).
4. **`IBuilder` rename collision** — `App.modules.builder.code.IBuilder` is the new interface. There's no `App.Builder` interface today (Builder.@this is a class). No collision expected; verify on read.
5. **`IFile`** in `App.modules.file.code` — System.IO.File or similar might shadow if usings aren't qualified. Use qualified namespace `App.modules.file.code.IFile` where needed.
6. **The `ICode` marker** — confirm no other `ICode` interface elsewhere in the codebase. Unlikely (not a common .NET name); grep first.

**Dependencies: stages 1, 17 (Builder rename), 21 (Navigators move) all already landed.** Stage 19 builds on the finished branch.

### Tests

**No new tests required.** Behavior preserved.

**Existing test coverage to verify:**
- `PLang.Tests/App/Providers/` (or wherever provider-registration tests live) — exercise the new ICode marker.
- `PLang.Tests/App/Modules/X/` for each module — verify the per-module interface rename works.
- Tests that load DLLs (`provider.load` / `code.load`) — verify the registration flow.
- `Tests/` — full PLang suite. Critical given the scope.

### Watch for (coder eyes-on)

- **The `modules/provider/` action folder** — decide before starting: keep as `provider/` (action-namespace stable; `.goal` files don't break) or rename to `code/` (consistent rename; `.goal` files need migration). Read existing `.goal` files in Tests/ for `provider.X` invocations first.
- **The `Default.cs` naming proliferation** — once you rename `DefaultBuilderProvider.cs` → `Default.cs`, every module/code/ folder has a `Default.cs`. Their FQN disambiguates. Don't try to merge or share.
- **The marker interface name `ICode`** — search for any other `ICode` in the codebase before claiming it. Unlikely conflict.
- **PLang reflection** — if anything reflects on the `Provider` suffix (probably none, since handlers are typed via `[Action]` attribute and source generators), the rename breaks it. Trust the build.
- **The `IsBuiltIn` flag** — unchanged. RegisterDefaults at boot still sets IsBuiltIn = true; snapshot/restore still excludes built-ins.
- **`IEvaluator` not renamed** — already follows the new convention (no `Provider` suffix). Just folder relocates. Class name stays.
- **A grep miss could leave dangling `Provider` references** — rebuild from clean and run grep verification:
  ```bash
  grep -rn "IProvider\|XProvider\|ProviderProvider\|app\.Providers" PLang/ PLang.Tests/ Tests/ --include='*.cs' --include='*.goal'
  ```
- **Coder lessons accumulated through the cleanup**: prior briefs missed unqualified references that landed inside same-namespace files (`Variables` inside App, `ValueNavigators` from inside App.Data). For stage 19 the same pattern applies: search for unqualified `IProvider` and `Providers` references inside files in App.Providers and elsewhere that would resolve via implicit using.

### Stages that follow this one

None. Stage 19 is the last stage of the cleanup. After it lands, all 22 stages are done.

### Out of scope

- Anything inside the renamed types beyond the type-name change.
- The action-handler logic in `modules/provider/{load,setDefault,...}.cs` — only the namespace changes.
- The `Default.cs` impl bodies (which are the actual provider implementations) — bodies preserved exactly.

## Commit plan

```
runtime2-cleanup stage 19: Provider → Code rename, end-to-end

Provider was DI-flavored (imported from .NET conventions). PLang's
vocabulary is goals/code: "everything is goals, except where you need
code." Renaming the runtime escape-hatch to Code aligns the runtime's
words with the language's narrative.

Folder relocations (12):
  App/Providers/                       → App/Code/
  App/Data/Providers/                  → App/Data/Code/
  App/modules/{assert,builder,condition,crypto,file,http,identity,llm,signing,ui}/providers/
                                       → App/modules/X/code/

Per-module interfaces drop "Provider" suffix:
  IGrepProvider     → IGrep
  ISigningProvider  → ISigning  (etc., 11 interfaces)
  IEvaluator        unchanged (already correct)

Marker interface IProvider → ICode. Fields preserved (Name, IsDefault,
IsBuiltIn, Source) — they encode the developer-DLL-registration flow.

Implementations drop both "Default" and "Provider" suffixes:
  OpenAiProvider.cs   → OpenAi.cs    (variant name)
  FluidProvider.cs    → Fluid.cs     (variant name)
  Ed25519Provider.cs  → Ed25519.cs   (variant name)
  DefaultXProvider.cs → Default.cs   (one impl per module; parent
                                      path says the role)

App property: app.Providers → app.Code.

Modules/provider/ action folder: rename TBD by the coder based on
.goal files in Tests/. If renaming, .goal files migrate too.

100+ caller sweeps across PLang/ and PLang.Tests/. Mechanical;
build catches misses. Behavior preserved everywhere.
```
