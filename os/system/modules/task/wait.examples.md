Step text: `wait for %task%, write to %r%`
Properties: `{"Task": "%task%"}` — waits for the one task and its result is the answer; the trailing `write to %r%` is its own action.

Step text: `wait for %tasks%` (several tasks in a list)
Properties: `{"List": "%tasks%"}` — their results, in order, are the answer.
