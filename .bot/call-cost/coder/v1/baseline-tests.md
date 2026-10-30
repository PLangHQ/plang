# call-cost baseline (cut from runtime2 7bf093245 + the architect's plan, 77add2895)

C# (`./dev.sh test`): Modules 1500, Types 1042, Wire 641, Data 881, Generator 191, Runtime 1489 — all green, 0 failing.
plang (`plang --test` in `test/`): 323 total, 322 pass, 0 fail, 0 stale, 1 skipped.

The C# sweep compiled at its start, before the first edit; the plang run's build may have held the step cache's edit
(made while the sweep ran). Event-action's merge gate on 7bf093245 gave the same plang numbers.
