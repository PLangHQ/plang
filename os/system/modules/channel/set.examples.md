Step text: `set output channel as MyGoal`
Mapping: `channel.set Name([string] "output"), Goal([goal.call] MyGoal)`

Step text: `set error channel as ErrorHandler`
Mapping: `channel.set Name([string] "error"), Goal([goal.call] ErrorHandler)`

Step text: `set input channel as PromptUser`
Mapping: `channel.set Name([string] "input"), Goal([goal.call] PromptUser)`

The channel name (output, error, input, or any custom literal) is always a
bare string — never a %variable% reference. The Goal parameter is a
goal.call to the goal that backs the channel.
