# codeanalyzer — set-add-order, v1, f3d89adf6 — FAIL (coder next)

Scope: `PLang/app/goal/step/pick/list/this.cs` read whole; `PLang.Tests/Wire/App/Decider/ModulePartTests.cs`; the re-pinned
`pick_golden.json`. Parent: runtime2 (366c06e8c). Decider fixtures run: ModulePartTests 2, PickListTests 8, PickOptionTests 18,
ConfirmTemplateTests 2, LineTwinTests 1, KeptStepTests 8 — all green. Bench: skipped (build-time listing, no run path, no store).

## Findings by pattern

### 1. Pattern 12 (one answer) — `Part` breaks the 09-30 guard when stage 1 gives no probabilities — BLOCKING
`this.cs:504-512`. `Share` (`:457-467`) falls back to `into[choice] = confidence` when the answer has no `probabilities`
(the tests build such answers: `Choice(...)` defaults to `probabilities: null`). Then `_module` holds one entry, `open` is
that one module, `sum` is its own share, and `Part` returns `share / share = 1.0`.

Probe (announced, deleted, tree clean): `remove %x%`, stage 1 `{choice: file, confidence: 0.56, probabilities: null}`,
`@also.file` 0.99, `file → delete`:
- runtime2: `file.delete` **Possible** (0.56 < 0.9).
- f3d89adf6: `file.delete` **Certain**, starting line `file.delete(Path)`.
That is the exact step the 09-30 guard (fb14118e0) was written for, silently let through. `ARemoveUnsureOfItsModule_StaysPossible`
is green only because it supplies `probabilities`.

Why it is the shape, not the input: `Part` divides by the *sum of the open shares*, which assumes the shares are the whole
distribution. A truncated or absent distribution inflates it.
Root-level fix, smaller than today's code: divide by what is left after the settled modules, not by what the open ones
add up to:
```csharp
// a module's share of the work the settled modules left: its stage-1 share over 1 less the shares they took
private number Left(string module)
{
    var settled = Settled;
    var taken = Ranked().Where(m => settled.Contains(m.Module))
        .Aggregate((number)0.0, (total, m) => total + m.Share);
    var left = (number)1.0 - taken;
    return left <= (number)0.0 ? 0.0 : (_module.GetValueOrDefault(module) ?? (number)0.0) / left;
}   // sketch: number arithmetic as the file already writes it
```
`{file 0.56}` (no probabilities) → 0.56 Possible ✓. `{math 0.67, variable 0.33}`, variable settled → 0.67/0.67 = 1.0 ✓.
`remove %x%` with probabilities → 0.56 Possible ✓ (plan expected 0.61; same verdict). Mass nobody claims stays as doubt.
Needs the three golden picks re-read against it (they pass today because the open shares are the only mass; check
`error 0.48`, `error 0.52`, `goal 0.23` each have the rest of their mass on the settled module).
Test to add (red on f3d89adf6): the same `remove %x%` with `probabilities` null.

### 2. Dead guard in `Part` (deletion test) — minor
`this.cs:508`: `if (open.All(m => m.Module != module)) return 0.0;`. `Certain` asks only for a pick made *through* a module
(`Through`), and a module reaches a pick only via `Asked`, which drops settled modules and takes only `Ranked` ones. So the
branch cannot be reached, and no test would fail if it were deleted; the comment ("0 when the module is settled") describes
code that never runs. The fix in finding 1 removes it with the `open` list. The `sum <= 0` guard is reachable (all open
shares 0) and untested.

### 3. Boy scout — regex for a `%…%` value, `this.cs:102-104,133` (8df240b2f2) — minor, required by the rule
`AssignsOne` writes `%\S+%` as a variable, and `Placed()` finds each position by `text.IndexOf(v.Text, …)`. The repo rule: a
reference has one definition, `variable.parser`; never a regex or hand scan for `%…%`. Line 133-134 already asks the parser
(`.Whole is { IsBare: true }`) to re-judge what the regex matched, so the regex is the second answer. Scale to the smell: the
value after `=` is read by `parser.Read(at)` / `.Whole`; the literal alternatives (quoted text, number, true/false) can stay a
small pattern or become the type readers' job.

