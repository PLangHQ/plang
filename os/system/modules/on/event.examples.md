Step text: `before the app starts, call Say`
Formal: `on.event(Event: on = %!app.on.start%, When: choice<when> = "before", Action: action = goal.call(Name: goal = "Say"))` — the app itself is `%!app%`; its start is `%!app.on.start%`.

Step text: `before each goal call LogBefore`
Formal: `on.event(Event: on = %!app.type.goal.on.start%, When: choice<when> = "before", Action: action = goal.call(Name: goal = "LogBefore"))`

Step text: `before goal Process, call Prepare`
Formal: `on.event(Event: on = %!app.goal["Process"].on.start%, When: choice<when> = "before", Action: action = goal.call(Name: goal = "Prepare"))` — a goal the step names is `%!app.goal["Name"]%` (its bare name), not `%!app.type.goal%`, which is every goal.

Step text: `after action llm.query, call ShowCost`
Formal: `on.event(Event: on = %!app.module.llm.query.on.start%, When: choice<when> = "after", Action: action = goal.call(Name: goal = "ShowCost"))` — an action the step names as module.action is `%!app.module.<module>.<action>%`, not `%!app.type.action%`, which is every action.

Step text: `after step call TrackAfterStep`
Formal: `on.event(Event: on = %!app.type.step.on.start%, When: choice<when> = "after", Action: action = goal.call(Name: goal = "TrackAfterStep"))`

Step text: `before action file.read call OverrideFileRead`
Formal: `on.event(Event: on = %!app.module.file.read.on.start%, When: choice<when> = "before", Action: action = goal.call(Name: goal = "OverrideFileRead"))`

Step text: `after action output.write call CaptureWrite`
Formal: `on.event(Event: on = %!app.module.output.write.on.start%, When: choice<when> = "after", Action: action = goal.call(Name: goal = "CaptureWrite"))`

Step text: `after each goal call LogAfter, level="info"`
Formal: `on.event(Event: on = %!app.type.goal.on.start%, When: choice<when> = "after", Action: action = goal.call(Name: goal = "LogAfter", Parameter: list = [{level: item = "info"}]))`

Step text: `before write on "audit" channel call ApprovalGoal`
Formal: `on.event(Event: on = %!channel.audit.on.write%, When: choice<when> = "before", Action: action = goal.call(Name: goal = "ApprovalGoal"))`

Step text: `on ask on "input" channel call CaptchaGoal, write to %event%`
Formal: `on.event(Event: on = %!channel.input.on.ask%, When: choice<when> = "before", Action: action = goal.call(Name: goal = "CaptchaGoal"))` — the trailing `write to %event%` is its own action.

Step text: `on click on #window.bot, call ShowClaude`
Formal: `on.event(Event: on = %!screen.element["#window.bot"].on.click%, When: choice<when> = "after", Action: action = goal.call(Name: goal = "ShowClaude"))` — `#…` in a step is an element of the screen in play, `%!screen.element["#…"]%`: a window's own part (`#window.bot` is its ☰, `#window.save` its save tool) or a page's id. The call reads `%!event!item%` (the element) and `%!event!data%` (the click: its window, where).
