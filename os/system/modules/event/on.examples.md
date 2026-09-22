Step text: `before step, call LogStep, on goal pattern 'Api/*'`
Properties: `{"Trigger": "BeforeStep", "GoalToCall": {"name": "LogStep"}, "GoalPattern": "Api/*"}`

Step text: `on ask on "input" channel call CaptchaGoal, write to %bindingId%`
Properties: `{"Trigger": "OnAsk", "ChannelName": "input", "GoalToCall": {"name": "CaptchaGoal"}}` — the trailing `write to %bindingId%` is its own action.

Step text: `before write on "output" channel call LogOutput`
Properties: `{"Trigger": "BeforeWrite", "ChannelName": "output", "GoalToCall": {"name": "LogOutput"}}`

Step text: `before each goal call LogBefore`
Properties: `{"Trigger": "BeforeGoal", "GoalToCall": {"name": "LogBefore"}}`

Step text: `after each goal call LogAfter`
Properties: `{"Trigger": "AfterGoal", "GoalToCall": {"name": "LogAfter"}}`

Step text: `before action variable.set call OnVarSet`
Properties: `{"Trigger": "BeforeAction", "ActionPattern": "variable.set", "GoalToCall": {"name": "OnVarSet"}}`

Every `event.on` carries a `Trigger` (the moment) and a `GoalToCall` (the goal to register).
`GoalToCall` is always the object `{"name": "<GoalName>"}`, never a bare identifier.

"each goal" / "each step" / "action" pick BeforeGoal/AfterGoal, BeforeStep/AfterStep,
BeforeAction/AfterAction. The channel moments (OnAsk, BeforeWrite/AfterWrite, BeforeRead/AfterRead)
also take a `ChannelName`.

These are BINDINGS: the goal is registered to run when the event happens, not called now.
