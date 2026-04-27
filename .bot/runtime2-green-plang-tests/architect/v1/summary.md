# Architect v1 — Summary

## What this is

Planning + triage for getting all PLang `.test.goal` files green on the `runtime2` branch after two big merges landed (action modifiers + testing module). My slice of v1 was: write the overall plan (Phases 0–7), propose the Tests/ folder restructure, and after tester completed Phases 0–2 do the Phase 4 triage that routes dispatchable work to coder.

## What was done

**Plan + restructure (landed earlier in v1)**
- `v1/plan.md` — seven-phase sequence: build, restructure, baseline, drain stale, triage, dispatch, iterate, audit.
- `v1/folder_structure.md` — PascalCase Tests/ layout with top-level `Modules/` (per-module tests), `App/` (cross-cutting app layer), `Builder/` (reserved).
- Four plan commits on branch: `b0637f05`, `f62a8813`, `9f77aef9`, `4db0f109`.

**Tester executed Phases 0–2** (commits `58cf7f77` restructure + `2060be4b` rebuild + `bae84328` tester summary): 1309 git renames, 135/141 folders built, baseline `161 tests: 109 pass / 48 fail / 4 stale`, 6 build-failures. Tester handed six findings to architect for Phase 4.

**Phase 4 triage (this session's output)**
- Read tester baseline + test-report findings.
- Verified failure hypotheses by reading `.pr` files, handler code, test goals, and module registry across representative exemplars (Signing/Expired, Loop/CountItem, Event/Basic, ReturnMapping, SetupGoal, Condition/Compound/And, ListOps2).
- Presented clusters to Ingi and collected six design decisions (D1–D6 in triage.md).
- Classified 48 fails + 6 build-failures + 4 stale across five dispatch waves.
- Wrote `v1/triage.md` — preamble with design decisions, per-wave specs (including draft prompt edits for BuildGoal.llm), full per-test classification table, coder's order-of-operations.

## Key design decisions collected

Each reshaped the triage. Full context lives in `triage.md` — summary here:

- **D1 — In-memory sqlite per test** (replaces the "archive then create" workaround). The Identity/Signing "already exists" flood isn't a handler bug — the test runner shares a filesystem-backed system store across tests. Fix at the runner.
- **D2 — `event.on.Type` → `EventType` enum.** Type the field, source generator carries valid values to builder. LLM stops hallucinating.
- **D3 — Builder, not handler, does arithmetic routing.** `set %x% = %x% + 1` compiles to `math.add` + `variable.set` chain via prompt rules. No string-eval magic in the set handler.
- **D4 — Goals auto-return last `%__data__%`.** Ingi's idea in response to the null-return cluster — cleaner than making every helper goal declare explicit return. Coder implements with discussion.
- **D5 — Split `http.download` from save.** Remove `SaveTo`; action returns bytes; `file.save` persists. OBP shape: one concern per action.
- **D6 — Setup is scope-isolated.** `Tests/App/SetupGoal/` is premature because Setup's runtime semantics aren't designed yet. Park the test.

## Dispatch wave summary

| Wave | Fix | Owner | Expected wins |
|---|---|---|---|
| 1 | Per-test in-memory system db | coder | +18 tests (and a class of latent flakes) |
| 2 | `event.on.Type` enum | coder | +3 tests |
| 3 | Goal auto-return `%__data__%` | coder | +5–8 tests |
| 4 | `http.download` split + BuildGoal.llm prompt edits | architect drafts prompt, coder applies | 6 build-failures clear, 2 foreach tests, 4 stale rebuilds |
| 5 | Park SetupGoal | — | deferred until Setup spec |
| 6 | Tail (UI, List, ContextVars, etc.) | architect + Ingi re-triage | after tester re-baselines |

Projected after coder finishes waves 1–4: ~135/161 green (85%, up from 68%).

## Code example — the pattern of the prompt edits I drafted

All Wave 4 rules follow the same voice: state the intent semantically, show a correct `.pr` fragment, show the incorrect one, describe what's wrong. Example (the modifier-shape rule, in full):

> Modifiers wrap a preceding action. They live in the action's `modifiers` array. Never concatenate modifier names into the module path. Module names never contain dots.
>
> Correct: `{"module":"signing","action":"sign","modifiers":[{"module":"error","action":"handle",...}]}`
>
> Incorrect: `{"module":"signing.error.handle","action":"sign"}` — the dotted path invents a non-existent module.

This voice keeps rules multilingual-safe (no English keywords in conditions) and gives the LLM a crisp positive+negative example.

## What's next

- **Coder** — takes waves 1–4 as one dispatch (per Ingi's direction). One commit per wave, or bundled if diff stays readable. Includes the prompt-edit draft from triage.md §4b.
- **Tester** — re-baseline after coder lands waves 1–4.
- **Architect (me)** — Wave 6 re-triage after re-baseline, with Ingi.
- **Auditor** — Phase 7 at the end, per the original plan.

## Files written in this session

- `v1/triage.md` — Phase 4 output
- `v1/summary.md` — this file
- `summary.md` (bot root) — light cross-session rollup (updated)

## Blockers / open items

None at hand-off. All coder-relevant decisions are in triage.md's D1–D6. If coder hits ambiguity mid-implementation (especially on D4 `%__data__%` semantics for side-effect-only last steps, and D5 `http.download` breakage on any app goals that used `SaveTo`), they stop and ask Ingi rather than guessing.
