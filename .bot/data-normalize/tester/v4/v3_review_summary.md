# tester v3 review summary

## What coder v4 said

Coder v4 disagreed with the v3 verdict. On a clean rebuild from origin
(`f1475688c`) both suites pass: C# 3381/3381, PLang 233/233 *including*
`/BuilderSanity/BuilderSanity.test.goal`. The committed `.pr` at origin
has `%items%` parsed as a real `[1, 2, 3]` list, so the string-atomicity
rule (`PLang/app/data/this.cs:341`) doesn't fire for the fixture as
checked in.

## What I (tester v3) actually did — and where the misattribution was

Before running `plang --test` I ran a builder sanity step:

```
cd /workspace/plang/Tests && plang build /BuilderSanity --cache=false
```

That rebuilt the BuilderSanity `.pr` files. The LLM run produced a `.pr`
where `%items%` was a literal string `'[1, 2, 3]'` (the goal-source form
is quoted), not a list. Then `plang --test` ran against my locally-modified
`.pr`, foreach yielded the whole string once, and `math.add` choked with
`FormatException: The input string '[1, 2, 3]' was not in a correct format.`

I committed only `.bot/` — not the bad `.pr` — so the symptom never
reached origin. I reverted the `.pr` regen *after* the failure was
already in /tmp/plang-test.out, so my failure data was real but its
**cause was my own pre-test builder run, not the branch**.

This is a tester-procedure failure on my part. The `/memory/feedback_validate_builder_before_plang_tests.md`
rule (build with cache=false to confirm builder works before trusting
plang --test) is meant to validate that a freshly-built fixture passes —
but I treated a randomly chosen test (BuilderSanity) as the validator
without checking it would survive a non-deterministic rebuild, and then
ran the full suite *against* my rebuild. The list-literal form coder v4
adopted removes the LLM nondeterminism for this fixture.

## What was changed

- `Tests/BuilderSanity/BuilderSanity.test.goal:7` —
  `set %items% = '[1, 2, 3]'` → `set %items% = [1, 2, 3]` (unquoted list
  literal).
- `Tests/BuilderSanity/.build/buildersanity.test.pr` — regenerated under
  the new shape.
- `.bot/data-normalize/coder/v4/baseline-tests.md` — process gap closed.

## What v4 needs to verify

1. The committed fixture passes on clean rebuild (no local builder runs
   touching BuilderSanity beforehand).
2. The new list-literal form is **stable across builder reruns** — that
   was the core failure mode v3 ran into.
3. The V1 fixture (`StoreView_PropagatesIntoInnerDataProperties_NotHardcodedToOut`)
   still mutation-catches — re-verify since v4 didn't touch json.Writer
   but rebuild changes the binary.
4. Coder v4 wrote the missing `baseline-tests.md`. Recognize the process
   gap as closed.

## Lesson worth memorising

For future runs: do the cache=false builder check on a **different**
fixture from the ones tester is about to grade, OR check `git status`
*before* running `plang --test` and revert any locally-rebuilt `.pr`
files first. The "validate the builder works" step shouldn't bleed
non-deterministic LLM output into the test fixtures whose pass/fail
state is the subject of the report.
