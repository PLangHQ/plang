`goal.call` runs another goal. It is the action behind `call X` wherever that clause appears — on its own, as the body of a condition, or as the body of a loop. The surrounding clause compiles to its own action; `call X` is always `goal.call`.

Step text: `call Finalize`
Mapping: `goal.call GoalName([goal.call] Finalize)`

Step text: `call ProcessOrder id=%orderId%, retries=3`
Mapping: `goal.call GoalName([goal.call] ProcessOrder)` — `id=%orderId%` and `retries=3` are the goal's parameters; they live inside `GoalName.parameters`, not as peers.

Step text: `if %total% > 5, call MarkBig`
Mapping: `condition.if Left([object] %total%), Operator([operator] >), Right([int] 5) { goal.call GoalName([goal.call] MarkBig) }` — TWO modules: `condition` for the guard, `goal` for the body.

Step text: `if %x% is "yes", call WhenYes, else call WhenNo`
Mapping: `condition.if … { goal.call GoalName([goal.call] WhenYes) } | condition.else { goal.call GoalName([goal.call] WhenNo) }`

Step text: `foreach %items%, call HandleItem item=%item%`
Mapping: `loop.foreach Collection([object] %items%) | goal.call GoalName([goal.call] HandleItem)` — `loop` for the iteration, `goal` for the body; `item=%item%` is a `goal.call` parameter.

Step text: `call /system/builder/EmitBuildEvent kind="done"`
Mapping: `goal.call GoalName([goal.call] /system/builder/EmitBuildEvent)` — a path-qualified name is still a plain `goal.call`.
