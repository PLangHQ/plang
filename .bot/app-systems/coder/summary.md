# coder — app-systems

**Version:** v1

## What this is
app-systems makes every `app.X` the type X (a generic `type<X>` over its concept's `list<X>`), so the
plang path, the C# path and the file path agree (`%!app.type["text"]%` ↔ `app.type.Get("text")` ↔
`app/type/this.cs`). The architect's plan (`.bot/app-systems/architect/plan.md`) lays it out in 13
stages; v1 is the review of the plan against the code and stage 0, the base.

## What was done
- **Review** (`v1/result.md`): the plan's file:line claims for stages 1, 3 and 4 checked. Most hold. Not
  holding: the generator emits one `await Run()` for every handler, so stage 1 can't defer the five
  handlers later stages delete; timer's handler class is named `Start` (CS0542); `callback.run` is a
  third plang action named `run`; a type's navigation hands everything to a `clr` reflection (no
  "members first"); the registry's context also serves `App.Format`; more spelling/alias sources and
  list mutators than listed. Sent to the architect (plang-40).
- **Stage 0:**
  - Compile re-recorded; the python twin now refuses a missing entry per step, as C# does
    (`tools/decider/prompt_c.py`). BootstrapTests passes.
  - Baseline: `v1/baseline-tests.md`.
  - 552 deprecated v0.1 `NN. stepname.pr` / `00. Goal.pr` files under `os/` deleted.
- **Open:** 98 tracked v0.1 `NN. stepname.dll`/`.pdb` under `os/apps/*/.build` — not in the ruling;
  asked. Next: stage 1 once the architect answers the review.

## Code example
python mirror of C#'s refusal of a missing entry (`tools/decider/prompt_c.py`, `check`):
```python
# a step the answer leaves out is that step's refusal, as step.list.Read has it — the others stand
for i in range(len(steps)):
    if i not in parsed: per_step.setdefault(i, []).append(f'step {i} ("{steps[i]["text"]}") has no entry')
```
