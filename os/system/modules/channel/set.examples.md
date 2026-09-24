Step text: `set output channel as MyGoal`
Properties: `{"Name": "output", "Goal": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "MyGoal"}]}}`

Step text: `set error channel as ErrorHandler`
Properties: `{"Name": "error", "Goal": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "ErrorHandler"}]}}`

Step text: `set channel "builder" call BuilderChannel`
Properties: `{"Name": "builder", "Goal": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "BuilderChannel"}]}}` — the channel name is the quoted literal; the goal is the one named after `call`.

The channel name (output, error, input, or any custom literal) is always a bare string, never a
%variable%. `Goal` is the `goal.call` action whose goal backs the channel — the same shape as any
other goal.call.
