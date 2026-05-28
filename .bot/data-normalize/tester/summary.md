# tester — data-normalize

## Version
v4 (current). v3 → FAIL was retracted by v4 — see `v3_review_summary.md`
under `v4/`.

## What this is
Final tester pass on data-normalize. Validates that the data-normalize
branch (Normalize + IWriter + As<T> reconstruction, M1-M3 closures, V1
view-threading fix) is honest and that the v3 BuilderSanity red was a
tester-procedural failure, not a branch defect.

## What was done (v4)
- Clean rebuild per stale-binary protocol.
- C# suite: **3381 / 3381 pass**.
- PLang suite: **233 / 233 pass** including BuilderSanity (which v3
  flagged red).
- **Robustness check:** rebuilt `BuilderSanity.test.pr` three times in
  succession with `cache=false`. All three produced byte-identical .pr
  files (md5 `0d66eb02b8f30b03461a128a69a96218`) and the test passed each
  time. Coder v4's unquoted list-literal rewrite (`set %items% = [1, 2, 3]`)
  removed the LLM nondeterminism that bit v3.
- **V1 fixture re-mutation:** temporarily replaced `_view` with
  `app.View.Out` in `json.Writer.EndRecord` and reran the new fixture
  `StoreView_PropagatesIntoInnerDataProperties_NotHardcodedToOut`. Test
  failed at the Store assertion as expected (PRIV-must-persist missing
  from store bytes). Reverted; source diff clean.
- Coder v4 wrote the missing `baseline-tests.md` — process gap closed.

## What changed since v3
- `Tests/BuilderSanity/BuilderSanity.test.goal:7` — unquoted list literal.
- `Tests/BuilderSanity/.build/buildersanity.test.pr` — regenerated.
- `.bot/data-normalize/coder/v4/baseline-tests.md` — added.

## v3 misattribution (lesson learned)
v3 ran `plang build /BuilderSanity --cache=false` as a builder-validation
step before `plang --test`. That rebuild produced a bad `.pr`
(`%items%` as a string literal instead of a list), which then failed
the suite. The bad `.pr` never reached origin because v3 committed only
`.bot/`. v3 still issued a FAIL verdict and attributed the cause to the
branch — incorrect. Lesson saved to memory:
`feedback_builder_check_not_on_graded_fixture.md` — pick a throwaway
fixture for the builder check, never a graded one, or `git checkout`
the .pr before running plang --test.

## Code example — what changed in the fixture
```diff
- - set %items% = '[1, 2, 3]'   # quoted: builder might JSON-parse, might not
+ - set %items% = [1, 2, 3]     # unquoted: unambiguous PLang list literal
```
With the unquoted form, three cache=false rebuilds produced identical
.pr output and the test passed each time.

## Verdict
**PASS.** Data-normalize is ready for the next bot.
