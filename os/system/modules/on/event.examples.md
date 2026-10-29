Step text: `before the app starts, call Say`
Properties: `{"Event": "%!app.on.start%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Say"}]}}` — the app itself is `%!app%`; its start is `%!app.on.start%`.

Step text: `before each goal call LogBefore`
Properties: `{"Event": "%!app.type.goal.on.start%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "LogBefore"}]}}`

Step text: `before goal Process, call Prepare`
Properties: `{"Event": "%!app.goal[\"Process\"].on.start%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Prepare"}]}}` — a goal the step names is `%!app.goal["Name"]%` (its bare name), not `%!app.type.goal%`, which is every goal.

Step text: `after action llm.query, call ShowCost`
Properties: `{"Event": "%!app.module.llm.query.on.start%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "ShowCost"}]}}` — an action the step names as module.action is `%!app.module.<module>.<action>%`, not `%!app.type.action%`, which is every action.

Step text: `after step call TrackAfterStep`
Properties: `{"Event": "%!app.type.step.on.start%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "TrackAfterStep"}]}}`

Step text: `before action file.read call OverrideFileRead`
Properties: `{"Event": "%!app.module.file.read.on.start%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "OverrideFileRead"}]}}`

Step text: `after action output.write call CaptureWrite`
Properties: `{"Event": "%!app.module.output.write.on.start%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "CaptureWrite"}]}}`

Step text: `after each goal call LogAfter, level="info"`
Properties: `{"Event": "%!app.type.goal.on.start%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "LogAfter"}, {"name": "Parameter", "value": [{"name": "level", "value": "info"}]}]}}`

Step text: `before write on "audit" channel call ApprovalGoal`
Properties: `{"Event": "%!channel.audit.on.write%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "ApprovalGoal"}]}}`

Step text: `on ask on "input" channel call CaptchaGoal, write to %event%`
Properties: `{"Event": "%!channel.input.on.ask%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "CaptchaGoal"}]}}` — the trailing `write to %event%` is its own action.

Step text: `on click on #window.bot, call ShowClaude`
Properties: `{"Event": "%!screen.element[\"#window.bot\"].on.click%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "ShowClaude"}]}}` — `#…` in a step is an element of the screen in play, `%!screen.element["#…"]%`: a window's own part (`#window.bot` is its ☰, `#window.save` its save tool) or a page's id. The call reads `%!event!item%` (the element) and `%!event!data%` (the click: its window, where).