### 4. Pattern 3 (a choice on the element) — the pick's kind is switched on in the list — minor, older (fb14118e0, e10c4c4689)
`Listing` (`:290-301`) decides a pick's mark by `_items.FirstOrDefault(...)?.From is From.YesNo or From.Choice`, then by
`popular.ContainsKey`, then by score. "Is this pick certain?" is a question about the pick; each kind (`Common`, `YesNo`,
`Choice`, `Branch`) would answer it (`pick.Certain(list)`), and `Part` would belong to the two through-a-module kinds only.
Not a rename; larger than this branch. Not on `obp-cleanup.md` — proposal: add it (architect/docs write).

### 5. Pattern 11 (lazy) — `Take` works everything out at every ask — minor, older (be81913753, 763f8dbbd)
`:190-197`: items, questions, asked modules, `IsCondition`, `IsUnsure`, the listing, `Formal` and `Known` are all worked out
at each `Take`, stage 1's included, for properties read later. The "caller is sync" excuse is the smell's own tell
(`Listed`, `Formal`, `Known` are sync properties). Larger than this branch; tracked, not blocking.

### 6. Naming — `Part` — minor
A bare noun for "a module's share of the work the settled modules left". `Share` is the stage-1 word, `Part` hides which. If
finding 1's shape is taken, `Left(module)` reads as what it answers.

## Checks with no finding
- **Return early:** no nested guard in the touched methods (`Part`, `Listing`), none in the file.
- **Comments:** the new/changed comments state what is; no history or branch references.
- **Pattern 7 / 12 for `Settled`:** `Part` reuses the one set `Asked` uses; no second notion of settled.
- **Moved golden picks (judged):**
  - checkout step 3 `if %order.email% does not contain "@", throw …`: error.throw Possible → Certain. Right: the step says throw; condition.if (0.98) settles condition, `@also.error` confirms. (golden: `error.throw 0.95 (module error 0.48)`)
  - checkout step 5 `if %order.total% is not a number, throw …`: same, `(module error 0.52)`. Right.
  - loop step 2 `foreach …, call Ship`: goal.call 0.97 (module goal 0.23) Possible → Certain, loop settled by loop.foreach. Right: `call Ship` is goal.call.
  All three hold under finding 1's shape if the rest of each module split sits on the settled module (to re-read when re-pinned).
