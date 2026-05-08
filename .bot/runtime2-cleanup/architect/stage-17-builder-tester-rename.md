# Stage 17: `builder-tester-rename`

**Read first:**
- `plan/principles.md` — **Rule D** (gerund-named app-graph properties are wrong-shape) is the driver. CLI form follows; flag form is the only form (no verb commands).
- `plan/scope-map.md` — Build and Testing are App-level; renaming doesn't change scope.

**Goal:** Apply Rule D to two app properties + their folders + the CLI:

| Form | Today | After |
|------|-------|-------|
| Folder | `App/Build/` | `App/Builder/` |
| Folder | `App/Test/` | `App/Tester/` |
| Namespace | `App.Build` | `App.Builder` |
| Namespace | `App.Test` | `App.Tester` |
| App property | `app.Build` (verb) | `app.Builder` (noun) |
| App property | `app.Testing` (gerund) | `app.Tester` (noun) |
| CLI | `plang build` / `plang --build` | `plang --builder` |
| CLI | `plang --test` | `plang --tester` |

Pure rename — no shape change to any class or method. The internal logic on `App.Build.@this` (now `App.Builder.@this`) and `App.Test.@this` (now `App.Tester.@this`) stays exactly as today.

**Scope:**
- *Included:* folder relocations (2); namespace renames (App.Build → App.Builder, App.Test → App.Tester); App property renames (`Build` → `Builder`, `Testing` → `Tester`); CLI parameter normalization at `Executor.cs:34` and `RegisterStartupParameters.cs:47`; full caller sweep — ~29 sites referencing `Build` and ~124 sites referencing `Testing` across PLang/ and PLang.Tests/.
- *Excluded:* anything inside the renamed types' surface (methods stay; fields stay). The `Test/{File,Run,Status}.cs` files inside the folder also keep their names — only the folder + namespace + outer property rename. (The "drop Test prefix on inner files" mentioned in earlier tree annotations was already happening in the Test folder; verify on read.)

**Deliverables:**

### Folder + namespace

```
App/Build/                 → App/Builder/
  this.cs                    (namespace App.Build → App.Builder)
  this.Snapshot.cs           (same)

App/Test/                  → App/Tester/
  this.cs                    (namespace App.Test → App.Tester)
  this.Snapshot.cs           (same)
  Coverage.cs                (same)
  File.cs                    (same; namespace updates)
  Results.cs                 (same)
  Run.cs                     (same)
  Status.cs                  (same)
```

### App property changes

`PLang/App/this.cs`:

```csharp
// Today (line 181, 186):
public Testing Testing { get; }
public global::App.Build.@this Build { get; }

// After:
public Tester Tester { get; }
public global::App.Builder.@this Builder { get; }
```

(The `Testing` type alias from `GlobalUsings.cs` if any may need updating to `Tester`. Verify.)

### Field-init or ctor allocations

Any place in App's ctor that does `Build = new Build.@this(this)` or `Testing = new Testing(this)` updates to `Builder = new Builder.@this(this)` and `Tester = new Tester(this)`.

