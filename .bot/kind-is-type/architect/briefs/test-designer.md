# test-designer: kind-is-type

Traces to: decisions 743–749 in `.bot/kind-is-type/architect/summary.md`; the plan is `test/plan/kind-is-type/start.md` with its index `start.goal`.

## Why you first

Ingi (2026-10-14): "I think we should start with test-designer". Each stage's tests come from the plan before the code, so they prove what we asked for, not what got built. They go red now; the coder makes them green.

## What to do

- Read `start.md` (the rulings, the stages, the checks) and `start.goal` (each behaviour with the test that proves it).
- Write the tests for **stages 1 to 4**, at the paths `start.goal` names. Stage 0 is the coder's, already under way with its own pins.
- Each test is a plang test that builds from `test/` and fails today for the reason its behaviour names, not for an unrelated one. Where a behaviour can't be a plang test (a builder measurement, a C# boundary), write it as a C# test and say so in `start.goal`'s comment.
- For stage 4 the builder bot measures (5 builds, fresh, `"cache":"skip"`, jq); you write the plang tests that run what those steps build.
- Keep the plang high level where the builder can't build it yet, and say so: a test that can't build today is "red because unbuildable", which is fine for stage 4 and not for stages 1–3.

## Output

The tests under `test/plan/kind-is-type/`, built and run from `test/`, and a table in `.bot/kind-is-type/test-designer/tests.md`: test, stage, why it's red today (quoted), plang or C#.

## In plang

```plang
Write kind-is-type's tests from its plan, stages 1–4 (decision 749)
Start
- read test/plan/kind-is-type/start.md and start.goal
- write each test start.goal names for stages 1–4, at its path
- build each from test/ and run it: it fails today for the reason its behaviour names
- list each test, its stage, and why it's red (quoted) in .bot/kind-is-type/test-designer/tests.md
LIMITS: tests only; no production C#, no teaching edits; never hand-edit a .pr; stage 0 is the coder's
DONE WHEN: every stage 1–4 test exists, builds (or is marked red-because-unbuildable for stage 4), and is red for its own reason
REPLY: a message to the architect in the Done/HASH/CHECK/OPEN shape, text first
WHY: the tests come from the plan before the code, so they prove what Ingi asked for

/ a test that's red for the wrong reason
/ - set %n% = "1.5" as decimal          builds Type=number today (the baseline), so a test asserting %n!type.kind% is decimal fails for the right reason
/ - a test that fails "file not found" because its fixture is missing is red for the wrong reason: fix the fixture
```
