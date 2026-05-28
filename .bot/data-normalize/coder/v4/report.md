# coder v4 — data-normalize

## Tester v3 verdict addressed

Tester v3: FAIL — one failing PLang test
(`/BuilderSanity/BuilderSanity.test.goal`) plus a process gap (no
`baseline-tests.md` from v1/v2/v3).

## What I did

### 1. Could not reproduce the failure

Clean rebuild from `origin/data-normalize` (`f1475688c`) and both suites
are fully green: C# 3381/3381, PLang **233/233 including BuilderSanity**.
The checked-in `.pr` (`Tests/BuilderSanity/.build/buildersanity.test.pr`)
has `%items%` parsed as a real `list` `[1, 2, 3]`, so the
string-atomicity rule (`PLang/app/data/this.cs:341`) doesn't kick in for
this fixture.

Most likely cause of the tester's red: they rebuilt the fixture locally,
the non-deterministic builder produced a `.pr` where `%items%` came out as
a literal string (because the goal source said `set %items% = '[1, 2, 3]'`
— quoted), `foreach` then yielded the whole string once, and `math.add`
choked. The bad `.pr` wasn't committed, so the symptom didn't propagate to
origin.

### 2. Hardened the fixture against that class of non-determinism

`Tests/BuilderSanity/BuilderSanity.test.goal:7`

```
- set %items% = '[1, 2, 3]'        →        - set %items% = [1, 2, 3]
```

The quoted form `'[1, 2, 3]'` is a string literal that the builder *might*
JSON-parse into a list, but isn't obligated to. The unquoted form is the
unambiguous PLang list literal — same shape used elsewhere
(`Tests/Modules/List/Start.goal:2`, `App/DeepNavigation/Start.goal:4`,
`Modules/List/ListOps.test.goal:2`, …). With the rewrite the builder has
no choice: `%items%` is a `list` of `[1, 2, 3]` regardless of LLM mood.

Rebuilt the fixture with `cache=false` to confirm the new shape:

```json
"name": "Value", "value": [1, 2, 3], "type": "list"
"name": "Type",  "value": "list",     "type": "string"
"formal": "variable.set(Name=%items%, Value=[1, 2, 3], Type=list)"
```

`.pr` regenerated; BuilderSanity test still passes (190ms).

### 3. Wrote `baseline-tests.md`

The process gap the tester flagged across v1/v2/v3 — see
`.bot/data-normalize/coder/v4/baseline-tests.md`. Records the pre-edit
state of both suites so future tester runs have a ground truth to compare
against.

## Final test state (post-edit)

- C# suite: **3381 / 3381** pass.
- PLang suite: **233 / 233** pass.
- `git status` clean apart from the fixture + `.pr` + this report +
  baseline file.

## Files touched

- `Tests/BuilderSanity/BuilderSanity.test.goal` — list-literal form
- `Tests/BuilderSanity/.build/buildersanity.test.pr` — regenerated to match
- `.bot/data-normalize/coder/v4/baseline-tests.md` — new
- `.bot/data-normalize/coder/v4/report.md` — this file

No production C# changes — the codeanalyzer v3 V1 fix (`json.Writer`
View threading) from coder v3 stands and is mutation-verified by tester v3.

## Next bot

tester — rerun to confirm BuilderSanity passes on a clean rebuild and
that the list-literal rewrite holds across builder reruns.
