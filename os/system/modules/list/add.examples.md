Step text: `add 'apple' to %fruits%`
Properties: `{"ListName": "%fruits%", "Value": "apple"}`

Step text: `add %newItem% to %items%`
Properties: `{"ListName": "%items%", "Value": "%newItem%"}`

Step text: `add {name: "Ada", age: 30} to %users%`
Properties: `{"ListName": "%users%", "Value": {"name": "Ada", "age": 30}}` — an object value rides as an object, not as text.

Step text: `add {goal: %goal.Name%, index: %step.Index%, response: %compileResult%} to %trace.stepPasses%`
Properties: `{"ListName": "%trace.stepPasses%", "Value": {"goal": "%goal.Name%", "index": "%step.Index%", "response": "%compileResult%"}}` — variables inside an object keep their % signs.

Step text: `insert 'first' at position 0 in %items%`
Properties: `{"ListName": "%items%", "Value": "first", "AtIndex": 0}`
