`goal.call` runs another goal. It is the action behind `call X` wherever that clause appears — on its own, as the body of a condition, as the body of a loop, or inside an error handler. `Name` is the goal; each named argument after it is one row in `Parameter`.

Step text: `call Finalize`
Properties: `{"Name": "Finalize"}`

Step text: `call ProcessOrder id=%orderId%, retries=3`
Properties: `{"Name": "ProcessOrder", "Parameter": [{"name": "id", "value": "%orderId%"}, {"name": "retries", "value": 3}]}`

Step text: `call /system/builder/EmitBuildEvent kind="done"`
Properties: `{"Name": "/system/builder/EmitBuildEvent", "Parameter": [{"name": "kind", "value": "done"}]}` — a path-qualified name is still just the name.

Step text: `call goal Render source=%!a.b.c%`
Properties: `{"Name": "Render", "Parameter": [{"name": "source", "value": "%!a.b.c%"}]}` — the name is the token right after `call goal`; `source=%!a.b.c%` is an argument even though its value is a dotted variable — the variable never becomes the Name.

Step text: `call goal Render module=%!a.b.c%`
Properties: `{"Name": "Render", "Parameter": [{"name": "module", "value": "%!a.b.c%"}]}` — the word before `=` is the argument's name and is kept even when it is a plang word (`module`, `file`, `goal`); never a bare `Parameter` value with no name, never dropped.

Step text: `if %total% > 5, call MarkBig`
Properties: `{"Name": "MarkBig"}` — the condition is its own action; this one is only the call.

Step text: `foreach %items%, call HandleItem item=%item%`
Properties: `{"Name": "HandleItem", "Parameter": [{"name": "item", "value": "%item%"}]}` — the loop is its own action.

Step text: `read 'config.json', on error call HandleReadError`
Properties: `{"Name": "HandleReadError"}` — a goal called inside an error handler is an ordinary call (the read is its own action; the call is in on.error's Recovery).

Step text: `call Turn content=%content%, don't wait`
Properties: `{"Name": "Turn", "Parameter": [{"name": "content", "value": "%content%"}], "Parallel": true}` — "don't wait" / "in the background" / "and go on" is `Parallel=true`: the call runs beside the step and answers a task at once. There is no `Wait` property.

Step text: `call Backup in parallel`
Properties: `{"Name": "Backup", "Parallel": true}` — `in parallel` is the parallel value; `in parallel(cpu: 2)` is `{"cpu": 2}`.

Step text: `call goal Claude in %!app.parent%, message=%text%, write to %reply%`
Properties: `{"Name": "%!app.parent.goal[\"Claude\"]%", "Parameter": [{"name": "message", "value": "%text%"}]}` — a goal of the app that started this one (`%!app.parent%`), picked by name: it runs there, and its result comes back.
