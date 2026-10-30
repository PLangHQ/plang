Step text: `add 'apple' to %fruits%`
Formal: `list.add(ListName=%fruits%, Value="apple")`

Step text: `add %newItem% to %items%`
Formal: `list.add(ListName=%items%, Value=%newItem%)`

Step text: `add {name: "Ada", age: 30} to %users%`
Formal: `list.add(ListName=%users%, Value={"name": "Ada", "age": 30})` — an object value rides as an object, not as text.

Step text: `add {goal: %goal.Name%, index: %step.Index%, response: %compileResult%} to %trace.stepPasses%`
Formal: `list.add(ListName=%trace.stepPasses%, Value={"goal": "%goal.Name%", "index": "%step.Index%", "response": "%compileResult%"})` — variables inside an object keep their % signs.

Step text: `insert 'first' at position 0 in %items%`
Formal: `list.add(ListName=%items%, Value="first", At=0)`
