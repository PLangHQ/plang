# Tester summary — collections-are-data

**Version:** v3 (matches coder v3) · **Verdict: PASS**

## What this is

The `collections-are-data` branch makes PLang collections first-class `Data`: a
native `dict` value type (`Dictionary<string,data>`) and a native `list` value type
(`List<data>`), set-rebinds for variable independence, a single typed-compare path
shared by condition operators and `list.sort`, list/dict ops (incl. the new `where`),
and an `item` apex type. v3 specifically resolved the architect's decompose handoff
(A: wire-reconstruction navigates the native dict instead of deep-decomposing; B: a
real `dict.ToRaw` nested-list asymmetry bug; D: CommandLineParser array/object
symmetry). codeanalyzer v2 = PASS before me.

## What was done (this session)

- **Clean rebuild + both suites.** C# **4082/4082**, plang **273/273**. Ran plang
  `--test` twice from `Tests/`; git stayed clean both times — no warm-cache / bad-LLM
  `.pr` churn (the trap from memory).
- **Read every Stage1–6 C# test for intent.** They verify behavior, not shape:
  Stage4 pins numeric-vs-lexical order, trichotomy, structural equality, and
  equality-only-types-throw-on-Order; Stage5 pins where/group/sort/unique with error
  edges (`WhereOnApex`, `SortOnListOfDict` throws); Stage3 proves a signed element
  survives inside a list across the `.plang` wire (F1).
- **Mutation-tested the highest-risk code.** Flipped the compare text-order arm
  `OrdinalIgnoreCase -> Ordinal`; `Compare_TextCaseInsensitive_OrderAndEqualsAgree`
  failed as expected, then reverted. The compare-path green is honest, not false.
- **Builder smoke test** on a throwaway collection goal: native `set %x% = [1,2,3]`
  compiled, but later steps got "no actions" — the documented bad sandbox LLM
  (`gpt-5.4-nano`), not a branch regression. Throwaway deleted; nothing committed.

## Findings (all minor — none red, none false-green)

1. **Missing plang test for the new `where` action.** Strong C# coverage exists, but
   no `.test.goal` exercises the builder step->`list.where` mapping. Builder env
   blocks closing it here; flagged for a good-LLM build.
2. **Bad-LLM `.pr` rebuilds committed.** `whenless.pr` / `whenlte.pr` (called
   sub-goals, not the test `.pr`) were rebuilt by the sandbox LLM and committed
   (builderVersion null, path collapsed). The real test `.pr` is intact and still
   exercises `<` / `<=`. Process smell — `git checkout` them back.
3. **No `coder/v3/baseline-tests.md`.** Process violation; moot this round since both
   suites are fully green.

## Code example — the kind of intent-test that earns the PASS

```csharp
// Stage4 — case policy is ONE policy across order and equality (no trichotomy break):
await Assert.That(Cmp.AreEqual(D("a"), D("A"))).IsTrue();
await Assert.That(Cmp.Order(D("a"), D("A"))).IsEqualTo(0);   // not >0 — mutation-verified
// equality-only types refuse Order rather than inventing one:
await Assert.That(() => Cmp.Order(D(new DictV()), D(new DictV()))).Throws<Cmp.NotOrderableException>();
```

## Next

`run.ps1 security collections-are-data "Review the code on branch collections-are-data" -b collections-are-data`
