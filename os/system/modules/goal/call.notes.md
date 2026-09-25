Name — the goal to call, copied exactly as the step writes it: `Goal`, `Folder/Goal`, `/system/builder/Goal`. Never an action name like `goal.call`.
Parameter — the arguments: every `name=value` after the goal name is one argument, its value as written. Left out when the step passes none.
Actor — the actor the goal runs on, only when the step names one (`on actor "system"`, `as user`). Not because the goal is a system goal, a sub-goal or a recovery.
Parallel — true only when the step says the call may run beside others.

- The arguments belong to the called goal; they are never properties of goal.call.
- `foreach %list%, call X item=%y%` is loop.foreach, then goal.call with `item` as its argument.
