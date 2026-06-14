# Residual-failure triage — compare-redesign (2026-06-14)

380 C# test failures on the branch, triaged by 4 parallel agents (each ran
representatives in isolation + read production code). **Zero flaky, zero
environment** — every failure reproduces deterministically in isolation. The
branch is mid-migration (typed-value model implemented in the core; not all
consumers migrated yet).

## Top-line split

| Bucket | ~count | Meaning |
|---|---:|---|
| **Signing / key-BLOB** | 64 | Excluded — signatures change next branch (Ingi). |
| **STALE tests** | ~127 | Assert the OLD raw-CLR shape; production is arguably correct, tests need rewriting. |
| **REAL / migration-incomplete** | ~125 | Production consumers not yet migrated to the typed model. Collapse to ~8 root causes below. |
| **Flaky** | 0 | — |
| **Environment** | 0 | — |

The ~125 "real" failures are NOT 125 independent bugs — they collapse to a
handful of shared roots. Fixing the top two clears a large fraction.

## Real root causes (deduplicated, highest-leverage first)

**A. The `item.clr` carrier bridge leaks everywhere.** `Data.Lift`
(`PLang/app/data/this.cs:221-243`) only narrows `IDictionary`/`IList<object?>`/
`ArrayList`; a strongly-typed `List<T>` (T≠object) or a POCO falls through to
`new item.clr(v)` — an opaque no-op carrier. The code self-documents this as a
"TEMPORARY bridge." It then leaks into:
- `Normalize`/`json.Writer` — no `clr` arm → `NormalizeException: json.Writer
  received item.clr` (Wire W2/W3, the 2 NormalizeException failures).
- navigation — `CanNavigate` sees the carrier not the backing → NotFound (Goal
  sub-goal nav, property-plane, several Runtime).
- module couriers — http body / render read the carrier.
This is the single biggest structural lever.

**B. Null-citizen vs C# null guards in production.** Production still checks
`Peek() == null` / `await Value() == null`, but the model returns the
`null.@this` / `absent.Slot` singletons (never bare null). Dead guards:
- `settings/get.cs:24` missing-key → AskError never returned (+ propagates to
  Variables.Get).
- `data/this.Result.cs:88` Merge null-guard.
- `list` `OrderOf` + `data/this.cs:921` CompareValues — nulls-last in sort throws.
- `variable/list/this.cs:705` `%!missing%` blanked instead of preserved.
- **`SnapshotOnError_SensitiveProperty` — a sensitive prop with null value masks
  to `"******"` instead of staying null. Security-relevant (auditor pinned this).**

**C. Diff serializes `Peek()` as the abstract `item.@this`** → value never
emitted → all comparisons report match=true. Two spots:
`data/this.Diff.cs:41` and `snapshot/this.Diff.cs:43`. ~8 tests.

**D. Snapshot callstack frames write/read asymmetry.** `snapshot/serializer/
Default.cs:21` writes frames as Data-enveloped dicts (scalars nested under
`value`); the read side (`callstack/this.Snapshot.cs:171`) expects flat keys →
empty `goalName` → `CallbackGoalNotFound: ''`. 8 Wire tests.

**E. Compress/Decompress through `clr`.** `Compress` boxes bytes in a `clr`
carrier; `DecompressAsync` (`this.Transport.cs:209`) does `Lower<byte[]>(Peek())`
→ null ("Archived Data has no byte[] value"), and its `InvalidCastException`
isn't in the catch set → crash instead of graceful 500. ~10 tests.

**F. Reader exception escapes as `TargetInvocationException`.** Reflection
`method.Invoke` (`type/reader/this.cs:124`) wraps a `JsonException` in TIE;
`source.Value`'s catch filter (`type/item/source.cs:85`) misses TIE → escapes
`Data.Value()` to couriers (OBP Rule 9 violation). ~6 tests.

**G. Module consumers not migrated (Stage 10 tail).** Big raw count, mostly
downstream of A/B:
- LLM `query.*` (~45) — request body built as raw `Dictionary`, response read as
  wrapper STJ can't parse (`llm/code/OpenAi.cs:200-298`). Mock never receives
  the request.
- Identity store (~33) — Create/GetAll/SetDefault read-back broken (null-wrapper
  store root).
- `error.handle` modifier (~14) — wrap ineffective, original error leaks for all
  variants (`error/handle.cs:82-137`).
- HTTP body (~5) — `JsonSerializer.Serialize(bodyVal)` serializes the wrapper.
- Fluid render (~5) — template doesn't unwrap typed value.

**H. Tester / orchestrate sub-step migration incomplete (~19, Runtime R1).**
Discover/Run/MultiBranch/Orchestrate not yet delivering post-refactor behavior;
indented sub-steps not re-enabled.

## STALE-test patterns (~127 — tests to rewrite, not bugs)

Uniform: assert the pre-migration shape. Dominant sub-patterns —
- `text.@this` has no `.Value` string face anymore (removed); read via `.ToString()`/`.Clr<string>()`.
- door returns `null.@this` present-null, not C# null.
- born-native `dict.@this`/`list.@this`/number-wrapper replace raw `Dictionary`/`List`/`long`.
- `Peek()` returns the typed instance, not raw CLR (`byte[]`, `Guid`, …).
- removed internals (`UnwrapJsonElement`, `ToRaw`, `CopyStructure`), drifted error keys (`ValueRequired`→`CreateDeclined`).

## Recommended order (if we close the branch to green)

1. **A — `item.clr` bridge** (finish `Lift` narrowing / give `clr` a Normalize+writer arm + `[Out]`). Highest leverage; clears Normalize/writer/wire/nav + much of G.
2. **B — null-citizen guards** (incl. the sensitive-null security fix).
3. **C/D/E/F** — discrete, clear fixes.
4. **G/H** — consumer migration (the Stage-10 tail).
5. **STALE tests** — bulk rewrite to the typed surface (mechanical, large).

Agent IDs (resumable for deeper drill): Modules a303bb85d0d62cc49 · Data
a14cdd21ebcfaa714 · Runtime+Wire aaed4b191959ee370 · Types+Gen aca9adcaa752299e5.
