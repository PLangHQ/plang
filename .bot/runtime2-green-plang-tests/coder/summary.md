# Coder — runtime2-green-plang-tests

## v1 (2026-04-21)

Implemented architect's waves 1–4: per-test in-memory System db (+App.Id scoping kills shared-cache leak), `event.on.Type → EventType` enum, `variable.set` returns its stored Data, `http.download` split (returns bytes, `file.save` persists), and 5 new builder prompt rules (modifier shape, arithmetic, download+save, wait/sleep, event enum). Unified `Variables.Put`/`Set`/`PutAs` to a single `Set` with Data aliasing (per Ingi — no clone, same reference under `%__data__%` + producer name). Final: **161 tests — 122 pass / 35 fail / 4 stale** (baseline 109/48/18, +13 passes, −13 fails). Full Tests/ rebuild regressed the suite and was reverted; C# changes alone deliver the wins. See [v1/summary.md](v1/summary.md) for details. Next: tester re-baseline, architect Wave 6 re-triage.

## v2 (2026-04-21)

Test-only follow-up addressing tester v2's three major W3 findings (F3-1, F3-2, F3-3). Added assertions in `settests.cs` that `variable.set` returns a Data whose `.Value` matches the stored value (plus the AsDefault-existing branch). Added a new test that runs `Action.RunAsync` and verifies `%__data__%` and the producer variable are reference-equal to the returned Data, and that `Name` is not mutated to `"__data__"`. Added a new `Variables.Set` test proving aliasing-without-clone when the Data's Name differs from the storage key. C# suite: 2275/2276 (pre-existing LLM flake). No runtime changes. See [v2/summary.md](v2/summary.md) for details. Next: tester verifies closure; then architect on F4c-1.
