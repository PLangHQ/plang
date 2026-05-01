# docs v1 — runtime2-data-share-state

## Context

Branch landed phases 1–4 (and the spot-check tests for 5a) of architect/v1's 6-phase plan: `Data` events become `List<Action<...>>` so cross-type wraps share state by reference, identity-preserving wrap rules (same-type / variance / cross-type) replace the always-allocate `ConvertAndWrap`, `AsCanonical` for plain-Data slots returns the LIVE variable Data, `Variables.Set` is dumb storage with event-list aliasing on replacement, and `variable.set` is the sole binding-mint site with type inference + snapshot-clone semantics. Phases 5b/5c/6 (handler migration off `[VariableName]` + Legacy emission delete) deferred — they need rebuilt `.pr` files which the LLM build pipeline can't produce on this branch.

Coder/v2 added a follow-up fix for nested-`%var%` resolution on plain Data plus `JsonNode`/`JsonArray` dispatch in `TypeConverter` — the LLM builder pipeline was crashing because plain-Data resolution drifted from the typed walk.

Coder/v3 (auditor-response) reframed the contract from "subscriber-survival when dv lists are empty" to "events bound to the **name**, Properties bound to the **Data instance**" — `CarryStateFromSource` deleted entirely, `variable.set` becomes pure mint+store, `Variables.Set` owns all state survival.

Reviewer chain: codeanalyzer/v1 (NEEDS WORK, 4 cleanups) → v2 (CLEAN) → v3 (PASS, nested-var walk + JsonNode dispatch) → tester/v1 (APPROVED, 4 major coverage gaps) → tester/v2 (PASS, 1 confirmed false-green + 6 carryovers) → auditor/v1 (FAIL, 1 major Debug-watch regression) → coder/v3 (regression fix) → auditor/v2 (PASS).

Final ground state: C# 2533/2542 (9 honest stubs for Phase 5c/6); plang `--test` 166/166. One CLAUDE.md proposal on file (coder/v2 — running plang tests).

No prior docs work on this branch, no prior security review.

## Documentation gaps identified

### Architecture / design docs (`Documentation/v0.2/`)

- **`data-generic-design.md`** — describes the v4 design (`As<T>` always allocates, copies `.Value`). No section on identity preservation, the four wrap rules, `AsCanonical`, or the events-follow-name / Properties-stay-with-Data contract. The doc's "data owns its conversion" sketch (line 388-402) shows the always-allocate shape — outdated. Append a "Identity preservation — `As<T>` wrap rules + `AsCanonical`" section.

- **`variables.md`** — API surface lists `Put(Data data)` which doesn't exist (was renamed to `Set(Data)` long ago). Lists wrong return type for `GetAll()` (`IEnumerable<Data>` vs the actual `IEnumerable<KeyValuePair<string, Data>>`). Missing: `Save()`, `Restore()`, `Snapshot()`, `Resolve()`. Stale `*__Generated` reference at line 152. No mention of the events-follow-name contract.

- **`architecture.md`** — Variables snippet (line 405-413) shows `Variables.Put(data)` which doesn't exist. No mention of identity preservation in the "Everything is Data" section.

- **`debug.md`** — line 141 says "Events are copied when a variable is replaced." Misleading post-this-branch — events are **aliased** (list refs shared), not copied. The semantic change matters: subscribers added later are visible from any prior alias holding the same list ref.

- **`good_to_know.md`** — no entries on identity preservation, AsCanonical, the `Variables.Set` contract, the `variable.set` mint-site role, the `IsPlangIterable` / `IsPlangAssignable` rule, or the `JsonNode`/`JsonArray` dispatch addition. All cross-cutting (multiple files, non-obvious from the code alone).

### `Documentation/Runtime2/`

`good_to_know.md` and `data-spec.md` exist under `Documentation/Runtime2/` but per the user's auto-memory note ("Old v1 docs in `Documentation/modules/` are deprecated"), `Documentation/v0.2/` is the canonical home for runtime2 docs. Skipping `Documentation/Runtime2/`.

