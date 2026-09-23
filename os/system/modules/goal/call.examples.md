`goal.call` runs another goal. It is the action behind `call X` wherever that clause appears — on its own, as the body of a condition, as the body of a loop, or inside an error handler. `Name` is the goal; each named argument after it is one row in `Parameter`.

Step text: `call Finalize`
Properties: `{"Name": "Finalize"}`

Step text: `call ProcessOrder id=%orderId%, retries=3`
Properties: `{"Name": "ProcessOrder", "Parameter": [{"name": "id", "value": "%orderId%"}, {"name": "retries", "value": 3}]}`

Step text: `call /system/builder/EmitBuildEvent kind="done"`
Properties: `{"Name": "/system/builder/EmitBuildEvent", "Parameter": [{"name": "kind", "value": "done"}]}` — a path-qualified name is still just the name.

Step text: `if %total% > 5, call MarkBig`
Properties: `{"Name": "MarkBig"}` — the condition is its own action; this one is only the call.

Step text: `foreach %items%, call HandleItem item=%item%`
Properties: `{"Name": "HandleItem", "Parameter": [{"name": "item", "value": "%item%"}]}` — the loop is its own action.

Step text: `verify %data% with contracts ['C1'], on error call HandleContractError`
Properties: `{"Name": "HandleContractError"}` — a goal called inside an error handler is an ordinary call.
