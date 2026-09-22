Step text: `if %count% > 0, call ProcessItems`
Properties: `{"Left": "%count%", "Operator": ">", "Right": 0}` — the call is its own action.

Step text: `if %content% is not empty`
Properties: `{"Left": "%content%", "Operator": "isempty", "Negate": true}` — `isempty` takes no `Right`; `Negate` inverts it.

Step text: `if %flag% is true, call Go`
Properties: `{"Left": "%flag%", "Operator": "==", "Right": true}`
