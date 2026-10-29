Step text: `else if %a% > 5, write 'mid'`
Formal: `condition.elseif(Left: item = %a%, Operator: choice<operator> = ">", Right: number = 5)` — the write is the body: it goes in the elseif's `child`, `[{"text": "write 'mid'", "action": [output.write]}]`.
