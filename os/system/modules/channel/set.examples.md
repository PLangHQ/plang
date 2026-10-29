Step text: `set output channel as MyGoal`
Formal: `channel.set(Name: text = "output", Goal: action = goal.call(Name: goal = "MyGoal"))`

Step text: `set error channel as ErrorHandler`
Formal: `channel.set(Name: text = "error", Goal: action = goal.call(Name: goal = "ErrorHandler"))`

Step text: `set channel "builder" call BuilderChannel`
Formal: `channel.set(Name: text = "builder", Goal: action = goal.call(Name: goal = "BuilderChannel"))` — the channel name is the quoted literal; the goal is the one named after `call`.

The channel name (output, error, input, or any custom literal) is always a bare string, never a
%variable%. `Goal` is the `goal.call` action whose goal backs the channel — the same shape as any
other goal.call.
