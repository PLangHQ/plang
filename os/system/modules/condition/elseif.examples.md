Step text: `else if %a% > 5, write 'mid'`
Properties: `{"Left": "%a%", "Operator": ">", "Right": 5}` — the write is the body: it goes in the elseif's `child`, `[{"text": "write 'mid'", "action": [output.write]}]`.
