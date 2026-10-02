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

Step text: `verify %data% with contracts ['C1'], on error call HandleContractError`
Properties: `{"Name": "HandleContractError"}` — a goal called inside an error handler is an ordinary call.

Step text: `call Turn content=%content%, don't wait`
Properties: `{"Name": "Turn", "Parameter": [{"name": "content", "value": "%content%"}], "Wait": false}` — "don't wait", "in the background", "and go on": the goal starts on its own and the step goes on at once; there is no result to write to.
