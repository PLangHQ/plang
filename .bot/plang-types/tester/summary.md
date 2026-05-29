# tester — plang-types — summary

**Version:** v1 — **VERDICT: FAIL (needs-fixes)**

## What this is

The `plang-types` branch lands the unified `type + kind` value model: every value is a
high-level PLang type plus an optional `kind` refinement; per-(type,format) renderer
dispatch; new `number`/`image`/`code` value types; arithmetic policy on `number`;
temporal cleanups (datetime/date/time/duration, `timespan` dropped); and a runtime
DLL-loading extension point (`code.load` → `[PlangType]` + `ITypeRenderer`). 7 architect
stages, implemented by coder v1.

## What was done (this tester pass)

Clean rebuild (stale-binary trap honored). Full suites green with no regressions:
**C# 3609/3609, plang 246/246.** Coverage of changed prod files high (51/54 >0%).
Confirmed both codeanalyzer dead-branch findings are cleanly fixed. Validated the builder
works via `cache:false` rebuilds (after correcting my command form to the documented
`--build={...}` flag) — no builder false-greens; .pr step text matches module.action.
All mutations/rebuilds reverted; tree clean.

The suite is all-green but **not honest** — 10 findings, headline:

1. **CRITICAL** — Cut4 runtime-DLL goals (`LoadDllRegistersType`, `LoadDllOverwritesBuiltIn`)
   are load-only stubs: `code.load` + `assert %loadFailed% is null`. Identical behavior,
   distinct names. Never render a loaded-type value or check overwrite precedence — the
   exact roundtrip the codeanalyzer recommended.
2. **MAJOR** — `LoadDll_AlreadyCompiledHandlerSlot...` C# test asserts only that a property
   exists; tautology (deleting the feature keeps it green).
3. **MAJOR** — literal kind-stamping goals (`SetDecimalLiteralStampsKind`, `Cut1`,
   `PolymorphicMathAddHasNoKind`) assert runtime values; cache:false proves the .pr carries
   `type:object`/no-kind (builder stamps kind only for typed params, not `variable.set`).
4. **MAJOR** — ~10 deferred tests pass as no-op `Assert.That(true).IsTrue()` (PLNG003 gate,
   PlangWriter/TextWriter, MathHelper absence, http image, sub-context policy). Should be `[Skip]`.
5. **MAJOR** — `DurationRoundTrip` only `is not null`; no value/second-form/round-trip.
6–10. weak `FailsLoad` guard, NumberPolicy `Config.cs` 0% coverage, weak `ReadPhotoStampsImage`,
   `RuntimeRendererWins` no-shadow-assert, and a process gap (coder shipped no baseline-tests.md).

Solid (not findings): NumberDivide/Arithmetic (value+Kind+Error.Key), PathSerializer
byte-for-byte parity, FileReadBuild runtime image lift, KindField wire round-trip,
MathHandler RunSignature (throw-based).

## Files written

- `.bot/plang-types/test-report.json` — full findings (shared branch root)
- `.bot/plang-types/tester/v1/{plan,result,verdict}.md/json`, `coverage.json`

## Example of the false-green pattern

```
# Tests/Cut4_RuntimeLoadAndRender/LoadDllOverwritesBuiltIn.test.goal
# comment: "value resolves to loaded CLR type and renders via loaded renderer — runtime wins"
- code.load Path=TypeProvider.dll, on error set %loadFailed% = true
- assert %loadFailed% is null      # <-- only checks the DLL loaded; nothing about resolve/render/overwrite
```

## For coder (next)

Most fixes are in the tests, not production code: mark deferrals `[Skip]`; add real
assertions to the Cut4 goals (assign + render + assert output / overwrite), DurationRoundTrip
(value + both forms), and the kind-stamping path (C# .pr-shape assertion on a typed param);
implement the NumberPolicy resolution coverage (SubContext + the two planned Math goals).
