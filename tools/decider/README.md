# Decider harness — builder accuracy measurement

Recovered from the session transcript after a restart wiped the scratchpad. **Lives in the repo
now, not the scratchpad**, because the scratchpad does not survive a restart and this took a long
time to get right.

## What it measures

Accuracy of the typesafe two-stage pipeline against the labels — the `Tests/**/.pr` files, kept at
`labels/<same path under Tests/>` (e.g. `Tests/Simple/.build/start.test.pr` →
`labels/Simple/.build/start.test.pr`) so each maps back to its `.goal` under `Tests/`:

- **stage 1** — `noul` per (step, module) → the module SET for each step
- **stage 2** — `choice` per (step, module-in-set) → the action for each (step, module)
- **stage 3** — parameters, via gpt-5.4-nano (NOT the decider): `stage3.py` / `stage3b.py`

## Build the builder's .pr files — `build_pr.py`

The python stand-in for the plang builder (`os/system/builder/**`, which is the same pipeline written
in plang) until the runtime can run it. Stage 3 reads `os/system/builder/llm/Properties.llm`
verbatim, so tuning that prompt here tunes the plang builder too.

    python3 build_pr.py            # every .goal under os/system/builder, all goals in parallel
    WORKERS=4 python3 build_pr.py  # fewer parallel goals

Output goes to `out/<folder>/.build/<name>.pr` (+ `.raw.json`: menu + stage-3 answer per goal) —
a staging tree, never over the live `.pr` files. Every prompt sent is written, per goal, to
`/shared/coder/llm/plang/<file>/<Goal>/` (`1.decider.*`, `2.decider.*`, `3.llm.*`).

Keys: `TYPESAFE_API_KEY` (env, else `/shared/hopkaup/secrets/typesafe.txt`), `OPENAI_API_KEY`.

## Run

    python3 harness.py <runname> <goal-limit> <seed>   # writes runs/<runname>.jsonl
    python3 report.py  <runname>                        # scores it, sweeps thresholds

Raw answers are kept in `runs/*.jsonl` so re-scoring and threshold sweeps cost no API calls.

## Rules that govern this work (Ingi's, verbatim where quoted)

1. **Accuracy is the product.** "a programming language needs to do what the programmer expects it
   to do." A remaining miss is not acceptable — "these are all bad to miss. we cannot just glaze
   over it." 117/118 is not accuracy.
2. **There is no syntax.** `write to %x%` is how *we* happen to write it — it could be Icelandic or
   Chinese. Never parse step text for tokens; the INTENT is what maps.
3. **A `.pr` is not ground truth.** Several were found wrong (hallucinated asserts, empty
   `condition.if`, invented `elseif`, dropped goal-call arguments) and hand-fixed. Judge, or ask.
4. **Only plang types in a `.pr`** — `text`, `item`, `number`, … never `object`/`tstring`.
5. **Report with numbers, tables, YES/NO.** Not prose.

## State at recovery

Smoke run (5 goals / 7 steps) works end to end. Both misses were threshold-adjacent
(0.45–0.46 against a 0.5 cut), one of them the Icelandic step — i.e. calibration, not
comprehension. The larger settled corpus needs re-running to re-establish the baseline.
