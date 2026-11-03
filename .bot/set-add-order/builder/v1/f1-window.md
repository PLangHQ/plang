# F1 window — do real steps reach it? (binary at set-add-order 37b2995fa-era, `./dev.sh build`, scratch app, cache skip)

Rows: f1-rows.txt (shares, variable.set yes/no, the listing's decider line, built actions). Captures: f1-captures.txt (stage 1's answer line and the `[0] -` line per run).

The window: variable.set settled (>= 0.9), the destructive module's plain share < 0.9, Part >= 0.9.

| step | plain share (3 runs) | variable.set yes/no | marks | built |
|---|---|---|---|---|
| 1 `remove %x%, write to %y%` | file 0.90 / 0.90 / 0.91 | 0.86 / 0.85 / 0.84 | file.delete Certain (plain >= 0.9); variable.set possible | file.delete, variable.set 3/3 |
| 2 `delete %x%, write to %y%` | file 1.00 / 0.99 / 0.98 | 0.79 / 0.81 / 0.75 | file.delete Certain; variable.set possible | file.delete, variable.set 3/3 |
| 3 `clear %x%, write to %y%` | variable 0.52 / 0.49 / 0.47, output 0.34-0.36, file 0.13-0.17 | 0.86 x3 | variable.clear 0.96, output.write 0.90-0.93 both possible (module variable<0.9) | variable.clear, output.write, variable.set 3/3 |
| 4 `remove %item% from %list%, write to %list%` | list 0.99 x3 | 0.89 / 0.93 / 0.92 | list.remove Certain; variable.set possible, then Certain (runs 2,3) | list.remove, variable.set 3/3 |
| 5 `remove %path%, write to %result%` | file 1.00 x3 | 0.90 / 0.87 / 0.85 | file.delete Certain; variable.set Certain (run 1), possible (2,3) | file.delete, variable.set 3/3 |
| 6 `delete the file %x%, write to %ok%` | file 1.00 x3 | 0.88 / 0.89 / 0.89 | file.delete Certain | file.delete, variable.set 3/3 |
| 7 `set %x% = %x% + 1` (control) | math 0.55 / 0.59 / 0.52, variable 0.45-0.41 | 0.99 / 0.98 / 0.99 | variable.set Certain; math.add 0.98 Certain (Part) | math.add, variable.set 3/3 |

Runs in the window (variable.set >= 0.9 AND destructive plain < 0.9): none of the 21. Where variable.set was settled (4 run2/3, 5 run1, 7) the plain share was
0.99 / 1.00 / math 0.52-0.59 (the intended gain). Closest: step 1, file at 0.90 on the line, but variable.set 0.84-0.86 is not settled, so Part removes nothing.
The window needs a step that is a destructive word on a name AND a settled write-to: the decider answers variable.set at 0.75-0.90 for a `write to` with a name it can't
read as a result, and file is >= 0.9 there, so the two don't coincide today. Not proof: 21 runs, one phrasing each.

Side finding, step 3: `clear %x%, write to %y%` builds `output.write` as an extra action (0.90-0.93 possible) beside variable.clear. Unrelated to Part (module variable 0.47-0.52 -> Possible
under both rules). Backlog, decider teaching.
