# Baseline — before any edit (branch `ireader-read-path`, off compare-redesign HEAD)

Captured with `./dev.sh full` + `cd Tests && plang --test`. Working tree clean
(only `.bot/ireader-read-path/` untracked).

## C# suite — `./dev.sh full` → all green, 0 errors
| project   | total | result |
|-----------|-------|--------|
| Modules   | 973   | green  |
| Types     | 726   | green  |
| Wire      | 495   | green  |
| Data      | 937   | green  |
| Generator | 203   | green  |
| Runtime   | 789   | green  |
| **sum**   | 4123  | green  |

Build succeeds (warnings only: nullable CS8604 in generated code, NU1902
ImageSharp advisory, TUnit0023/CS4014 in test code — all pre-existing).

## plang suite — `plang --test` → HARD ABORT (pre-existing, not my change)
Runner aborts on the first goal:
```
CreateDeclined(400) at Test - /test.goal:4
variable.set: %Name% holds a text — a variable names a thing; it is born typed
(declare 'type:variable'), never created from a value.
```
This is the in-flight born-typed `variable.set` work on compare-redesign — the
runner never reaches a result summary. **Pre-existing**: tree is clean, I've
changed nothing. The plang suite cannot give regression signal on this base.

## Regression strategy for this change
Lean on the C# **Wire (495)** and **Data (937)** suites — they directly cover
deserialization, lazy materialization, `@schema:data`, signature-layer read, and
verbatim passthrough (the exact surface this change touches). Watch specifically:
`Wire/**`, `Data/App/LazyDeserialize/**`, Stage3 verbatim/never-narrowed, binary/
kind decode (json→dict, csv→table, image, md→text, .pr→goal). Bar: **zero new
C# failures** vs the green baseline above.

Known-skipped (do NOT expect to clear): snapshot-redesign (3), archive-as-layer
(8), pure-lazy source-gen (8) — per stage-11a handoff.
