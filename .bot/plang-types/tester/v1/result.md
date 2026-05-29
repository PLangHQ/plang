# tester v1 — plang-types — VERDICT: FAIL

Reviewed coder v1 (commits `205063c5..35d79348a`): the 7-stage `type + kind` model
— registry fold, kind field, per-(type,format) serializer dispatch, number/image/
code value types, temporal cleanups, runtime DLL loading. codeanalyzer had passed
with 2 minor dead-code findings (both now fixed).

## Test runs (clean rebuild — stale-binary trap honored)

- Wiped all bin/obj, `dotnet build PlangConsole`, then:
- **C#: 3609 / 3609 pass, 0 fail, 0 skip** (`dotnet run --project PLang.Tests`)
- **plang: 246 / 246 pass, 0 fail, 0 skip** (`cd Tests && plang --test`)
- No regressions. All 12 new plang goals run and pass.
- Coverage: 51/54 changed prod files >0% (gaps: 2 interfaces, + `math/number/Config.cs` at 0%).

## Builder validation (per workflow + Ingi's instruction to use cache:false)

Rebuilt graded goals with `plang '--build={"files":[...],"cache":false}'`. The builder
works (my first attempts used the wrong `plang build <goal>` command form — corrected
to the flag form per `cli_reference.md`). All cache:false rebuilds matched the committed
.pr step text to `actions[0].module.action` — **no builder false-greens.** All rebuilt
.pr restored via `git checkout`; tree clean, nothing committed from mutations.

## What's genuinely solid (not findings)

- codeanalyzer #1 (`renderers.IndexAssembly` dead Convert.ChangeType branch) and #2
  (`number.DoDivide` dead policy conditional) are both cleanly collapsed to a single
  delegate / unconditional Decimal. Verified in source.
- `NumberDivideTests` / `NumberArithmeticTests`: pin value, `Kind`, and `Error.Key`
  (`DivideByZero`, `MathOverflow`) — not just `!Success`. Honest.
- `PathSerializerMigrationTests.Path_Wire_ByteForByteParity`: real byte comparison of
  new renderer vs legacy `JsonConverter`. Honest.
- `FileReadBuildTests.FileReadRun_ImageMime`: runs `file.Read.Run()` on real PNG bytes,
  asserts `result.Value is image` and `result.Type.Value == "image"`. Real runtime lift.
- `KindFieldTests`: wire round-trips `Kind` and proves it's a separate field (never
  `type:kind` string). Honest.
- `MathHandlerDataReturnTests` `*_RunSignature_*` (6): reflect Run() return type and
  THROW if it isn't `Task<Data<number>>` — the trailing `Assert.That(true)` is redundant
  padding, the throw is the real check. Honest (just noisy).

## Findings (full detail in `.bot/plang-types/test-report.json`)

### 1 — CRITICAL false-green: Cut4 runtime-DLL goals are load-only stubs
`LoadDllRegistersType.test.goal` and `LoadDllOverwritesBuiltIn.test.goal` are
behaviorally identical:
```
- code.load Path=TypeProvider.dll, on error set %loadFailed% = true
- assert %loadFailed% is null
```
Their comments promise "load → resolve by name → render via runtime-registered renderer"
and "value resolves to loaded CLR type and renders via loaded renderer — runtime wins
ResolveType precedence." Neither creates a value of the loaded type, renders it, or
asserts output or the overwrite. The full TypeProvider.dll (Money/CustomInt) roundtrip
the codeanalyzer explicitly asked to pin is **absent**. A render or precedence regression
ships green. *(The C# RuntimeTypeLoadingTests cover registration+lookup against the test's
own assembly, but never render a value of a loaded type through the wire.)*

### 2 — MAJOR false-green: `LoadDll_AlreadyCompiledHandlerSlot_StillSeesBuiltInType_NoRewrite`
Only assertion: `Assert.That(aProp).IsNotNull()` (math.Add has a property "A"). The
runtime override line above has no bearing on it. Deleting the entire runtime-loading
feature leaves this green. The "honest limit" guarantee it's named for is unpinned.

