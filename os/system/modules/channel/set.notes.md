Name — the channel's name: the quoted text, or `output`, `error`, `input`.
Goal — the goal that backs the channel: a goal.call action, the goal named after `call` or `as`.
Actor — the actor whose channel it is, only when the step names one (`set system input channel …`).
Buffer — the channel's buffer size, only when the step gives one.
Timeout — how long one message may take, only when the step gives one (a duration, `PT30S`).
Mime, Encoding — the content type and text encoding, only when the step names them.
Direction — `input`, `output` or `bidirectional`, only when the step says; the names input and output decide it themselves.
Encryption, Signing — the variables holding the keys, only when the step names them.
