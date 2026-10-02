Name — the goal to call, copied exactly as the step writes it, as a quoted text: `goal.call(Name="Finalize")`, `goal.call(Name="Folder/Goal")`, `goal.call(Name="/system/builder/EmitBuildEvent")` — never bare (`Name=Finalize`). Never an action name like `goal.call`.
Parameter — the arguments: every `name=value` after the goal name is one argument, its value as written. Left out when the step passes none.
Actor — the actor the goal runs on, only when the step names one (`on actor "system"`, `as user`). Not because the goal is a system goal, a sub-goal or a recovery.
Parallel — true only when the step says the call may run beside others.

- The arguments belong to the called goal; they are never properties of goal.call.
- `foreach %list%, call X item=%y%` is loop.foreach, then goal.call with `item` as its argument.
- `call goal X` and `call the goal X` call X: the leading `goal` / `the goal` is only a word, never part of the name or an argument — the same whether X is a bare name, a `Folder/Goal`, or a `/path/name`. `call goal Unmatched action=%item%` → goal.call(Name="Unmatched", Parameter={action: %item%}); `call goal /builder/Build` → goal.call(Name="/builder/Build") (the path is the whole name, `goal` dropped). A name that itself starts with Goal keeps it: `call goal GoalSetup` → Name="GoalSetup".
