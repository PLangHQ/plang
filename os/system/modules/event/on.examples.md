Step text: `before step, call LogStep, on goal pattern 'Api/*'`
Properties: `{"Trigger": "BeforeStep", "Goal": {"module": "goal", "name": "call", "parameter": [{"name": "Name", "value": "LogStep"}]}, "GoalPattern": "Api/*"}`

Step text: `on ask on "input" channel call CaptchaGoal, write to %bindingId%`
Properties: `{"Trigger": "OnAsk", "ChannelName": "input", "Goal": {"module": "goal", "name": "call", "parameter": [{"name": "Name", "value": "CaptchaGoal"}]}}` — the trailing `write to %bindingId%` is its own action.

Step text: `before write on "output" channel call LogOutput`
Properties: `{"Trigger": "BeforeWrite", "ChannelName": "output", "Goal": {"module": "goal", "name": "call", "parameter": [{"name": "Name", "value": "LogOutput"}]}}`

Step text: `before each goal call LogBefore`
Properties: `{"Trigger": "BeforeGoal", "Goal": {"module": "goal", "name": "call", "parameter": [{"name": "Name", "value": "LogBefore"}]}}`

Step text: `after each goal call LogAfter, level="info"`
Properties: `{"Trigger": "AfterGoal", "Goal": {"module": "goal", "name": "call", "parameter": [{"name": "Name", "value": "LogAfter"}, {"name": "Parameter", "value": [{"name": "level", "value": "info"}]}]}}`

Step text: `before action variable.set call OnVarSet`
Properties: `{"Trigger": "BeforeAction", "ActionPattern": "variable.set", "Goal": {"module": "goal", "name": "call", "parameter": [{"name": "Name", "value": "OnVarSet"}]}}`

Every `event.on` carries a `Trigger` (the moment) and a `Goal` — the `goal.call` action to run when
it fires, in the same shape as any other goal.call (`Name`, and its arguments in `Parameter`).

"each goal" / "each step" / "action" pick BeforeGoal/AfterGoal, BeforeStep/AfterStep,
BeforeAction/AfterAction. The channel moments (OnAsk, BeforeWrite/AfterWrite, BeforeRead/AfterRead)
also take a `ChannelName`.

These are BINDINGS: the call is registered to run when the event happens, not run now.
