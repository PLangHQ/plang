# set-add-order — builder measurement (binary at f3d89adf6, cache skipped, scratch apps, fresh build each)

| # | step | result |
|---|------|--------|
| 1 | `set %x% = %x% + 1` x5 | math.add, variable.set — 5/5 (was 1/5) |
| 2 | `remove %x%` alone x5 | file.delete 5/5 (miss; see below) |
| 2b | `set %x% = 5` then `remove %x%` x5 | variable.remove 5/5 |
| 3 | `remove %x%, write to %y%` x3 | file.delete + variable.set 3/3 |
| 4 | doc/events.md Mark example, 5 builds | runs, prints `marks: 3` 5/5; Mark's step is math.add then variable.set |

Miss, #2: `[0] - remove %x% => decider: file.delete 0.94 (module file 0.94) => formal: file.delete(Path)`.
Stage 1: s0_@module file 0.94, variable 0.03; all common actions <= 0.06. Module share 0.94 is >= 0.9 either way, so the fix does not change this
(not measured on the parent binary). With %x% set on an earlier line the decider chooses variable.

Finding #4: the Mark example builds `on.event(Event=%!app.type.step.on.start%, When=before, goal.call Mark)` 5/5, not
`%!app.module.output.write.on.start%`: "before action output.write" built as every step. The output is 3 by coincidence
(the three write steps follow the bind). Not fixed; teaching is the architect's call.
