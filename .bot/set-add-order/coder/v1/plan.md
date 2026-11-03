# set-add-order — coder v1 — stage 1

The architect's plan: `.bot/set-add-order/architect/plan/start.md`.

1. `PLang/app/goal/step/pick/list/this.cs`: a private `Part(module)`, the module's share of the open work: its stage-1
   share over the summed shares of the `Ranked()` modules that are not `Settled`, and 0 when the module is settled or
   nothing is open. The local `Sure` (which shared a name with the const `Sure`) becomes `Certain` and reads `Part`.
   Its comment now says what holds.
2. Tests: `ModulePartTests`. (a) `set %x% = %x% + 1` makes math.add Certain and adds first. (b) `remove %x%` keeps
   file.delete Possible and off the line.
3. The decider fixtures are re-run, and any moved pin is re-pinned through `AcceptTheFixture` and listed.
