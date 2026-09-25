Step text: `if %count% > 0, call ProcessItems`
Properties: `{"Left": "%count%", "Operator": ">", "Right": 0}` — the call is the body: it goes in the if's `child`, `[{"text": "call ProcessItems", "action": [goal.call]}]`, never beside the if.

Step text: `if %content% is not empty`
Properties: `{"Left": "%content%", "Operator": "isnotempty"}` — the negation is the operator's; `isnotempty` takes no `Right`.

Step text: `if %flag% is true, call Go`
Properties: `{"Left": "%flag%", "Operator": "==", "Right": true}`
