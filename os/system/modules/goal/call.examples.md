`goal.call` runs another goal. It is the action behind `call X` wherever that clause appears — on its own, as the body of a condition, as the body of a loop, or inside an error handler. Named arguments after the goal name belong to the CALLEE and ride inside `GoalName.parameter`, never as properties of `goal.call` itself.

Step text: `call Finalize`
Properties: `{"GoalName": {"name": "Finalize"}}`

Step text: `call ProcessOrder id=%orderId%, retries=3`
Properties: `{"GoalName": {"name": "ProcessOrder", "parameter": [{"name": "id", "value": "%orderId%"}, {"name": "retries", "value": 3}]}}`

Step text: `call /system/builder/EmitBuildEvent kind="done"`
Properties: `{"GoalName": {"name": "/system/builder/EmitBuildEvent", "parameter": [{"name": "kind", "value": "done"}]}}` — a path-qualified name is still just the name.

Step text: `if %total% > 5, call MarkBig`
Properties: `{"GoalName": {"name": "MarkBig"}}` — the condition is its own action; this one is only the call.

Step text: `foreach %items%, call HandleItem item=%item%`
Properties: `{"GoalName": {"name": "HandleItem", "parameter": [{"name": "item", "value": "%item%"}]}}` — the loop is its own action.

Step text: `verify %data% with contracts ['C1'], on error call HandleContractError`
Properties: `{"GoalName": {"name": "HandleContractError"}}` — a goal called inside an error handler is an ordinary call.