### 3 — MAJOR false-green: literal kind-stamping goals assert runtime values, not the .pr shape
`SetDecimalLiteralStampsKind`, `Cut1_LiteralKindArithmeticOutput`, `PolymorphicMathAddHasNoKind`
all claim their `.pr` carries `type=number`/`kind=decimal` (or "no kind"). **cache:false
rebuild proves the `.pr` param is `{type:"object", value:3.5}` with no kind, no number** —
the builder stamps kind only for *typed* parameters (`builder/code/Default.cs:881-896`,
`Kinds.Of(declaredType,...)`), and `variable.set`'s Value is polymorphic `Data<object>`.
The goals assert `%z% equals 3.5` / `%b% equals 5.5`, which pass regardless. The build-time
behavior they're named for is unexercised (and for set-literals, does not happen).

### 4 — MAJOR false-green: ~10 deferred no-op `Assert.That(true).IsTrue()` tests pass green
`PlngSerializerCoverageTests` ×4 (PLNG003 gate "FailsBuild"/"DiagnosticId" — not shipped),
`IWriterFormatTests` ×2 (PlangWriter/TextWriter tokens — not shipped),
`MathHandlerDataReturnTests` ×2 (MathHelper ToDouble/PreserveType absence — still present),
`ImageParseTests` Resolve_HttpUrl_FetchesAndConstructs, `PathSerializerMigrationTests`
LegacyJsonConverter_FileDoesNotExist_AfterMigration, `NumberPolicyResolutionTests`
Resolve_SubContext_ClimbsParent_InheritsParentSetting. These map to the coder's documented
deferrals (#1/#3/#4) — but encoding a deferral as a *passing* test reports coverage that
doesn't exist. Fix: `[Skip("deferred: …")]` so they surface as skipped, not green.

### 5 — MAJOR weak-assertion: `DurationRoundTrip.test.goal`
Claims dot:colon + ISO-8601 parse to the same TimeSpan and round-trip canonically.
Actual: `set %d%(duration)="PT5M"` + `assert %d% is not null`. No value, no second form,
no round-trip. Passes even if PT5M → TimeSpan.Zero.

### 6 — MINOR weak-assertion: `LoadDll_PlangTypeWithoutAnyRenderer_FailsLoad_TypedError`
Failure asserts guarded by `if (RegisteredTypes.Contains(...))`. Executes today (Loader
registers all `[PlangType]` before the coverage gate), but a refactor that skips
no-renderer types silently empties the test. Assert the failure unconditionally.

### 7 — MAJOR missing-coverage: NumberPolicy resolution (`Config.cs` 0%)
Policy resolution from `app.config` (overflow/precision + parent-context climb) is 0%
covered. The C# parent-climb test is a no-op (finding 4); the planned plang goals
`OverflowThrowSettingHonored` / `SubGoalInheritsParentPolicy` were not created. Only a
reflection-only property-existence test touches Config. The axis that governs
overflow/divide behavior is unverified end-to-end.

### 8 — MINOR: `ReadPhotoStampsImage.test.goal` weak (but C#-backed)
Only `%photo% is not null`; real image-stamp coverage exists in C# FileReadBuildTests, so
no gap — just an overclaiming comment + redundant goal.

### 9 — MINOR: `RuntimeRendererWins...` doesn't assert shadowing
Verifies the runtime renderer registers + fires, not that it beats a built-in. (The
ResolveType sibling IS a genuine shadow test.)

### 10 — PROCESS: coder shipped no `baseline-tests.md` / `summary.md` / `plan.md`
Only `report.md`. Baseline missing → can't mechanically separate regressions from
pre-existing reds. Mitigated here by a fully-green clean run.

## Verdict

**FAIL** on the strict-red rule: multiple confirmed false greens. The branch is not
broken — no reds, no regressions, the mechanism unit tests are honest — but the two
features a reader would most want pinned (runtime-DLL roundtrip, build-time kind-stamping)
are green-but-empty, and ~10 deferrals masquerade as passing coverage. Most fixes are in
the *tests*, not the production code (mark deferrals `[Skip]`, add real assertions to the
Cut4/duration/kind goals, implement the policy-resolution coverage).
