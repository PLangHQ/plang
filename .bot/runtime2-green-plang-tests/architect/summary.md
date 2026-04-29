# Architect — runtime2-green-plang-tests

## v1 (2026-04-21)

Planned the seven-phase sequence to drive all `.test.goal` files green on `runtime2` after the action-modifiers + testing-module merges. Proposed the PascalCase Tests/ folder restructure (`Modules/`, `App/`, `Builder/`). Tester executed Phases 0–2; baseline was 109 pass / 48 fail / 4 stale with 6 build-failures. Phase 4 triage classified every failure into five dispatch waves, collecting six design decisions from Ingi that reshaped the fix strategy (biggest shift: per-test in-memory sqlite replaces the proposed handler-level "archive/create" fix; goal auto-return of last `%__data__%` replaces per-test `return` boilerplate). Coder dispatched with waves 1–4 bundled. See [v1/summary.md](v1/summary.md) for full session detail and [v1/triage.md](v1/triage.md) for the dispatch specs.

## v2 (2026-04-21)

Coder v1 landed waves 1–4 but the full Tests/ rebuild regressed 38 tests and was reverted — the five BuildGoal.llm prompt rules are code-landed but dormant. Tester v2 flagged as F4c-1 (critical, architect-owned). Coder v2 closed the W3 C# test-coverage gaps (F3-1/2/3). This session specifies a **surgical rebuild** dispatch for coder: 11 currently-failing Tests/ folders, each with the specific rule it should satisfy and a hand-review procedure. Bounded, no blast radius — worst case a folder stays failing. Deferred golden-eval infrastructure (tester's option b) as a separate follow-up task. See [v2/plan.md](v2/plan.md) for the coder dispatch and [v2/summary.md](v2/summary.md) for full detail.
