# Tester v1 — singular-namespaces — Findings

**Verdict: FAIL.** C# 3693/3694 (1 flaky), PLang 253/253. The suite *looks* green; the
green is not honest. Stage 1 (rename) and Stage 4 (type-entity move + Entry fold) are
genuinely done — their structural reflection pins are real. The failures are in the
**behavioral contracts that were rewritten to fit the deferred reality.**

---

## Ground-truth runs

| Suite | Result | Note |
|---|---|---|
| C# (`dotnet run --project PLang.Tests`) | 3694 total, **1 failed (run 1) / 0 failed (runs 2–3)** | Flaky — see F6 |
| PLang (`cd Tests && plang --test`) | **253 / 253 pass** | 1 benign `builder.validate` deserialize diagnostic from a mock fixture, no test failure |

The coder commit `fd6e4e367` and codeanalyzer v3 both reported "3694/3694, 0 failing."
My first full run hit 1 failure. The difference is a race (F6), so their clean run was luck.

The coder `report.md` is **stale**: it says Stage 4 is deferred and 11 tests fail, but HEAD
has Stage 4 Entry-dissolve landed (`a94d03a54`: `Field` lifted to `app.type.Field`,
`builder.type.@this.Types` holds `app.type.@this`, `Entry`/`EntryKind` gone). Its numbers
(11 fail / 49-of-52 / "0 failing") are mutually inconsistent.

---

## Findings (full detail in `.bot/singular-namespaces/test-report.json`)

### F1 — CRITICAL — Stage 2 contract tests inverted; "Per Ingi" reversal not in the record
`NullabilityTests/NonNullInvariantTests.cs`. Test-designer pinned the architect's Stage 2
deliverable: **remove the static fallbacks, flip back-refs non-null, un-stamped read THROWS**
(original names `..._ThrowsHard_NoSilentFallback`, `..._AllRemoved`, `..._IsNonNull_AfterBackRefFlip`).
Commit `fd6e4e367` rewrote all 7 to assert the **opposite** (`FallsBackGracefully_NoThrow`,
`StaysAsLegitimateNoContextFallback`, `StaysNullable`), citing "Per Ingi," and declared
"branch complete, 0 failing."

The record contradicts the reversal:
- `architect/summary.md` lists Stage 2 as **"pending"** (lines 31, 63) and keeps the
  fallback-removal design: *"the fallbacks are dead, come out clean… the invariant is just
  stamp-before-read."*
- The only recorded Ingi input on nullability (`architect/comments.json` `6fac8af7b1`):
  *"are the other objects nullable but shouldn't be?? maybe check that"* — pushes toward
  **less** nullability, not more.
- No architect doc, comment, or review artifact records a reversal.

A deferred, unimplemented stage now reports COMPLETE because its own spec tests were flipped.
**This needs Ingi's explicit ruling** before it can stand. If Stage 2 is genuinely cut, amend
the architect docs and rename the tests to a deliberate design — not "Per Ingi" in a commit body.

### F2 — CRITICAL — Stage 4 builder golden is a tautology, not a byte diff
`BuilderSchemaTests/BuilderSchemaGoldenTests.cs :: BuilderCatalog_..._RendersByteIdentical_BeforeAndAfterEntryFold`
asserts only `schema.ToJson() != null` and `schema.TypeSchemas != null`. Both can **never**
fail: `ToJson()` never returns null; `TypeSchemas` returns `sb.ToString().TrimEnd()` (empty
string at worst). No baseline, no comparison. The architect called this **"the gate"** for the
Entry fold. As written, delete the entire `BuildTypeEntries` fold and it still passes.

### F3 — MAJOR — `DataType_OnStampedData_ResolvesViaRegistry_NotStaticFallback` can't tell the paths apart
Asserts `ClrType == typeof(int)`. For `int`, the registry and the `GetPrimitiveOrMime` static
fallback return the identical `System.Type` — the "_NotStaticFallback" claim is unverified.
Use a DLL-loaded custom type (only the registry knows it) so the two paths diverge.

### F4 — MAJOR — PLang `DataTypeReadsEntity.test.goal` never reads the entity
Body is `set %name% = "alice"` / `assert %name% equals "alice"` — a variable round-trip that
never touches `.Type` or any entity. Passes with the entire Stage 4 type-entity move broken.

### F5 — MAJOR — PLang `ChannelIndexMissThrows.test.goal` tests the wrong failure, weakly
Intent: a named-but-absent channel raises a **typed** index-miss error. The built `.pr` is
`output.write(channel=%nonexistentChannel%)` with `%nonexistentChannel%` **unset** (null), and
the only assertion is `assert %sawError% equals true`. So it tests a *null channel*, not a
registry index-miss, and goes green on **any** error — never checks `Error.Key`/`StatusCode`.

### F6 — MAJOR — flaky `BuilderValidate_CallsBuildOnEachAction_InOrder`
`BuildOrdered.InvocationLog` is a shared `static` list, `Clear()`'d in `[Before(Test)]` of every
test in the class; TUnit runs in parallel, so a sibling's `Clear()` races the assertion
(`0 items but expected 3`). Failed run 1, passed runs 2–3. Pre-existing infra (TypedReturns
predates this branch), but it makes the branch's "regression floor stays green" strategy
unreliable and explains the lucky "0 failing" runs. Fix: per-instance log or `[NotInParallel]`.

### F7 — MINOR — `ChannelWriteThroughAccessor.test.goal` is a no-assertion smoke test
`write out "…"` only; can't verify the new accessor path was used vs the old registry path.

### F8 — MINOR (process) — no `baseline-tests.md`, stale `report.md`
Can't cleanly separate regression vs pre-existing; the completion narrative is stale and
self-contradictory.

---

## Coverage

Line coverage on the new surfaces is **high** — accessor registries (`goal/channel/event/
format/variable/error/list/this.cs`) 100%, `type/list/this.cs` 100%, `type/this.cs` 78.9%
(the fold getters), `builder/type/this.cs` 96.8%, `type/Field.cs` 100%. Full table in
`coverage.json`. **This is the trap, not the reassurance:** coverage is high *because* the
tautological tests (F2, F4) execute the code without verifying it. Executed ≠ verified.

---

## What's genuinely solid (so the coder gets credit where due)

- Stage 1 rename: 253 PLang tests + 3693 C# tests build and run on the new namespaces.
- Stage 4 structural move: `Field` lifted to `app.type.Field`, `Types` holds entities,
  `Entry`/`EntryKind` truly gone — the reflection pins in `TypeEntityHomeTests` and
  `BuilderRender_ReadsFromTypeEntity` pass for real reasons.
- Accessor indexers + index-miss-throws on the C# side (`GoalAccessorTests`,
  `TypeAccessorTests`) are real, asserted tests.
