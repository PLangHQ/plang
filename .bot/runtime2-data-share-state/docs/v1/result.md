# docs v1 — result on runtime2-data-share-state

## Verdict

**PASS.** All identified gaps filled. CLAUDE.md proposal applied. Build clean post-edits (0 errors; 364 pre-existing nullability warnings, unchanged from auditor v2).

## What changed

### `Documentation/v0.2/data-generic-design.md`
Appended a new section: **"Identity preservation — `As<T>` wrap rules + `AsCanonical`"**.
- The four `As<T>` rules (same-type fast / variance fast / cross-type with conversion / failure) with the aliasing semantics explicitly called out.
- `AsCanonical` for plain-Data slots — full match returns the live variable Data, partial / container / unset paths.
- The `Variables.Set` contract: events follow the name (aliased), Properties stay with the Data instance.
- `IsPlangIterable` / `IsPlangAssignable` carve-out — strings are atomic.

The pre-existing "Section 7's sketch" of `As<T>` (always-allocate) is preserved as historical design context; the new section is appended as the shipped contract.

### `Documentation/v0.2/variables.md`
- Replaced the API-surface block (the old block listed `Put(Data data)` which doesn't exist; corrected to actual `Set` overloads + `Save`/`Restore`/`Snapshot`/`Resolve`).
- "Behavior & Rules" rewritten to reflect: dictionary-key-is-source-of-truth, in-place update on non-Data path, events-follow-name on replacement, Properties-stay-with-Data, snapshot-clone via JSON roundtrip in `variable.set`.
- Code examples updated (`Data.Type.Int` not `Type.Int`, added a `Set(Data)` example).
- Fixed stale `*__Generated` reference (line 152) — replaced with the v4 `partial class` shape using `As<T>` / `AsCanonical`.

### `Documentation/v0.2/architecture.md`
- Variables snippet (line 405-413): replaced `Variables.Put(data)` with the actual API; added a one-paragraph pointer to the events-follow-name / Properties-stay-with-Data contract.

### `Documentation/v0.2/debug.md`
- "Events are copied" rewritten to **"Events are aliased"** with a note on visibility through any alias holding the shared list ref.

### `Documentation/v0.2/good_to_know.md`
Six new cross-cutting entries appended:
- **Data identity preservation — `As<T>` four wrap rules** (same-type / variance / cross-type / failure; aliasing semantics; cross-cutting impact on debug watches and `condition.if`'s `branchIndex`).
- **`AsCanonical` — plain `Data` slots return the live variable** (full-match / literal / partial / container-walk / unset paths).
- **`Variables.Set` — events follow the name, Properties stay with the Data** (the alias-on-replacement code; idempotent Set; non-Data path inconsistency from auditor v2 N1).
- **`variable.set` is the sole binding-mint site** (`MintTyped` if-chain; snapshot-clone via JSON roundtrip; forced `[Type]`; other callers that don't mint user-named bindings).
- **String-not-iterable — `IsPlangIterable` / `IsPlangAssignable`** (the carve-out; user-visible foreach behaviour; one source of truth for three call sites).
- **JsonNode / JsonArray dispatch in `TypeConverter`** (why `JsonObject`/`JsonArray` need their own arms; cross-cutting impact on the LLM builder pipeline).

### `docs/modules/loop.md`
Added a **"Strings are atomic"** subsection at the end of the foreach docs — `foreach %greeting%` over `"hello"` runs once with `%item% = "hello"`, not five times with each char. Includes example + reminder to use `string.split` for chars.

### `docs/modules/variable.md`
Added a **"Lists and dictionaries are independent copies"** subsection under `set default` — `set %x% = %y%` for List/Dict creates a fresh copy. Example shows `add` to source not bleeding into target.

### Root `CLAUDE.md`
Applied coder/v2's proposal: new `## Running plang Tests` subsection between `## Build` and `## Debugging`. Codifies the `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` rule and the C# vs PLang test runner distinction.

## CHANGELOG (user-visible)

User-facing behavioral notes captured for this branch:

1. **`set %x% = %y%` for List/Dict produces an independent copy.** Snapshot-cloned via JSON roundtrip in `variable.set.MintTyped`. Mutating the source after binding does not bleed into the target. Documented in `docs/modules/variable.md`.

2. **`foreach %s%` where `s` is a string runs the body once.** With `%item% = s`, not char-by-char. Plang's `IsPlangIterable` carve-out. Documented in `docs/modules/loop.md`.

3. **`--debug={"variables":[...]}` watches now survive every replacement.** Subscribers attached to a placeholder fire on every assignment to that name, not just the first. Pinned by `DebugWatch_OnChange_FiresOnEveryReplacement`. The user-visible doc in `Documentation/v0.2/debug.md` was already correct in spirit ("Events are copied… so the handler survives"); updated to the more precise "aliased" language.

No new actions, no new error keys, no new attributes, no new builder syntax.

## What I did NOT touch

- **Stale `[VariableName]` references in existing docs.** Phases 5b/5c/6 are deferred — `[VariableName]` is still in active use across ~25 handlers. Any "removal" doc edit would be premature.
- **PLang `.goal` examples** for the new behaviour. Tester writes PLang tests; deferred-stub `.test.goal2` for `StringNotIterable` already exists and is documented in coder/v1 summary as deferred.
- **`Documentation/Runtime2/` docs.** Per memory note, `Documentation/v0.2/` is the canonical home; the other tree is deprecated.
- **`F2` / `F3` / `F4` auditor carryovers.** Not docs work — they're code cleanups (3-line test, qualifier strip, dead `??`).

## Build verification

```
dotnet build PLang/PLang.csproj
  0 Error(s)
  364 Warning(s)  (all pre-existing nullability — auditor v2 noted same count)
```

Docs-only edits; no behavioral risk.

## Hand-off

- **Security review** — flagged by both auditor v1 and v2 as recommended next step but not yet on file. JSON-roundtrip expansions (TypeConverter JsonNode dispatch, list.add SnapshotClone, variable.set MintTyped's deep-clone of List/Dict) are new attack surface worth one pass. **Out of scope for docs.**
- **Auditor F2/F3/F4 cleanups** — small code-only tasks, no docs change. Can ride a future cleanup PR.
