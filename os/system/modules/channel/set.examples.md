Step text: `set output channel as MyGoal`
Properties: `{"Name": "output", "Goal": {"name": "MyGoal"}}`

Step text: `set error channel as ErrorHandler`
Properties: `{"Name": "error", "Goal": {"name": "ErrorHandler"}}`

Step text: `set channel "builder" call BuilderChannel`
Properties: `{"Name": "builder", "Goal": {"name": "BuilderChannel"}}` — the channel name is the quoted literal; the goal is the one named after `call`.

The channel name (output, error, input, or any custom literal) is always a bare string, never a
%variable%. `Goal` is the goal that backs the channel, as the object `{"name": "<GoalName>"}`.
