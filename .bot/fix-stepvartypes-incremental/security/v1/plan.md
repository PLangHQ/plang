# Security v1 — fix-stepvartypes-incremental

## Scope
17 C# files / +330 −177 vs origin/runtime2 merge-base (eb709d7aa). Touches:
- `app/types/path/` — canonical display form for `_relative` (PLang root-relative `/`-anchored). `_absolutePath` canonicalization (the AuthGate gate) unchanged.
- `app/types/Conversion.cs` — write side now uses the same `ContextualReadOptions` as the read side so nested `path.@this` round-trips via `PathJsonConverter` (string form).
- `app/formats/this.cs` — adds `.template` / `.liquid` → `text/plain` MIME entries.
- `app/modules/test/{report,run,discover}.cs`, `app/tester/{Run,Timing,Timings,File}.cs` — child-app rooting fix, output capture via `BeforeWrite` event, per-step timings, `Output` + `Timings` surfaced in `results.json`. Plus `ReportOptions` with `ReferenceHandler.IgnoreCycles`.
- `app/modules/llm/code/OpenAi.cs` — cached-tokens pricing table + cost math.
- Plus minor builder/condition cleanups + dropped Step properties.

## Threat-model focus
1. **Path canonicalization fence (prior-branch fix).** Confirm `purge-systemio-from-actions` AuthGate canonicalization (`PathHelper.GetFullPath` in `path.@this` ctor) is still the only path through — the `_relative` display change does NOT touch `_absolutePath`. ✓
2. **`results.json` leakage surface.** `Output` field now ships per-test stdout to `results.json` and to `[Sensitive]`-blind serializers. Broadens — does not introduce — the standing Medium on `Variables.Snapshot()` leakage.
3. **Conversion converter symmetry.** Applying `ContextualReadOptions` on serialize too — confirm converters are pure (no IO/side effects).
4. **Child-App rooting change.** `new app.@this(parentApp.AbsolutePath)` instead of `test.Directory` widens the trust scope of the child test; not a regression because tests are user code on the user's machine.
5. **Event-binding capture concurrency.** `outputBuf` (StringBuilder) and `stepStarts` (Dictionary) shared across `BeforeWrite`/`BeforeStep`/`AfterStep` handlers in a child App. Are step/write paths serialized?
6. **OpenAi pricing/cost math.** Pure data plumbing; no security-relevant surface.

## Result
See `result.md` and `verdict.json`.
