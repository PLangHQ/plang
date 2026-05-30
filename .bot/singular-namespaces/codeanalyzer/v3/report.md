# Code Analyzer — v3 — `singular-namespaces` (re-review of v2 fix)

**Reviewing:** `7b9a5ee8a..HEAD`:
```
58e6e6533 codeanalyzer v2: correct misleading goal/list.All comment (it has 2 callers)
a5e42d908 codeanalyzer v2: deterministic catalog collision resolution by richness rank
```
**Build:** clean rebuild of PlangConsole + PLang.Tests → 0 errors.
**Suite:** **3694 / 3694 pass, 0 failed** (clean rebuild; the prior `BuilderValidate…InOrder` parallel-flake also passed this run).

---

## Verdict: **PASS**

Both v2 items are resolved, and verified empirically the way they failed — in isolation, not just in a full-suite run.

## Finding 1 (v2 blocker — non-deterministic fold props) — **FIXED**

The cache build replaced first-wins `TryAdd` with a deterministic richness-rank tiebreak (`type/list/this.cs:172–205`):
```csharp
if (!dict.TryGetValue(entry.Value, out var existing)) { dict[entry.Value] = entry; continue; }
if (Rank(entry) > Rank(existing)) dict[entry.Value] = entry;
// Rank: Record(Fields)=3 > Enum(Values)=2 > Scalar(Shape/CtorSig)=1 > barren=0
```
On a same-name collision the catalog-richer entry now wins regardless of reflection order, so a barren `"goal"` entry can no longer shadow the `Fields`-bearing `app.goal.@this`.

**Verification (the exact reproduction that failed 8/8 in v2):**
- `AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry` in **isolation, 8 consecutive runs → 8 PASS / 0 FAIL** (was 0/8).
- TypeAccessorTests 9/9; full `App.Types` namespace 290/290; full suite 3694/3694.

`Rank()` reads `entry.Fields/Values/Shape/ConstructorSignature` during cache construction when `Context == null` — confirmed safe: those getters route through `Promote()`, which short-circuits with `Context == null` (no re-entry into `ComplexSchemas()`), so there's no recursion and no accidental `_foldLoaded` damage to the kept winner (its fields are init-populated). `data.Type` (which reads the same cache via `Promote → ComplexSchemas`) inherits the now-deterministic result.

**Minor residual (non-blocking):** the tiebreak resolves *cross-rank* collisions only. Two **equal-rank** entries sharing one name (e.g. two distinct records both exposing `Fields`) would fall back to first-wins and stay order-sensitive. That case doesn't occur today (suite green, contract deterministic), and even if it did both candidates satisfy `Fields != null` — so it's a latent edge, not a defect. Worth a one-line guard (e.g. assert/disambiguate on equal-rank same-name) only if a second populated type ever legitimately claims an existing PLang name.

## Finding 2 (v2 low — "dead `goal/list.All`") — **WAS A FALSE POSITIVE on my side; comment now correct**

I called `goal/list.All` dead. It isn't — `GoalsTests.cs:233` does `goals.All.ToList()`. My v2 grep (`\.Goal\.All`) was too narrow and missed the local-variable call site (`goals.All`). The coder verified the real callers and corrected the comment to state why `All` stays (`goal/list/this.cs:296–298`). The correction is accurate; my finding was the error. Noted so it isn't carried forward.

---

## Bottom line
The non-determinism is gone — proven by the same isolated reproduction that exposed it (8/8), plus a fully green suite on a clean build. The remaining equal-rank-collision edge is latent and non-blocking. The singular-namespace reshape is clean and the type-entity door contract now holds deterministically across both doors. Ship it to tester.