### PLang user-facing docs (`docs/`)

- **`docs/modules/loop.md`** — `foreach` over a string. C# treats strings as `IEnumerable<char>`; users coming from imperative languages might expect char iteration. Plang's rule: strings are atomic, foreach runs once with the string itself. User-visible behaviour change-free since it was never documented; deserves an explicit note.

- **`docs/modules/variable.md`** — `set %x% = %y%` snapshot-clone semantics for List/Dict. Independent copies, mutating the source doesn't bleed through. User-visible (would surprise someone expecting reference semantics).

### Stale `[VariableName]` references — leave alone

The branch documented `[VariableName]` removal (Phase 6) as **deferred**. The 25 handlers under `App/modules/list/`, `App/modules/loop/`, and `App/modules/variable/` still use `[VariableName]` and raw `partial` scalars. Existing references in `architecture.md:265`, `good_to_know.md:614,616,618,620`, `action-catalog.md:66,171,174`, root `CLAUDE.md:25` are still accurate. **Do not edit them.**

### XML docs / public surface

Spot-checked `Data.AsCanonical`, `Data.WrapAs<T>`, `Data.IsPlangIterable`, `Data.IsPlangAssignable`, `Data.TryFullVarMatch`, `Data.WalkContainerVars`, `Variables.Set`, `Variables.Remove`, `variable.set.MintTyped`, `variable.set.ConstructDataOfT`. All carry meaningful XML covering what + why. Adding none.

### CHANGELOG

No project-level CHANGELOG file exists. User-visible changes (snapshot-clone semantics for `set %x% = %y%`, `foreach` over a string runs once) captured in `result.md`.

## CLAUDE.md proposal decisions

| From | Target | Decision | Reason |
|------|--------|----------|--------|
| coder/v2 — Running plang Tests | root `/CLAUDE.md` (under `## Build`) | **applied** | Genuinely canonical: any future bot running tests from project root will trip the same `tests/` (lowercase) and `.bot/` discovery noise. The user noted this directly while debugging the v2 work. The proposal is concrete (commands, paths) and prescriptive. Folded into root CLAUDE.md as a new `## Running plang Tests` subsection between `## Build` and `## Debugging`. |

The proposal targeted root `/CLAUDE.md` already, so no folding decision needed.

## Plan of work (in order)

1. **`Documentation/v0.2/data-generic-design.md`** — append "Identity preservation — `As<T>` wrap rules + `AsCanonical`" section.
2. **`Documentation/v0.2/variables.md`** — fix API surface (no `Put`, correct `GetAll` shape, add Save/Restore/Snapshot/Resolve), update event-copy → aliased, fix stale `*__Generated` reference.
3. **`Documentation/v0.2/architecture.md`** — fix Variables snippet (no `Put`); add identity-preservation pointer.
4. **`Documentation/v0.2/debug.md`** — update "Events are copied" → "Events are aliased" with the contract distinction.
5. **`Documentation/v0.2/good_to_know.md`** — add 5 cross-cutting entries (As<T> four rules; AsCanonical; Variables.Set contract; variable.set mint-site; string-not-iterable rule; JsonNode/JsonArray TypeConverter dispatch).
6. **`docs/modules/loop.md`** — add "Strings are atomic" subsection.
7. **`docs/modules/variable.md`** — add snapshot-clone-on-`set %x% = %y%` note.
8. **Root `CLAUDE.md`** — apply coder/v2's "Running plang Tests" proposal.
9. **`.bot/runtime2-data-share-state/docs/v1/result.md`** — CHANGELOG-style entry for user-visible surface changes.
10. **`docs-report.json`** + **`v1/verdict.json`** — final reports.
11. **`v1/summary.md`** + bot root **`docs/summary.md`**.
12. Commit.

## Open questions

None.
