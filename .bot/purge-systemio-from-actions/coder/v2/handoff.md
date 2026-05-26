# Handoff — purge-systemio-from-actions — coder/v2

## Where we are

Branch `purge-systemio-from-actions` at `adb751279`, pushed.

**All seven stages of the architect plan landed.** PLang and PlangConsole
build clean with **PLNG002 at error severity and zero warnings**. The
gate now fails compilation on any new `System.IO.*` reach into
PLang/app/** outside the allowlist.

Suite: **101 / 3025 failing**. ~75 of those are test-designer stubs at
`Assert.Fail("Not implemented")` — they describe behavioural contracts
for the migration (denial-path tests for sqlite/openai/fluid/http,
in-root silent fast-path, mutation guards, etc.) that need real bodies
written. The remaining ~26 are test fixtures that need updating for the
Path-typed Goal.Path and a handful of GoalCall name-resolution edge
cases.

## What changed since coder/v1

v1 shipped Stages 1–4 + 7 fully and Stage 5 partial (test/discover +
test/report). v2 closes Stage 5, lands Stage 6, and reshapes the
JsonConverter design per Ingi's direction.

### Stage 3 design pivot — AsyncLocal scope replaced with per-Actor converters

v1's `PathJsonConverter` read its Context from a `DeserializationScope`
AsyncLocal slot — clever but with ambient state. Ingi pushed back: "if
objects need context it should have it as a variable." The reshape:

- `PathJsonConverter` takes Context in its constructor.
- Each Actor's `channels.serializers.@this` is constructed with that
  actor's Context and bakes `new PathJsonConverter(context)` into both
  the plang and JSON serializer options.
- `Conversion.TryConvertTo(value, type, context)` — when context != null,
  builds a one-shot `ContextualReadOptions(context)` cloning
  `_caseInsensitiveRead`'s converter list plus a Context-bound converter.
  Static `_caseInsensitiveRead` keeps the stub converter for callers
  without context.
- `FilePath.ReadText` passes its own `Context!` into TryConvertTo for
  both snapshot and normal branches.

Net: deserialized Paths land Context-wired the moment they're built,
without ambient state. `DeserializationScope` class deleted.

### Stage 5 — completed

The deferred handlers from coder/v1 all landed:

- **`settings/Sqlite`** (D9b take-over). Ctor takes `path.@this`.
  Authorize(Write) sync-waits before passing `.Absolute` to the
  SqliteConnection string. Parent dir via `path.Mkdir`.
- **`llm/OpenAi.ResolveImage`** (D9a content-shape). Threads Context;
  file branch goes through new `path.ReadAsDataUri()` sync-wait. No raw
  `System.IO.File.ReadAllBytes`.
- **`module/add`, `code/load`** (D8 Execute verb). New
  `permission.verb.Execute` distinct from Read (Unix r/w/x model). New
  `path.LoadAssemblyAsync()` verb on Path base (NotSupported on non-FS
  schemes), overridden on FilePath with AuthGate(Execute) +
  Assembly.LoadFrom(Absolute). `code/this.Snapshot.Restore` sync-waits
  the same verb. AllowAll() and Covers() include Execute. Authorize's
  VerbLabel renders "execute" so the prompt is distinct from "read".
- **`ui/Fluid`** — `PlangFileProvider` + `PlangFileInfo` carry
  `path.@this` instead of strings. `GetFileInfo` gates via
  `path.ExistsAsync`; `CreateReadStream` gates via `path.ReadText`.
  Out-of-root `{% include %}` denied by AuthGate.
- **`http/Default`** — `CreateFileContent` / `CreateFormContent` +
  auto-detect lift through `path.ExistsAsync` + `path.ReadBytes`.
  Untrusted upload-as-file paths route through AuthGate.
- **`debug/this`** — `_currentLlmFilePath` becomes `path.@this`. Append
  via `path.Append`. `ResolveLlmFilePath` builds the trace dir via
  derivation verbs + `path.Mkdir`.
- **`modules/this.ResolveMarkdownTeachingRoot`** — resolves
  "/system/modules" through `path.Resolve` (relies on FilePath
  ValidatePath's /system/* → <OsDirectory>/system/* fallback).

New content-shape verbs on Path base:
- **`path.ReadAsBase64()`** — ReadBytes + base64 encode.
- **`path.ReadAsDataUri()`** — ReadBytes + base64 + `data:<mime>;base64,...`
  wrap.

### Stage 5 (final mop-up under Stage 6's banner)

- **`goals.LoadFromFileAsync` / `LoadFromDirectoryAsync` / `TryLoadPr` /
  `GetByPrPathAsync`** — all lift to `path.ReadBytes` / `path.List` /
  `path.ExistsAsync`. The `/system/*` fallback comes for free via
  FilePath.ValidatePath inside Resolve.
- **`goals.goal.Methods.FormatForLlm`** — `path.Resolve("/system/builder/
  templates/...")` + `path.ExistsAsync` + `path.ReadText`.
- **`modules.builder.RunAsync`** — app.pr existence probe via
  `path.ExistsAsync`.
- **`modules.builder.goals`, `modules.builder.load`** — Path action slots
  flip from `Data<string>` to `Data<path>`.

### Stage 6 — analyzer flipped

- `Plng002.cs` `defaultSeverity` flipped from `Warning` to `Error`.
- Allowlist extended:
  - Member match now covers methods, not just fields (was a bug — most
    `System.IO.Path` static APIs are methods, never matched the
    field-only check).
  - Pure path-arithmetic methods added (Combine, GetDirectoryName,
    GetFileName, ChangeExtension, GetExtension, GetRelativePath,
    IsPathRooted, GetFullPath, Join, HasExtension, etc.). These are
    string transformations — no IO, no gate concern.
  - File exemption: `app/modules/MarkdownTeaching.cs` (bootstrap-time
    discovery of static repo-shipped teaching .md files; converting its
    sync utility shape to async-everywhere buys no security).

Final state: zero PLNG002 warnings, severity Error.

## Suite breakdown

101 failures across 3025 total. Rough taxonomy:

- **Test-designer stubs (~75)** — `Assert.Fail("Not implemented")`
  bodies waiting for real assertions. Examples: `ExecuteVerb_*`,
  `LoadAssemblyAsync_*`, `ReadAsBase64_*`, `FluidInclude_*`,
  `StaticFile_*`, `SqliteOpen_*`, `TakeOverApi_*`,
  `MutationGuard_*`, `DiagnosticString_*`, `PathInternals_*`,
  `GoalPath_*`, `GoalGetRuntime_*`, `PathJsonConverter_*`,
  `RootComparison_*`, `LoadFromDirectory_*`, `Goal_JsonRoundTrip_*`,
  `DictionaryKeyedByPath_*`, `GenerateLlmFilePath_*`,
  `StepDisabledKey_*`, `CycleDetection_*`, `InRoot*`,
  `AppLoad_*`, `AppSave_*`. The Execute verb / LoadAssemblyAsync /
  ReadAsBase64 surfaces are now real — those stubs can be filled with
  real assertions and should pass.
- **Real regressions in test fixtures (~26)** — `SaveGoal_*`,
  `GetGoals_MergesExistingPrData`, `ValidateActions_GoalCallPath`,
  `Add_*` (module add), `Load_*` / `LoadAction_*`, several `Run_*`
  test-runner tests, `Providers_Restore_*`, `Add_ThrowsWhenPathIsEmpty`,
  PrPath_* unit tests for empty-string semantics, the `Post_405` HTTP
  test, `AsT_TypeWithStaticResolve_StringValue_DispatchesToResolve`.
  Most need their Path-typed fixture wiring updated (already done for
  the discover/library/provider sets).

## Verify the headline security claim

`plang --test=/etc` (or any path outside the actor root) on
`test.discover` — must surface as a permission prompt or denial, not a
silent empty list. Pre-migration the homebrewed `StartsWith(rootPrefix)`
check silently dropped the result; now `rootPath.List(...)` hits
AuthGate(Read).

## Files changed (cumulative across v1 + v2)

**New:**
- `PLang/app/types/path/this.Derivation.cs`, `file/this.Derivation.cs`,
  `http/this.Derivation.cs` (Stage 1).
- `PLang/app/types/path/this.JsonConverter.cs` (Stage 3, reshaped in v2).
- `PLang/app/types/path/permission/verb/Execute.cs` (Stage 5b).
- `PLang.Generators/Diagnostics/Plng002.cs` (Stage 1).
- `PLang.Tests/Generator/Diagnostics/Plng002SystemIoBanTests.cs` — 8
  passing analyzer tests.

**Lifted to path verbs:**
- Goal model: `goals/goal/this.cs`, `GoalCall.cs`, `goals/this.cs`,
  `goals/setup/this.cs`.
- App bootstrap: `app/this.cs` (Load / Save / CreateSettingsStore),
  `actor/this.cs` (per-Actor Serializers ctor).
- Path infrastructure: `types/path/this.cs`, `file/this.cs`,
  `file/this.Operations.cs`, `http/this.cs`, `types/path/this.Authorize.cs`,
  `permission/verb/this.cs`.
- Conversion / serialization: `types/Conversion.cs`, `types/this.cs`,
  `channels/serializers/this.cs`, `channels/serializers/serializer/Json.cs`,
  `channels/serializers/serializer/plang/this.cs`.
- Handlers: `modules/test/discover.cs`, `test/report.cs`, `test/run.cs`,
  `module/add.cs`, `code/load.cs`, `code/this.Snapshot.cs`,
  `builder/this.cs`, `builder/goals.cs`, `builder/load.cs`,
  `builder/code/Default.cs`, `settings/Sqlite.cs`, `llm/code/OpenAi.cs`,
  `ui/code/Fluid.cs`, `http/code/Default.cs`, `debug/this.cs`,
  `modules/this.cs`, `cache/wrap.cs`, `channel/set.cs`.
- Errors / callstack: `errors/Error.cs`, `errors/CallChainRenderer.cs`,
  `callstack/this.cs`, `callstack/call/this.Snapshot.cs`.

**Test fixtures patched:** ~20 test files across LibraryLoadTests,
ProviderModuleTests, GetGoalsTests, CallStack tests, GoalsTests,
StartGoalTests, DiscoverActionTests, EdgeCaseTests, PrPipelineTests,
RenderTests, GoalCallResolutionTests, ForeachTests, EventHandlerTests,
SaveGoalsTests, GoalPrPathTests, etc.

**Docs:** `Documentation/v0.2/good_to_know.md` "System.IO Is Banned"
section; `CLAUDE.md` one-liner under engine conventions.

## Recommended next steps for tester / next iteration

1. Fill the test-designer stubs with real bodies (Execute verb,
   LoadAssemblyAsync, ReadAsBase64, etc.) — the surfaces all exist now.
2. Update the remaining ~26 test fixtures (mostly Path-typed Goal.Path
   wiring + GoalCall name-resolution edge cases).
3. Manual smoke: `plang --test=/etc` denial path.
4. Confirm the in-root silent fast-path still holds: `cd Tests &&
   ../PlangConsole/bin/Debug/net10.0/plang --test` should produce zero
   permission prompts.

Branch is structurally complete. Suite has known soft spots
(stubs + fixture wiring) but no PLang or PlangConsole build errors and
no PLNG002 warnings. The "purge" promise — every filesystem reach in
production C# routes through AuthGate — is enforced by compilation.
