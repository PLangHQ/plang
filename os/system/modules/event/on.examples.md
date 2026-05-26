Step text: `before step, call LogStep, on goal pattern 'Api/*'`
Mapping: `event.on Type([eventtype] BeforeStep), GoalToCall([goal.call] LogStep), GoalPattern([string] Api/*)`

Step text: `on ask on "input" channel call CaptchaGoal, write to %bindingId%`
Mapping: `event.on Type([eventtype] OnAsk), ChannelName([string] "input"), GoalToCall([goal.call] CaptchaGoal) | variable.set Name(%bindingId%), Value(%!data%)`

Step text: `before write on "output" channel call LogOutput`
Mapping: `event.on Type([eventtype] BeforeWrite), ChannelName([string] "output"), GoalToCall([goal.call] LogOutput)`

"on X" / "before X" / "after X" step phrasings all map to event.on with the
appropriate Type (OnAsk, BeforeWrite, BeforeStep, AfterAction, etc.). The
channel-related events (OnAsk, BeforeWrite/AfterWrite, BeforeRead/AfterRead)
take a ChannelName filter. These are EVENT BINDINGS — they do not invoke
the goal immediately; they register a handler to fire when the event
happens. Never map to output.ask, environment.run, or variable.set.
