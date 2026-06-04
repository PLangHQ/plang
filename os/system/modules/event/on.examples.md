Step text: `before step, call LogStep, on goal pattern 'Api/*'`
Mapping: `event.on Trigger([trigger] BeforeStep), GoalToCall([goal.call] LogStep), GoalPattern([string] Api/*)`

Step text: `on ask on "input" channel call CaptchaGoal, write to %bindingId%`
Mapping: `event.on Trigger([trigger] OnAsk), ChannelName([string] "input"), GoalToCall([goal.call] CaptchaGoal) | variable.set Name(%bindingId%), Value(%!data%)`

Step text: `before write on "output" channel call LogOutput`
Mapping: `event.on Trigger([trigger] BeforeWrite), ChannelName([string] "output"), GoalToCall([goal.call] LogOutput)`

Step text: `before each goal call LogBefore`
Mapping: `event.on Trigger([trigger] BeforeGoal), GoalToCall([goal.call] {name: "LogBefore"})`

Step text: `after each goal call LogAfter`
Mapping: `event.on Trigger([trigger] AfterGoal), GoalToCall([goal.call] {name: "LogAfter"})`

Step text: `before action variable.set call OnVarSet`
Mapping: `event.on Trigger([trigger] BeforeAction), ActionPattern([string] variable.set), GoalToCall([goal.call] {name: "OnVarSet"})`

Every event.on MUST carry a `Trigger` (the lifecycle/channel moment) AND a
`GoalToCall` (the goal to register). The `GoalToCall` value is the goal-call
OBJECT `{name: "<GoalName>"}` — NOT the bare identifier and NEVER the literal
`goal.call` token or `goal.call(<name>)` (that token is the action's TYPE, not the
value). Never drop the Trigger and never collapse GoalToCall into a `goal`/`goalName`
param. The "each goal"/"each step"/"action" lifecycle phrasings pick
BeforeGoal/AfterGoal, BeforeStep/AfterStep, BeforeAction/AfterAction respectively.

"on X" / "before X" / "after X" step phrasings all map to event.on with the
appropriate Trigger (OnAsk, BeforeWrite, BeforeStep, AfterAction, etc.). The
channel-related events (OnAsk, BeforeWrite/AfterWrite, BeforeRead/AfterRead)
take a ChannelName filter. These are EVENT BINDINGS — they do not invoke
the goal immediately; they register a handler to fire when the event
happens. Never map to output.ask, environment.run, or variable.set.
