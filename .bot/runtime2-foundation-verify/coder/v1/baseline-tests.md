# Baseline tests — 2026-05-11

Captured before any code changes on this branch.

## PLang (`plang --test`)
- Total: 200, Pass: 199, Fail: 1
- Pre-existing failure: `Code/HelloPlain.test.goal` — leftover from the `modules.code.run/v1` branch (uncommitted file under `Tests/Code/`). Unrelated to this stage. Not my regression.

## C#
Running in background, see end of session.

## Implication
Any test that goes red on this branch outside of `Tests/Code/HelloPlain.test.goal` is my regression. My stage adds three *new* `.test.goal` files under `Tests/Errors/` — pure additions, zero existing files touched. Expected delta: +3 tests, +3 pass.
