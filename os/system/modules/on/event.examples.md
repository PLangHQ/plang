Step text: `before each goal call LogBefore`
Properties: `{"Event": "%!app.type.goal.on.start%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "LogBefore"}]}}`

Step text: `after step call TrackAfterStep`
Properties: `{"Event": "%!app.type.step.on.start%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "TrackAfterStep"}]}}`

Step text: `before action file.read call OverrideFileRead`
Properties: `{"Event": "%!app.module.file.read.on.start%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "OverrideFileRead"}]}}`

Step text: `after action output.write call CaptureWrite`
Properties: `{"Event": "%!app.module.output.write.on.start%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "CaptureWrite"}]}}`

Step text: `after each goal call LogAfter, level="info"`
Properties: `{"Event": "%!app.type.goal.on.start%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "LogAfter"}, {"name": "Parameter", "value": [{"name": "level", "value": "info"}]}]}}`

Step text: `before write on "audit" channel call ApprovalGoal`
Properties: `{"Event": "%!channels.audit.on.write%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "ApprovalGoal"}]}}`

Step text: `on ask on "input" channel call CaptchaGoal, write to %binding%`
Properties: `{"Event": "%!channels.input.on.ask%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "CaptchaGoal"}]}}` — the trailing `write to %binding%` is its own action.
