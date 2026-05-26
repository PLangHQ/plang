# Plan — purge `System.IO` from PLang (Path becomes the type, string is the perimeter)

**Branch:** `purge-systemio-from-actions`
**Author:** Claude (architect bot) — 2026-05-25
**For:** Ingi (review), then coder bot

## TL;DR

The brief framed this as "stop action handlers calling `System.IO`." Reading the code, the real problem is upstream: paths are stored as `string` across the Goal model, GoalCall, AppGoals, App, Builder snapshot, and ~13 action handlers. Every `string` path is a place that misses `FilePath.AuthGate`. The fix is one structural rule applied everywhere it currently isn't:

> **In production C# under `PLang/**`, a filesystem path is `app.types.path.@this` (alias `Path`). `string` only appears at the perimeter (CLI args, JSON-on-disk, scheme-resolved DLL loads). Crossing the perimeter into memory means calling `path.Resolve(raw, context)` — never holding the raw string.**

The audit found three rings: the load-bearing typing in Goal/GoalCall/AppGoals/App (Ring 1), the action-handler parameters (Ring 2), and the long tail of utilities/error types (Ring 3). Ring 1 is the cascade — everything downstream gets cleaner once it lands. The discover.cs concrete case is the visible Ring 2 instance.

## What already works (don't touch)