- **Known edge** (settled module's mass was for another of its own actions, `remove %x%, write to %y%`): the builder probes it; not mine.

## Pass 4.5
`Part` re-normalises stage 1's one-of "main work" choice into a many-of reading by hand (tell 9, two shapes translated). The
plan files the real fix (ask a many-of module question) as out of scope; fine. Finding 1 shows the cost of the translation: it
must not assume the distribution is whole.

## Verdict: NEEDS WORK
One blocking finding (1), with a smaller replacement and a test; the rest minor (3 is required by the boy scout rule, 4–5
tracked). Next: coder, then back to me.

---

# v2 — d30db6c03 (merged as 983bc7b38) — NEEDS WORK, narrow (tests only)

Scope: `pick/list/this.cs` whole again; `ModulePartTests.cs`. Fixtures green at the merge: ModulePartTests 3, PickListTests 8,
PickOptionTests 18, ConfirmTemplateTests 2, LineTwinTests 1, KeptStepTests 8, PrefillTests 5, KindFollowUpTests 2. `pick_golden.json`
is not in the v1→v2 diff: the three moved picks stay Certain under the new `Part`. Bench: skipped (build-time listing).

## v1 findings
- **1 (blocking) — fixed.** `Part` = share / (1 − the settled shares), 0 when nothing is left (`:504-512`). My no-probabilities
  probe is now `ARemoveAnsweredWithoutProbabilities_StaysPossible` (`{file 0.56}` alone: 0.56 Possible). The dead guard is gone.
  `ranked.Single(...)` is safe: a pick through a module comes from `Asked`, which reads `Ranked`; a missing module would throw,
  loudly, not silently.
- **3 (boy scout) — fixed.** `AssignsOne` and its `%\S+%` are gone; `Placed()` asks `parser.Read(at)` at each place; `Assigned()` reads
  the rest through `Literal` or one bare variable spanning it. Remaining regexes (`Assigns`, `Literal`) hold no `%…%`.

## The coder's question: a reference that opens but doesn't parse
Old `Scan` (`parser/this.cs:118-130`) jumps to the closing `%` of a broken reference (`i = close`); `Placed` tries the next place, so
`%x.%y%` now yields `y`. **It does not matter for behaviour:** `Placed` feeds `First`, `Target` and `Assigned`, which are hints for the
starting line; the formal Reader re-reads that line and "knows nothing" if it does not read, and a malformed reference is refused
through the parser's own `Error` elsewhere. One cost: for malformed text `Placed` and `parser.Variable` now answer differently about
which variables a step has. The one answer is a parser member that carries each variable's position; a parser addition goes to the
architect first, so: a proposal for the backlog, not a fix here.

## New findings
### 7. Deletion test — the two rewritten lines of `Assigned`/`Placed` are pinned by no test — NEEDS WORK (coder: tests)
Announced, reverted mutations (tree clean after each):
- **M2:** drop `&& one.Text.Length == value.Length` (`:138`, "one bare variable, the whole of what follows"). PrefillTests 5, KindFollowUpTests 2,
  PickOptionTests 18 all green. Consequence: for `set %x% = %x% + 1` (this branch's step) `Assigned()` would answer
  `(%x%, "%x% + 1")` and the known code would be `variable.set(Name=%x%, Value=%x% + 1)`. Nothing fails. (The old `\s*$` anchored it,
  also untested; v2 rewrote the line, so v2 pins it.)
- **M1:** drop `at += v.Text.Length - 1` (`:114`), so a reference's inside is read again as places. PickListTests 8, PickOptionTests 18,
  KeptStepTests 8, ModulePartTests 3, PrefillTests 5, KindFollowUpTests 2 green. `%a[%b%]%` would yield `b` as a second place.
- **Tests to add (each red on its mutation):** `Pick.Known` for `set %x% = %x% + 1` holds no `Value=%x% + 1` (variable.set without that value or no
  Name+Value set); `set %x% = %y%` holds `variable.set(Name=%x%, Value=%y%)`; a step whose first reference nests another
  (`set %a[%b%]% = 5`) yields one place.

## Other checks (v2 diff and the file whole)
- Return early: none nested in the touched methods. `Placed`: a guard `continue`, then the work.
- Comments state what is; no history. `Part`'s says what the share is of.
- Patterns 3 and 11 (v1 #4, #5): on the backlog, as ruled.

## Verdict v2: NEEDS WORK (narrow)
The code is right; two changed lines of the headline step's known-code path hold no test. Coder adds the tests (no production change
expected); then PASS and the tester.

---

# v3 — 37b2995fa — PASS (tester next)

Tests only (`ModulePartTests` +37 lines, 6/6); no production change. Re-ran the v2 mutations (announced, reverted, tree clean):
- **M2** (drop `&& one.Text.Length == value.Length`): `ASetOfASum_KnowsTheCapture` red (1 of 6). The coder pinned what M2 loses
  (the capture's `Value=%!data%`); my "no `Value=%x% + 1`" form would have survived, since the formal reader refuses that line.
- **M1, End shift** (`yield return (v, at + 1)` without the `at +=`): `ASetOfOneVariable_KnowsItAsTheValue` and
  `AVariablesPlace_EndsWhereItsTextEnds` red (2 of 6).
- **M1, skip only** (`yield return (v, at + v.Text.Length)`, End still right): 6/6 green. Agreed with the architect: the inner-variable
  skip has no production door; it goes with the backlog item "the parser answers each variable's place". The `at +=` line is
  pinned for its End; only its skip side waits.
- Return early, comments: unchanged and clean. `KnownSet` helper builds a real stage-1 answer; tests state what they pin.
No bench (build-time).