`Build.IsEnabled` / `Testing.IsEnabled` checks throughout the codebase (e.g., `App.Start`'s `if (Build.IsEnabled)`) become `Builder.IsEnabled` / `Tester.IsEnabled`.

### CLI normalization

`PLang/Executor.cs:33-35`:

```csharp
// Today:
// Normalize: "build" or "--build" both become the --build flag
if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
    args = ["--build", .. args[1..]];

// After:
// Normalize: "build" or "--builder" both become the --builder flag (legacy support)
if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
    args = ["--builder", .. args[1..]];
```

`PLang/App/Utils/RegisterStartupParameters.cs:47`:

```csharp
// Today:
var build = args.FirstOrDefault(p => p == "build" || p.StartsWith("--build")) != null;

// After:
var build = args.FirstOrDefault(p => p == "build" || p.StartsWith("--builder")) != null;
```

(Keeps `plang build` working as legacy syntactic sugar that normalizes to `--builder`. Per Rule D the flag form is the canonical surface; the verb-form is preserved as ergonomics so existing `plang build` invocations don't break.)

For `--test` → `--tester`, similar pattern in the testing-flag site (find the analogous line that handles `--test`).

### Internal namespace updates

Files inside the renamed folders all need `namespace App.Build` → `namespace App.Builder` and `namespace App.Test` → `namespace App.Tester`.

Cross-references `using App.Build;` → `using App.Builder;` and `using App.Test;` → `using App.Tester;` across PLang/ and PLang.Tests/. The grep counts (29 Build + 124 Testing) capture both namespace usages and property accesses.

### Caller sweep verification

After the sweep:
- `grep -rn "App\.Build\b\|app\.Build\b" PLang/ PLang.Tests/ --include='*.cs'` — zero hits (other than `App.Builder` matches, which are the new form).
- `grep -rn "App\.Test\b\|app\.Testing\b" PLang/ PLang.Tests/ --include='*.cs'` — zero hits.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild — note: the test invocation itself becomes `plang --tester` after this stage. Update the `Tests/CLAUDE.md` file's test-run command to match. Plus update any `.bot/`-internal test-run docs/scripts.
- `find PLang/App/Build -type f` — empty (folder gone).
- `find PLang/App/Test -type f` — empty (folder gone).
- `app.Builder` and `app.Tester` exist and resolve.
- `plang --builder my.goal` and `plang --tester` work.

**Dependencies:** None. Pure rename.

## Design

### The smell this closes

**Rule D — gerund-named app-graph properties.** `app.Testing` reads "the system is currently testing" — that's a state. `app.Tester` reads "the thing that runs tests" — that's an object. The latter is OBP-shaped; Tester is the navigable subsystem that owns testing concerns. The class definition `public sealed partial class @this` already sits in a folder named `Test/` — but the property name on App made it sound stateful.

`app.Build` is a verb — an imperative imperative ("Build!") — not a noun. `app.Builder` is "the builder subsystem." Same reshape.

CLI follows: the only form is flag; flag lives on the noun.

### Files touched

**Folders renamed (2):** Build/, Test/.

**Files modified across the rename:** ~9 files inside renamed folders for namespace + ~150 caller files across PLang/ and PLang.Tests/ for namespace and property updates. Plus 2 CLI normalization sites.

### Risk + dependencies

**Risk: low-medium.** Pure rename — no shape changes; build break catches any caller miss. The biggest risk is the ~124 testing-related callers — many are in test files that may also have file-level renames pending (e.g., `Test/TestFile.cs` may rename to `Tester/File.cs` per earlier tree annotations). Read the existing files before touching to avoid double-renaming.

Possible failure modes:
1. **`Tests/CLAUDE.md`'s test-run command** uses `plang --test`. Update to `plang --tester`. This file is on disk under `/workspace/plang/Tests/CLAUDE.md`.
2. **The architect/coder/analyzer .bot/ files** reference test-run commands. Update where you find them.
3. **PLang `.goal` files** that invoke `plang build` or similar — keep working via the legacy normalization at Executor.cs:34. No PLang code change needed.
4. **The internal "Test prefix dropped" annotation** in tree comments — verify on file read. If `Test/TestFile.cs` exists today, the rename to `Tester/File.cs` should drop both the folder rename AND the "Test" prefix on the file. If `Test/File.cs` exists today (already prefix-dropped), only the folder rename applies.

**Dependencies: none.**

## Watch for (coder eyes-on)

- **Type alias on `Testing`** in GlobalUsings.cs or similar — if it exists, update the alias.
- **The `[Choices]` attribute callers** if any (PLang choices that reference Build or Testing modes) — sweep.
- **`app.Build.IsEnabled` reads everywhere** — they become `app.Builder.IsEnabled`. Same shape, just renamed property.
- **The `BuildCancelled` / `NoAppFound` error codes** in stage 12's extracted `Build.@this.RunAsync` — error-code strings stay; they're stable identifiers.
- **PLang `.goal` files in `system/builder/`** — pre-built `.pr` snapshots may reference `build` mode. The legacy CLI normalization handles `plang build` → `plang --builder`; verify the pre-built snapshots don't need rebuild.
- **The `engine.Build`/`engine.Testing` patterns in tests** — `PLang.Tests/App/Core/PrPipelineTests.cs` and similar use `engine.Variables` / `engine.Context` already (those become `engine.User.Context` per stage 22 — out of scope here). The Build/Testing references rename mechanically.

## Stages that follow this one

- **Stage 21** (`navigators-to-variables`) — same Tier 4 batch; independent (different folder).
- **Stage 22** (`app-shortcuts-drop`) — separately carved next round.
- Stages 15, 16, 18, 19 each merit own session.

## Out of scope

- Anything inside the renamed types — methods, fields, internal logic stay.
- The `app.Variables` / `app.Context` shortcut removal — stage 22.
- Renaming individual files inside `Test/` (the "Test prefix" drop) — only happens if the prefix is still there; verify on read.

## Commit plan

```
runtime2-cleanup stage 17: Build → Builder, Testing → Tester (Rule D)

Rule D — gerund-named app-graph properties are wrong-shape. app.Testing
reads as a state ("the system is currently testing"); app.Tester is
the object that runs tests. Folder name follows the property; CLI
flag lives on the noun.

Folder renames (2):
  App/Build/   → App/Builder/
  App/Test/    → App/Tester/

Namespace renames:
  App.Build  → App.Builder
  App.Test   → App.Tester

App property renames:
  app.Build (verb)    → app.Builder
  app.Testing (gerund) → app.Tester

CLI:
  plang build / --build → --builder (with `plang build` legacy
                                     normalization preserved)
  plang --test          → --tester

~9 files in renamed folders + ~150 caller sweeps across PLang/ and
PLang.Tests/. Pure rename — no shape changes to types or methods.
Tests/CLAUDE.md test-run command updated.
```