- **`Path` (`app.types.path.@this`)** — abstract base, Context-wired, with `AuthGate` (`this.Authorize.cs:29`) that fast-passes in-root and prompts/denies out-of-root. Has the pure-derivation accessors `FileName`, `Directory`, `Extension`, `Relative`, `Absolute`, `MimeType`, `IsFile`, `IsDirectory` (`this.cs:117–125`). Has a settable `Content` slot for read-result staging (`this.cs:153`).
- **`FilePath` (`app.types.path.file.@this`)** — concrete file-scheme. Verb surface: `ReadText`, `ReadBytes`, `WriteText`, `WriteBytes`, `Append`, `Mkdir`, `Delete`, `List`, `Stat`, `MoveTo`, `CopyTo`, `Save`, `ExistsAsync`, `AsBooleanAsync`. Every method's first line is `AuthGate(verb)` (`file/this.Operations.cs:38, 101, 107, 130, 149, 167, 182, …`).
- **`path.Resolve(string, context)`** — scheme registry entry point (`this.cs:83`). Pickup is `Conversion.cs:212–233`, so `data.@this<path>` parameter binding already turns a step-parameter string into a `FilePath`. Action handlers do not need to call `Resolve` themselves — the parameter binder does.
- **`FilePath.ReadText`** already MIME-deserializes the content. `.pr` files come back as `Goal` (via `TryConvertTo` against the MIME's CLR type, `file/this.Operations.cs:51`). The `.pr` builder-snapshot indirection (`file/this.Operations.cs:42–57`) is preserved.
- **Eight action handlers** already use `data.@this<path>`: `file/read.cs`, `file/save.cs`, `file/delete.cs`, `file/list.cs`, `file/copy.cs`, `file/move.cs`, `file/exists.cs`, plus the builder action wiring in `builder/code/Default.cs`. These are the canonical patterns.

## What doesn't (the gaps)

### Gap 1 — `Path` has no derivation verbs

`Path` exposes string-shaped accessors (`FileName`, `Directory`, `Extension`) but no way to *produce a new `Path`* from an existing one. Everything that today does string math on `path.Absolute` or `path.Directory` to build a sibling/child/parent path falls back to `System.IO.Path.Combine`. Examples: `Goal.PrPath` getter (`goal/this.cs:104–117`), `Goal.GetRuntimeDirectory` (`goal/this.cs:91–101`), `GoalCall.GetGoalAsync` (`GoalCall.cs:68–114`), `discover.cs:83`. Without derivation verbs the migration stalls.

### Gap 2 — `Goal.Path`, `Goal.PrPath`, `Goal.LoadedFromPrPath`, `GoalCall.PrPath` are `string?`

These are the load-bearing slots. `Goal.Path` is `[Store]`d into `.pr` JSON (`goal/this.cs:73`) — serialization format dependency. `Goal.PrPath` is a derived getter doing string math (`goal/this.cs:104–117`) with an init-only no-op setter (a "compile errors instead of silent no-op" comment that doesn't apply once it becomes a real Path). `Goal.LoadedFromPrPath` is a runtime stash (`goal/this.cs:82`). `GoalCall.PrPath` is `[Store]`d in `.pr` JSON and authoritative when set (`GoalCall.cs:32`).

### Gap 3 — `AppGoals` walks the filesystem directly

`goals/this.cs:115–190` does `Path.Combine` + `File.Exists` + `Directory.GetFiles` (`LoadFromDirectoryAsync` at `:369`). `LoadFromFileAsync` does `File.ReadAllTextAsync` (`:317`). `GetByPrPathAsync` does `Path.IsPathRooted` + `Path.Combine` + `File.Exists` (`:286–293`). This is the runtime's *own* goal loader — outside the action-handler ring entirely, but the same rule applies.

### Gap 4 — `App.Load` / `App.Save` for `.build/app.pr` bypass the gate

`app/this.cs:361–408` reads/writes `app.pr` directly via `System.IO.File`. In-root, so the gate would auto-pass, but the rule is *route through the gate*, not "decide for yourself."

### Gap 5 — `.goal` MIME not registered

`FilePath.ReadText` MIME-deserializes `.pr` to `Goal`. There's no equivalent for `.goal` → `Goal` (which requires `Goal.Parse(text, relativePath)` — needs the file's relative path). Without this, "read a goal file → get a Goal object" requires the caller to call `Goal.Parse` after `ReadText`.

### Gap 6 — Action handlers (Ring 2)

`data.@this<string> Path { get; init; }` in `module/add.cs:9`, `code/load.cs:16`, `builder/goals.cs:9`, `test/discover.cs:26`. Each one calls `System.IO.*` on `Path.Value` directly. The fix is the canonical `data.@this<path>` swap, then verb-surface use.

### Gap 7 — Provider implementations and infrastructure (Ring 3)

`llm/code/OpenAi.cs:658–662` (image attachment), `ui/code/Fluid.cs:260–329` (Fluid template `IFileProvider`), `http/code/Default.cs:1027–1081` (HTTP static file serving), `debug/this.cs:401, 457–475` (LLM trace files), `code/this.Snapshot.cs:97–98` (DLL existence check), `settings/Sqlite.cs:27–31` (sqlite DB file path), `modules/this.cs:240` (markdown teaching dir), `test/report.cs:39–54` (report writer), `builder/code/Default.cs:150–151` (CLI `--build={…}` arg normalization). Each fixed in isolation; they don't cascade.

### Gap 8 — There is no analyzer / build-time gate

The Console ban (`good_to_know.md`) is enforced by convention + grep + reviewer eyes. Same for the in-progress `System.IO` ban. Without a Roslyn diagnostic that flags `System.IO.*` and `string` path parameters under `PLang/app/modules/**` and `PLang/app/goals/**`, regressions will land. The source generator's PLNG001 gate is the natural place to extend.

## Migration shape

**Perimeter / interior split.**

- **Perimeter (string survives)**: CLI args (`startupDirectory` in `Executor.cs:8`), JSON-on-disk shape for serialized `Path` properties, scheme-registry entry argument, `App.AbsolutePath` itself (the root *definition* — can't be a Path because Paths anchor against it; circular). `App.OsDirectory`, `App.OsAbsolutePath` — same boot anchor.
- **Interior (Path)**: everything else. Goal model, GoalCall, AppGoals lookups, Builder snapshot, action-handler parameters, provider intermediates.

**Crossing rule.** A string crosses the perimeter into the interior exactly once, via `path.Resolve(rawString, context)`. After that, it's a `Path` and stays one. The reverse crossing (writing JSON, calling a third-party API that wants a string) reaches for `.Relative` (portable form, for serialization) or `.Absolute` (OS form, for third-party APIs). Both accessors are public; the lift goes through `Resolve`.

**Serialization.** A `Path` property marked `[Store]` serializes as its `.Relative` string (portable across roots). Deserialization through the actor's serializer (`channels.serializers`) creates a `Path` with `Raw` set but `Context = null` (no Context available in the converter), and the existing back-reference pass that sets `Goal.App`, `step.Goal` (`GoalCall.cs:131–141`) extends to wire `Path.Context` for every Path field in the freshly-loaded tree. This matches the existing wiring pattern — no new lifecycle.

**Equality / dictionary keys.** Path already implements `Equals` / `GetHashCode` on `_absolutePath` with `RootComparison` (`this.cs:167–175`). Dictionaries key directly on `Path` — no reach into `.Relative` from call sites. That's the OBP shape: Path knows how to be a dictionary key, callers don't decompose it. Fuzzy `Get(name)` over bare goal names stays a separate by-name index (Linq scan or a `Dictionary<string, Path>` from-name-to-key) because name matching isn't a Path-equality question. (Ingi C1)

## Concrete design decisions (the ones Ingi needs to confirm or correct)

### D1 — Path derivation verbs

Add to `app.types.path.@this` (all pure string transformations — no IO, no async, no auth gate; return a new Path with the same Context, dispatched through the scheme registry so a derived `FilePath` stays a `FilePath`):

- `Path Parent { get; }` — `/Cache/Start.goal` → `/Cache/`. The directory containing this path.
- `Path WithName(string name)` — `/Cache/Start.goal` → `/Cache/{name}`. Swap the filename in place.
- `Path WithExtension(string extension)` — `/Cache/Start.goal` → `/Cache/Start{extension}`. Swap the extension in place. **Not a search** — pure transformation.
- `Path Combine(string child)` — `/Cache/` → `/Cache/{child}`. Append a child path segment.
- `Path InFolder(string folder)` — `/Cache/Start.goal` → `/Cache/{folder}/Start.goal`. Insert a sibling folder between parent dir and filename. (Ingi C2 — general-purpose, not `.build`-specific.)

PLang-specific rules live in PLang model classes, not on the generic Path type. The PrPath derivation reads cleanly with just these verbs:

```csharp
public path? PrPath => Path?.Parent
    ?.Combine(".build")
    ?.Combine(Path.FileNameWithoutExtension.ToLowerInvariant() + ".pr");
```

(Ingi C2 confirmed: `InBuildFolder()` shorthand dropped; lowercase + extension swap is Goal's business.)

### D2 — `.goal` MIME → Goal deserialization (replicate the `.pr` pattern)

`.pr` files already MIME-deserialize to `Goal` via `TryConvertTo` in `FilePath.ReadText` (`file/this.Operations.cs:51, 82`). Replicate the same shape for `.goal`:

- Register `.goal` in the MIME map → `Goal`.
- The converter calls `Goal.Parse(text, relativePath)`. The relative path isn't in the converter signature today; rather than add it, **`FilePath.ReadText` stamps `goal.Path = this` after the converter returns** (`.pr` deserialization has analogous post-conversion wiring — `GoalCall.LoadFromFile` stamps `LoadedFromPrPath`/`App`/`step.Goal` at `:131–149`). Same post-deserialize back-reference pattern, just one more line.

(Ingi C3 — settled. Don't add a path-aware converter overload, don't overthink.)

### D3 — `Goal.Path: Path` and friends

Migrate in one commit (the typing change is atomic — partial state would be more confusing than the cutover):

- `Goal.Path: string?` → `Goal.Path: path?` (`goal/this.cs:73`)
- `Goal.PrPath: string?` → `Goal.PrPath: path?`, derived getter becomes `Path?.InBuildFolder()` (`goal/this.cs:104`)
- `Goal.LoadedFromPrPath: string?` → `Goal.LoadedFromPrPath: path?` (`goal/this.cs:82`)
- `Goal.GetRuntimeDirectory(): string?` → `Goal.GetRuntimeDirectory(): path?` — becomes `LoadedFromPrPath?.Parent?.Parent` (`goal/this.cs:91`)
- `Goal.FolderPath: string` → `Goal.FolderPath: path` — becomes `Path?.Parent` (`goal/this.cs:163`)
- `GoalCall.PrPath: string?` → `GoalCall.PrPath: path?` (`GoalCall.cs:32`)

`Goal.FullPath` (`goal/this.cs:197`) stays `string` — it's a goal-name chain (`Parent/Child/Grandchild`), not a filesystem path.

### D4 — `AppGoals` dictionaries keyed by `Path`

`_goals: ConcurrentDictionary<Path, Goal>` and `_byPath: ConcurrentDictionary<Path, Goal>` (`goals/this.cs:15–16`) become Path-keyed. Path's own `Equals`/`GetHashCode` (`this.cs:167–175`) handles the keying — no `.Relative` decomposition at call sites. (Ingi C1 — OBP.)

Fuzzy `Get(name)` over bare goal names (e.g. `Get("ProcessData")`) is *not* a Path equality question. Either the caller lifts `name → Path` via `path.Resolve(name, context)` for a clean dict hit, or AppGoals keeps a separate by-name index (`Dictionary<string, Path>` mapping goal name → key). The latter is simpler and matches today's variation-matching behaviour (`goals/this.cs:65–89`).

**Future work (Ingi C4, out of scope this branch):** `_goals` collapses into a Cache module read so goal lookup goes through the same cache infrastructure as everything else. Noted in `todos.md`; deferred — other things block it now.

### D5 — `AppGoals` filesystem ops go through Path verbs

`LoadFromFileAsync`, `TryLoadPr`, `GetByPrPathAsync`, `LoadFromDirectoryAsync` all rewrite to use `path.Resolve(...)` + `path.ReadText()` + `path.List(...)`. `goals/this.cs:115–190, 274–302, 307–358, 363–385`. The `<root>/system/` → `<os>/system/` fallback (`ValidatePath` lines 73–81) stays in `FilePath.Resolve` where it already lives — AppGoals doesn't need to know.

### D6 — `App.Load` / `App.Save` for `app.pr` use Path verbs

`app/this.cs:361–408`. Lift to `path.Resolve(".build/app.pr", context)` + `path.ReadText()` / `path.Save(...)`. Bootstrap timing: this runs before `Goals` is loaded; the App-construction context is available. Verify the in-root gate fires correctly during bootstrap.

### D7 — `App.AbsolutePath` stays `string`

It's the root anchor. Lifting it to `Path` is circular (Path needs an actor-context → App → root). `App.OsDirectory` and `App.OsAbsolutePath` (`app/this.cs:64, 71, 79`) likewise stay `string`. These three are the only "outside the rule" sites, and they're the *definition* of the rule's boundary.

### D8 — Provider DLLs (`module.add`, `code.load`, `code.Snapshot`) + `Execute` verb

These load assemblies. Two pieces:

1. **New `Execute` verb** alongside `Read`/`Write`/`Delete` under `app.types.path.permission.verb` (the Linux model: read/write/execute are distinct grants). The Authorize prompt becomes "Allow X to execute Y" — meaningfully different from "read Y." (Ingi C5 confirmed.)
2. **`path.LoadAssemblyAsync()` verb on Path** — `AuthGate(Verb { Execute = … })` + `Assembly.LoadFrom(this.Absolute)`. The handler holds `data.@this<path>` and calls `Path.Value!.LoadAssemblyAsync()`. The handler never reaches for `.Absolute` directly — the verb owns that escape.

### D9 — Third-party APIs that demand a string path (Ingi C6)

**Critical insight.** Path construction does **not** call AuthGate — only verbs do. So:

```csharp
// `c:\temp\my.sqlite` is outside the actor's root
var p = path.Resolve(@"c:\temp\my.sqlite", ctx);
var abs = p.Absolute;          // ← returns the absolute string — NO PROMPT, NO DENIAL
sqliteOpen(abs);                // ← gate bypassed entirely
```

Anyone reaching for `.Absolute` to hand to a third-party library skips the gate. Two flavours need distinguishing:

**(D9a) Content-shape APIs — add a verb on Path.** When the third-party API wants the file's *content* in a specific shape (base64, data URI, byte stream, parsed JSON, …), the verb lives on Path so AuthGate fires inside, and the handler never sees `.Absolute`:

- `path.ReadAsBase64()` — bytes → base64 string. OpenAI image attachments (Ingi C9).
- `path.ReadAsDataUri()` — bytes + MIME → `data:` URI. Embedded image tags, mail attachments.
- Future content-shape verbs follow the same naming.

Handler shape (replaces `llm/code/OpenAi.cs:658–662`):

```csharp
var image = action.ImagePath.Value;     // data.@this<path>
var base64 = await image.ReadAsBase64(); // gate fires, returns Data<string>
if (!base64.Success) return base64;
openaiRequest.Content = base64.Value;
```

**(D9b) Take-over APIs — `Authorize` before `.Absolute`.** When the third-party library wants *the file path itself* (because it does its own opening, locking, or memory-mapping — sqlite, audio players, image libraries that take file paths, watchers), the handler must explicitly authorize first:

```csharp
var auth = await dbPath.Authorize(new Verb { Write = new Write() });
if (!auth.Success) return auth;       // out-of-root denial bubbles up
sqliteOpen(dbPath.Absolute);          // safe — gate already passed
```

No new API needed for D9b; `Authorize` is already public (`this.Authorize.cs:29`). DLL loading is the auth-shaped version of D9b and uses the dedicated `Execute` verb (D8) — `path.LoadAssemblyAsync()` does the gate + LoadFrom internally.

Apply: D9a → `llm/code/OpenAi.cs` (image attachments). D9b → `settings/Sqlite.cs`. The migration prefers D9a where possible — content-shape verbs are safer because they don't expose `.Absolute` to the handler at all.

See D13 for the discipline rule that flags `.Absolute` reaches outside `app.types.path.**`.

### D10 — `ui/code/Fluid.cs` and `http/code/Default.cs` (file-serving providers)

Both implement third-party "file provider" interfaces (`IFileProvider`-shaped). They wrap incoming string paths into `FilePath` internally and gate via `ReadText`/`ReadBytes`. Fluid's `PlangFileInfo` (`Fluid.cs:314`) gets a `Path` field instead of a string; `Read()` calls `path.ReadText()`. Same shape for `http/code/Default.cs`'s static-file branch — this is the most sensitive site (untrusted HTTP input → filesystem read), so it's the highest-value gate in the audit.

### D11 — `debug/this.cs` LLM traces

`debug/this.cs:401, 457–475` writes trace files to `<root>/.build/traces/`. In-root, but bypasses the gate. Lift to `path.WriteText(...)` / `path.Append(...)`. The `GenerateLlmFilePath` helper (`:457`) becomes a chain of Path derivation verbs.

### D13 — `.Absolute` discipline rule (new — Ingi C6 fallout)

Document under `Documentation/v0.2/good_to_know.md` and `CLAUDE.md`:

> **`path.Absolute` is an easy-to-misuse escape hatch.** Any reach for `.Absolute` outside `app.types.path.**` means a third-party API (sqlite, image library, `Assembly.LoadFrom`) is about to touch the filesystem with no gate. The handler MUST `await path.Authorize(verb)` first and check `auth.Success` before reading `.Absolute`. The same applies to `.OsAbsolutePath` when used as a destination string. The verb surface (`ReadText`, `WriteText`, `List`, `Stat`, `ReadBytes`, `ReadAsBase64`, …) does this automatically — reach for verbs first; only fall through to `.Absolute` + manual `Authorize` when a third-party API genuinely takes over the file (D9b).

Permitted exceptions (gate-already-fired or not-an-IO-reach): inside `app.types.path.**` itself (Path verbs use `.Absolute` post-AuthGate by design); inside diagnostic strings (`ToString()`, error messages) that don't read the file.

### D12 — Roslyn analyzer for the ban

Extend the PLang source generator to emit a diagnostic (`PLNG002`?) when production code under `PLang/app/**` (excluding `PLang/app/types/path/**` and Generators) contains:

- `System.IO.File.*`, `System.IO.Directory.*`, `System.IO.FileInfo`, `System.IO.DirectoryInfo`, `System.IO.Path.GetFullPath/Combine/IsPathRooted/GetDirectoryName/GetFileName/GetFileNameWithoutExtension/GetExtension/GetRelativePath/ChangeExtension` calls.
- `data.@this<string>` properties named `Path`, `PrPath`, `Source`, `Destination`, `Directory`, `Folder`, `FilePath` in `[Action]`-annotated classes.

Allowlist: `System.IO.Path.DirectorySeparatorChar` / `AltDirectorySeparatorChar` (separator constants, not IO). The analyzer's allowlist file goes in `PLang.Generators/` and is read at gen-time. Same shape as the existing PLNG001 gate.

## Staged plan

Each stage lands as its own commit pair (`coder`/`tester`). The order matters: earlier stages enable later ones.

### Stage 1 — Path derivation verbs (D1) + Roslyn analyzer in warning mode (D12)

Lands two things in parallel:

1. **Derivation verbs** — `Parent`, `WithName`, `WithExtension`, `Combine`, `InFolder` on `app.types.path.@this`. Pure derivations, no IO, no auth.
2. **`PLNG002` Roslyn analyzer** — emits **warnings** (not errors) for every `System.IO.*` call and `data.@this<string>` Path-typed parameter under `PLang/app/**` (excluding `PLang/app/types/path/**` and the App-anchor strings). Build stays green; the warning list doubles as the coder's worklist for the remaining stages. Flipped to **error** in Stage 6 once the migration finishes.

Tests: per-verb on both `FilePath` and a mock scheme. Edge cases: root path (`/`), filename-only, missing extension. Cross-OS separators. Analyzer fires-on-offender / doesn't-fire-on-Path-internals / doesn't-fire-on-allowlist.

Risk: low. New surface; analyzer is warning-only so no build break.

### Stage 2 — `.goal` MIME → Goal (D2)

Lands: `.goal` MIME registration; `FilePath.ReadText` post-conversion goal-Path stamp (option b). The discover handler is the first consumer.

Tests: `path.ReadText()` on a `.goal` returns Data with `Value as Goal` correctly parsed and `Path` stamped. `.test.goal` flows the same way.

Risk: low. The MIME map already supports new types via TypeMapping.cs registration.

### Stage 3 — Goal/GoalCall typing (D3, D4)

Lands: `Goal.Path`, `Goal.PrPath`, `Goal.LoadedFromPrPath`, `Goal.FolderPath`, `Goal.GetRuntimeDirectory`, `GoalCall.PrPath` all become `path?` / `path`. `AppGoals._goals` / `_byPath` keys stay `string` but consumer sites switch to `.Relative` form. JsonConverter for `Path` ↔ relative-string. Back-reference wiring extended to set `Path.Context`.

Tests: round-trip a Goal through `.pr` JSON, verify Path-typed properties reconstitute correctly under a different App root (child App scenario). `Goal.GetRuntimeDirectory()` returns the right Path under root-shift. Cycle-detection `Goal.PrPath` comparison still works.

Risk: **high**. This touches every site that consumes `Goal.Path` or `Goal.PrPath` — the explore agent's full table is the work surface. Build-error driven sweep is the right approach: change the property type, compile, fix every error.

Sub-risks:
- Cycle detection (`callstack.call`, looks up `Goal?.PrPath`) — Path equality vs. string equality.
- Step's `DisabledKey` cache key (`step/this.cs:40`) interpolates `Goal?.PrPath` — interpolation becomes `Goal?.PrPath?.Relative`.
- Error types (`errors/CallbackGoalErrors.cs:12, 33`) carry `string GoalPrPath` — purely informational, can stay string for now (Ring 3 deferred).

### Stage 4 — `AppGoals` and `App.Load`/`Save` (D5, D6)

Lands: `goals/this.cs` and `app/this.cs:361–408` switch to Path verbs. The bootstrap-timing question for `App.Load` (does Context exist before goals load?) gets verified — App.Load runs at the very start, so we may need a "boot context" that has App but no Goal, just enough for Path.Resolve.

Tests: cold start with no `app.pr`, with corrupt `app.pr`, with valid `app.pr`. `AppGoals.LoadFromDirectoryAsync` on a deep `Tests/` tree.

Risk: medium. The bootstrap sequencing is the only real surprise risk.

### Stage 5 — Action handlers (D8, D9, D10, D11, plus the Ring 2 list)

Lands per-handler, smallest-first:

1. `test/discover.cs` (the brief's concrete offender) — full rewrite around `rootPath.List(...)` + `match.ReadText()` + `goal.PrPath.ReadText()`. Hand-rolled `StartsWith(rootPrefix)` containment check (`discover.cs:55–57`) deletes — `AuthGate` does it.
2. `test/report.cs` — path.Save for the report JSON.
3. `module/add.cs`, `code/load.cs`, `code/this.Snapshot.cs` — DLL load via `path.LoadAssemblyAsync()` (D8).
4. `builder/goals.cs` — `data.@this<path> Path` swap.
5. `builder/code/Default.cs:150–151` — already does `path.Resolve` right after; the `GetFullPath`/`Combine` is the entrance preprocessing for CLI input; either lift earlier or accept this is the perimeter for `--build`.
6. `settings/Sqlite.cs` — gate-then-pass-string (D9).
7. `llm/code/OpenAi.cs` — image lift to `path.ReadBytes()`.
8. `ui/code/Fluid.cs`, `http/code/Default.cs` — file-provider wrappers (D10).
9. `debug/this.cs` — trace writes (D11).
10. `modules/this.cs:240` — markdown teaching dir lifted to `path.Resolve`.

Tests per handler: existing test goals continue to pass; one new test per handler that runs *out of root* to verify the auth gate fires (or denies). For `http/Default.cs` specifically: a test with a crafted URL trying to traverse outside the served root must hit AuthGate and be denied.

Risk: medium per handler, low cumulative. Each one is local. Some handlers (Fluid, HTTP) have wider surface area in their tests.

### Stage 6 — Flip the analyzer to error mode (D12)

Lands: the `PLNG002` analyzer (introduced as warnings in Stage 1) flips to **error**. Builds now break on any `System.IO.*` under the gated namespaces. Codebase should be green at this point — if it isn't, those are the misses from Stages 3–5 surfacing as build failures.

Risk: low. The hard work happened earlier; this is the ratchet click.

### Stage 7 — Documentation

Lands: new section in `Documentation/v0.2/good_to_know.md` titled "System.IO Is Banned in Production C# (use `path.@this`)". Mirrors the `Console.*` ban's structure: rule, permitted exceptions (`PathExtension.AdjustPathToOs`, `Path.DirectorySeparatorChar`, the App-anchor strings), test-fixture pattern. Plus a one-line update to `CLAUDE.md` under "Filesystem".

Risk: none (docs).

## Risks not yet covered

- **JSON converter for Path — wired into the existing per-type converter list** (Ingi C7 + C11). The pattern is already there: `serializer/plang/this.cs:29` registers `TimeSpanIso8601` and `data.Json` as per-type `JsonConverter`s on the `JsonSerializerOptions.Converters` list. Path's converter slots in alongside, living in `PLang/app/types/path/this.JsonConverter.cs` next to `this.cs` (OBP — the converter belongs with the type). It fires every time a `.pr` is read from disk — `.pr` files are JSON containing `path`, `prPath`, `loadedFromPrPath`, `goalCall.prPath` fields, and the deserializer needs to turn each string into a Path. The converter has no Context in scope at deserialize time; it sets `_absolutePath` and `Raw` only, leaving Context null. The existing post-deserialize back-reference pass that sets `goal.App` / `step.Goal` extends to also wire `Path.Context` on every Path property in the tree — same lifecycle slot, no new infrastructure. If we end up with several PLang types needing converters, a `PlangJsonConverterFactory` scanning `[PlangType]`-annotated types can replace the manual `Converters` list; for Stage 3 the immediate work is one converter for Path.
- **Performance**: every action-handler `Path` property invocation now goes through `Resolve` → scheme registry → `ValidatePath` → potential `FilePath` construction. Today's `data.@this<string>` is a free string ref. Need to confirm the lazy `data.@this<T>.As<T>(Context)` only resolves once per parameter access. (Per CLAUDE.md "Lazy params": yes, resolves once, cached by source-generator-emitted accessor. Should be fine.)
- **`Path.Context` cycles for serialization**: `Goal.App` is `[JsonIgnore]` to avoid cycles. `Path.Context` is also `[JsonIgnore]` already (`this.cs:74`). Verified.
- **The `Goal.PrPath` `init { }` no-op setter** stays (Ingi C8 — correcting my earlier claim). PrPath is derived from `Path`; the `init {}` exists so JSON round-trip doesn't error when the serialized `prPath` field comes back in — the no-op swallows the value and the getter recomputes from `Path` on read. When PrPath becomes `path?` the same shape holds: getter derives the Path, init no-op swallows. Net: serialized `prPath` is informational (debug aid), value of record is `Path`-derived.
- **`callstack.call` cycle detection by `Goal?.PrPath`** — switches to comparing `Path` (which equals by `Absolute`) or `Path?.Relative` (string compare). Equality semantics need to match today's `OrdinalIgnoreCase`. `Path.Equals` already uses `RootComparison` (`this.cs:167`) which is `OrdinalIgnoreCase` on Windows, `Ordinal` on Linux. Today's string-comparison cycle check uses `OrdinalIgnoreCase` unconditionally. Confirm: is the case-sensitivity on Linux a behaviour change? If yes, it's actually a bug fix (Linux IS case-sensitive at the FS layer), but flag it.

## Audit surface (Ring 1/2/3)

Full list lives in the explore agent's report (rendered up-thread). Distilling here for the coder's work-tracking:

**Ring 1 — cascade origins (Stage 3, 4):**
- `PLang/app/goals/goal/this.cs:73, 82, 91–101, 104–117, 163–177` — Goal typing
- `PLang/app/goals/goal/GoalCall.cs:32, 68–114, 117, 147` — GoalCall + LoadFromFile
- `PLang/app/goals/this.cs:15–16, 36–40, 115–190, 274–302, 307–358, 363–385` — AppGoals
- `PLang/app/this.cs:80, 363–406, 555–556` — App.Load/Save + os anchors
- `PLang/app/modules/builder/this.cs:39, 82, 90` — `_prSnapshot` keying

**Ring 2 — action handlers (Stage 5):**
- `PLang/app/modules/test/discover.cs:26, 43, 55–63, 79–95, 108, 130`
- `PLang/app/modules/test/report.cs:39–54`
- `PLang/app/modules/module/add.cs:9, 15–25`
- `PLang/app/modules/code/load.cs:16, 29, 53`
- `PLang/app/modules/code/this.Snapshot.cs:97–98`
- `PLang/app/modules/builder/goals.cs:9`
- `PLang/app/modules/builder/code/Default.cs:150–151`
- `PLang/app/modules/settings/Sqlite.cs:27–31`
- `PLang/app/modules/llm/code/OpenAi.cs:658–662`
- `PLang/app/modules/ui/code/Fluid.cs:260–329`
- `PLang/app/modules/http/code/Default.cs:1027–1081`
- `PLang/app/modules/debug/this.cs:401, 457–475`
- `PLang/app/modules/this.cs:240`

**Ring 3 — long tail (defer / Stage 5 cleanup):**
- `PLang/app/Utils/PathExtension.cs:11, 15–30` — stays string (the `AdjustPathToOs` utility is used by Path internals)
- `PLang/app/errors/CallbackGoalErrors.cs:12, 33` — stays string (informational)
- `PLang/app/tester/File.cs:14, 17, 26` — could lift; flagged for Stage 5
- `PLang/app/variables/this.cs:738` — `ResolveVariablesInPath(string)`: stays string (string-level substitution, not filesystem op)
- `PLang/Executor.cs:8, 38` — CLI entry; stays string (perimeter)
- `PLang/app/goals/setup/this.cs:49–54` — setup goal loader; lift in Stage 4

## Status of Ingi's review (rounds 1 and 2 — all settled)

- **C1 OBP dict keying** — D4 rewritten. Path-keyed dictionaries; Path's own Equals does the work.
- **C2 Verb semantics** — D1 clarified. WithExtension etc. are transformations, not searches. `InBuildFolder` dropped; `InFolder` kept. PrPath derivation moved into `Goal.PrPath` using the generic verbs.
- **C3 .goal MIME** — D2 settled. Replicate the .pr pattern; FilePath.ReadText stamps goal.Path post-parse.
- **C4 Cache module future** — D4 notes out-of-scope future work for `_goals` → Cache. Adding to todos.md.
- **C5 Execute verb** — confirmed, integrated into D8 (verb taxonomy expansion + `path.LoadAssemblyAsync`).
- **C6 `.Absolute` escape hatch** — D9 restructured. New D13 discipline rule lands.
- **C7 JSON converter trigger** — risk bullet clarified: only after Stage 3, fires on .pr deserialization.
- **C8 PrPath init no-op stays** — corrected; removed the "vestige" claim.
- **C9 `ReadAsBase64` and content-shape verbs** — D9 split into D9a (content-shape verbs on Path) / D9b (take-over APIs needing Authorize + Absolute). OpenAI image migrates to D9a.
- **C10 "footgun" word** — replaced with "easy-to-misuse escape hatch" in D13.
- **C11 PlangConverter** — risk bullet rewritten. Path converter slots into the existing per-type list at `serializer/plang/this.cs:29`, lives in `PLang/app/types/path/this.JsonConverter.cs`. Factory pattern noted as natural next step.
- **C12 Atomic flip (no `PathTyped` bridge)** — confirmed atomic; bridge alternative removed.
- **C13 One branch, no PRs** — confirmed; multi-PR alternative removed.

## Notes for test-designer

This branch is **security work**. Today's suite is mostly positive-path (happy walks, happy reads, happy serialization). The migration's value is in the *denial paths* — gate fires when it should, gate stays silent when it should. Many of those tests don't exist today. Design from the security claims, not from the type changes.

### Test categories, ranked by what only this branch enables

**1. Auth-gate firing on out-of-root reaches (the headline).** Today most handlers can read/write/walk anywhere the OS lets the process touch — `System.IO` doesn't care about roots. Post-migration, every verb routes through `AuthGate`. The tests that prove it works are the ones we don't have:

- `test/discover.cs` with `--test=/etc` or `--test=../../..` (outside actor root) → denial, not silent-empty.
- `file/read.cs`, `file/save.cs`, `file/list.cs` with absolute out-of-root paths → prompt or denial.
- `module/add.cs`, `code/load.cs` with a DLL path outside root → `Execute` denial.
- `http/code/Default.cs` static-file with a crafted URL containing `../` segments → denial (this is the most adversarial surface — untrusted HTTP input).
- `ui/code/Fluid.cs` with an `{% include %}` pointing outside the template root → denial.
- `settings/Sqlite.cs` with a `datasource` path outside root → denial (D9b: the explicit Authorize call must fire before sqlite opens).
- `llm/code/OpenAi.cs` with an image attachment outside root → denial (D9a: `ReadAsBase64` denies).

Each one of these is essentially "the gate did its job." If any of them silently succeed, the migration's security claim is broken.

**2. The `.Absolute` discipline (D13).** Mutation tests catch this best — temporarily remove an `Authorize` call before `.Absolute` in a sqlite-like handler, confirm a test catches the silent bypass. Without a test that fails on the missing Authorize, the rule is just documentation. Worth a fixture-style helper: "given a handler that reaches for `.Absolute` without prior Authorize, is there a test that would catch it?"

**3. In-root *non*-prompting (silent fast-path).** The `IsInRoot()` fast-path (`this.Authorize.cs:35`) auto-grants in-root verbs. Regression risk: an over-strict refactor breaks the fast-path and normal `plang --test` runs start spamming permission prompts. Need a test that asserts: in-root reads do NOT invoke `output.ask`.

**4. Permission prompt y/n/a behaviour.** The actor's answer to an out-of-root prompt has three forks (`this.Authorize.cs:66–73`). Tests need a stub-actor that scripts y/n/a answers and asserts the right downstream behaviour (signed grant persisted, in-memory grant, denial bubbled, `a` covers future reads of the same path).

**5. Derivation verb correctness (D1).** Pure functions, easy to table-test. The interesting cases are edge: root path (`Parent` of `/` → ?), filename without extension (`WithExtension` on `/foo`), `InFolder` on a directory vs. a file, Windows-vs-Linux separators, paths with `.` and `..` segments. Cross-scheme: `Parent` on an `HttpPath` should produce an HttpPath, not silently switch schemes.

**6. JSON round-trip with Path-typed Goal.** Build a goal with a Path-typed `Goal.Path`, serialize to `.pr`, deserialize, verify the Path round-trips and `Context` is wired correctly by the back-reference pass. Edge case: child-App scenario where the `.pr` was built under root A and loaded under root B — `GetRuntimeDirectory()` must resolve correctly under B.

**7. Equality / dictionary keying.** `Path.Equals` is on `_absolutePath` with `RootComparison` (Windows: case-insensitive, Linux: case-sensitive). `_goals[path]` lookups need to round-trip across build → load. Add a test where the on-disk-built path and the runtime-resolved path differ in case (Linux: should be distinct keys; Windows: same key).

**8. Handler-equivalence (existing tests stay green).** Every `.test.goal` that passes today should pass post-migration. Test-designer doesn't add these — they already exist; what's needed is a checklist that they're all run before merge.

### Test infrastructure that probably needs designing

- **Out-of-root test fixture.** How does a test goal configure an actor with a specific root and a scripted answer-stream for the permission prompt? `tester/File.Directory` carries the per-test root today (`tester/File.cs:17`). The y/n/a script probably lives in a new field. Test-designer's call on shape.
- **Roslyn analyzer fixture.** `PLNG002` needs its own tests — fixtures that compile a snippet with `System.IO.File.Exists` under `PLang/app/modules/foo/` and assert the diagnostic fires; fixtures inside `PLang/app/types/path/` and assert it doesn't. The Generators test project (`PLang.Generators.Tests`?) is the natural home.
- **Mutation-test announcements.** Per CLAUDE.md, security-relevant source mutations need announcement. The `.Absolute` discipline check (above) is one such case — tester should expect to do mutation tests on `Authorize` removal.

### What's NOT in test-designer's scope

- The type flips themselves (Goal.Path string → Path). Build errors catch those; no behavioural test adds value.
- Refactoring renames (e.g. `_prSnapshot` keying from string → Path). Compile-time work.
- Pure documentation changes (Stage 7).

### Quick wins (low-effort tests that punch above their weight)

- A single "test/discover with --test=/etc denies" test catches a whole class of regressions across all of discover.cs's internals.
- A single "in-root read does not call output.ask" test catches every variant of "I accidentally made the fast-path conditional" mistake.
- A single round-trip-then-load-under-different-root test catches every Context-wiring miss.

If only three tests get written, those three.

## What's not in scope

- Migrating `App.AbsolutePath` / `App.OsDirectory` away from string (D7 — boot anchors stay).
- Removing `ValidatePath` (`file/this.Validate.cs:30`) — it's a string normalizer used internally by `Resolve`, not a public API. Stays.
- Reworking `data.@this<T>.As<T>` parameter binding — already supports Path via Conversion (`Conversion.cs:212–233`).
- Rewriting the `actor.Permission` model (Permission Find/Add stays as-is; only the gate's callers change).
- Touching the http/HttpPath scheme for HTTP requests (the brief is filesystem-focused; HTTP path is already gated via `AuthGate`).

---

Ingi — please mark up. I expect D2, D8, and Q4 to be the ones with real opinions on them. Everything else is mechanical.
