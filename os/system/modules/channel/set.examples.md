Step text: `set output channel as MyGoal`
Formal: `channel.set(Name="output", Goal=goal.call(Name="MyGoal"))`

Step text: `set error channel as ErrorHandler`
Formal: `channel.set(Name="error", Goal=goal.call(Name="ErrorHandler"))`

Step text: `set channel "builder" call BuilderChannel`
Formal: `channel.set(Name="builder", Goal=goal.call(Name="BuilderChannel"))` — the channel name is the quoted literal; the goal is the one named after `call`.

The channel name (output, error, input, or any custom literal) is always a bare string, never a
%variable%. `Goal` is the `goal.call` action whose goal backs the channel — the same shape as any
other goal.call.
