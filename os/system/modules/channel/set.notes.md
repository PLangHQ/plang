Name — the channel's name: the word right BEFORE "channel" — `output`, `error`, `input`, or a quoted literal. In `set <name> channel as <Goal>` the name is `<name>`, never the goal after `as`.
Goal — the goal that backs the channel: a goal.call action naming the goal AFTER `as` or `call`. Never the current/enclosing goal. In `set output channel as MyGoal`, Name=`output` and Goal=call(`MyGoal`) — the two never swap.
Actor — the actor whose channel it is, only when the step names one (`set system input channel …`).
Buffer — the channel's buffer size, only when the step gives one.
Timeout — how long one message may take, only when the step gives one (a duration, `PT30S`).
Mime, Encoding — the content type and text encoding, only when the step names them.
Direction — `input`, `output` or `bidirectional`, only when the step says; the names input and output decide it themselves.
Encryption, Signing — the variables holding the keys, only when the step names them.
