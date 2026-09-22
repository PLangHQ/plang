# Decider harness — builder accuracy measurement

Recovered from the session transcript after a restart wiped the scratchpad. **Lives in the repo
now, not the scratchpad**, because the scratchpad does not survive a restart and this took a long
time to get right.

## What it measures

Accuracy of the typesafe two-stage pipeline against `Tests/**/.pr` as labels:

- **stage 1** — `noul` per (step, module) → the module SET for each step
- **stage 2** — `choice` per (step, module-in-set) → the action for each (step, module)
- **stage 3** — parameters, via gpt-5.4-nano (NOT the decider): `stage3.py` / `stage3b.py`

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
