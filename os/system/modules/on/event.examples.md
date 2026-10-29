Step text: `before the app starts, call Say`
Formal: `on.event(Event=%!app.on.start%, When="before", Action=goal.call(Name="Say"))` — the app itself is `%!app%`; its start is `%!app.on.start%`.

Step text: `after %count% is set, call Log`
Formal: `on.event(Event=%!app.variable["count"].on.set%, When="after", Action=goal.call(Name="Log"))` — a variable the step names is `%!app.variable["count"]%`: its own moments, not its value's (`%count.on…%`).

Step text: `before each goal call LogBefore`
Formal: `on.event(Event=%!app.type.goal.on.start%, When="before", Action=goal.call(Name="LogBefore"))`

Step text: `before each goal, call Audit, not in sub apps`
Formal: `on.event(Event=%!app.type.goal.on.start%, When="before", Sub="none", Action=goal.call(Name="Audit"))` — the event stays in this app; its sub apps don't take it in.

Step text: `before goal Process, call Prepare`
Formal: `on.event(Event=%!app.goal["Process"].on.start%, When="before", Action=goal.call(Name="Prepare"))` — a goal the step names is `%!app.goal["Name"]%` (its bare name), not `%!app.type.goal%`, which is every goal.

Step text: `after action llm.query, call ShowCost`
Formal: `on.event(Event=%!app.module.llm.query.on.start%, When="after", Action=goal.call(Name="ShowCost"))` — an action the step names as module.action is `%!app.module.<module>.<action>%`, not `%!app.type.action%`, which is every action.

Step text: `after step call TrackAfterStep`
Formal: `on.event(Event=%!app.type.step.on.start%, When="after", Action=goal.call(Name="TrackAfterStep"))`

Step text: `before each file action, call Count`
Formal: `on.event(Event=%!app.module.file.on.start%, When="before", Action=goal.call(Name="Count"))` — each action of a module the step names is that module's start, `%!app.module.<module>.on.start%`; not each step.

Step text: `after each http action, call Meter`
Formal: `on.event(Event=%!app.module.http.on.start%, When="after", Action=goal.call(Name="Meter"))`

Step text: `before action file.read call OverrideFileRead`
Formal: `on.event(Event=%!app.module.file.read.on.start%, When="before", Action=goal.call(Name="OverrideFileRead"))`

Step text: `after action output.write call CaptureWrite`
Formal: `on.event(Event=%!app.module.output.write.on.start%, When="after", Action=goal.call(Name="CaptureWrite"))`

Step text: `after each goal call LogAfter, level="info"`
Formal: `on.event(Event=%!app.type.goal.on.start%, When="after", Action=goal.call(Name="LogAfter", Parameter=[{level: "info"}]))`

Step text: `before write on "audit" channel call ApprovalGoal`
Formal: `on.event(Event=%!channel.audit.on.write%, When="before", Action=goal.call(Name="ApprovalGoal"))`

Step text: `on ask on "input" channel call CaptchaGoal, write to %event%`
Formal: `on.event(Event=%!channel.input.on.ask%, When="before", Action=goal.call(Name="CaptchaGoal"))` — the trailing `write to %event%` is its own action.

Step text: `on click on #window.bot, call ShowClaude`
Formal: `on.event(Event=%!screen.element["#window.bot"].on.click%, When="after", Action=goal.call(Name="ShowClaude"))` — `#…` in a step is an element of the screen in play, `%!screen.element["#…"]%`: a window's own part (`#window.bot` is its ☰, `#window.save` its save tool) or a page's id. The call reads `%!event!item%` (the element) and `%!event!data%` (the click: its window, where).
