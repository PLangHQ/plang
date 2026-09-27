# 7f — result (plan: `7f-plan.md`)

## Built
- ee9701612 concept types are type-list entries (actor stays scanned: stage 10).
- 36ce02507 `Save(setting)` / `Remove(setting)`.
- 8246f5595 `Every` → `Walk` (decision 36 closed as two jobs).
- bd877ef5e settings taught (2c (i)): a Settings block in prompt C's user message when a step names a `%!path%`;
  `settings.json` from the classes (SettingCatalogTwinTests), python `prompt_c.settings_block`, byte-equal twin.
  test's `Parallel` default is 0 (machine-independent).
- 0337f0641 the `%!app.X["key"]%` line moved out of the system prompt into a Keys line, only where a step reads one
  (the system line made nano drop start's step 0 in 6/6 replays; A/B/C in `round16-diff.md`).
- 72671e800 Finding 2: `save %!x%` taught apart from `file.save` (file module + file.save + setting.save descriptions).
- f200be5e7 Finding 1 (Ingi): test's settings are `%!app.test.setting%` — `setting.IConcept<T>` on the element, the
  concept type answers `.setting`; goal's type names none. A setting option is set through its convert walk.

## Eval (round 16; baseline `rounds/round11-run1`, its raw folder lost) — `/shared/coder/2.0/round16-diff.md`
| run | golden first / final | bootstrap first / retried / refused |
|---|---|---|
| 1 (system line) | 51 / 43 (15 failed: start step 0) | — (stopped) |
| 2 (Keys line) | 57 / 58 | 8/1/3, then 7/3/2 (ruled passing, decision 83) |
| 3 (both findings) | **58 / 58** | **8/2/2** — refusals Start [0] `Operator==`, SourceError [1] `\n`, unchanged steps |

System prompt byte-equal to round 11 in runs 2 and 3. Every hunk traced (rows 6, 8, 12; Finding 2; the decider's scores).

## Re-records
PickListTests (`pick_golden.json` from run 3) and BootstrapTests (the builder's answers, Build.goal's included) green.

## First builds (`plang --test`: 15 pass, 2 fail, 305 stale)
| goal | result |
|---|---|
| TestRunEnforcesTimeout ×2, TestRunIsolatesMemoryStackBetweenTests ×2 | pass (fixtures built) |
| TestReportWritesJunitXml ×2 | pass (reads rewritten to `%report!fact%`, stage 6's form) |
| TestReportMasksSensitiveVariablesJunit ×2 | fail: the fixture asserts on `%MyIdentity%`, which nothing binds (pre-existing) |
| SettingsCrud, ActorDatasource | pass |

## Noticed (not 7f)
- A `call /path …` step gets only a possible `goal.call` with no prefill (decision 81) — most first-try misses.
- HandleBuildFailure's `set %trace.buildError% = {…}` stores `%!error.Key%` / `%!error.Message%` unrendered.
- `%MyIdentity%` is bound nowhere (the two Masks tests, baseline `MyIdentity_UpdatedAfterSetDefault`).
