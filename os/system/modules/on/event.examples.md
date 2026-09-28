Step text: `before each goal call LogBefore`
Properties: `{"Item": "%!app.type.goal%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "LogBefore"}]}}` — a goal, step or action's event is `start`, the default.

Step text: `after step call TrackAfterStep`
Properties: `{"Item": "%!app.type.step%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "TrackAfterStep"}]}}`

Step text: `before action file.read call OverrideFileRead`
Properties: `{"Item": "%!app.module.file.read%", "When": "before", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "OverrideFileRead"}]}}`

Step text: `after each goal call LogAfter, level="info"`
Properties: `{"Item": "%!app.type.goal%", "When": "after", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "LogAfter"}, {"name": "Parameter", "value": [{"name": "level", "value": "info"}]}]}}`

Step text: `before write on "audit" channel call ApprovalGoal`
Properties: `{"Item": "%!channels.audit%", "When": "before", "Event": "write", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "ApprovalGoal"}]}}`

Step text: `on ask on "input" channel call CaptchaGoal, write to %binding%`
Properties: `{"Item": "%!channels.input%", "When": "after", "Event": "ask", "Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "CaptchaGoal"}]}}` — the trailing `write to %binding%` is its own action.
