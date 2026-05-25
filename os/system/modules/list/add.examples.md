Step text: `add 'apple' to %fruits%`
Mapping: `list.add ListName([variable] %fruits%), Value([string] apple)`

Step text: `add %newItem% to %items%`
Mapping: `list.add ListName([variable] %items%), Value([object] %newItem%)`

Step text: `add {name: "Ada", age: 30} to %users%`
Mapping: `list.add ListName([variable] %users%), Value([json] {"name":"Ada","age":30})`

Step text: `add {goal: %goal.Name%, index: %step.Index%, response: %compileResult%, usage: {model: %compileResult.Model%, promptTokens: %compileResult.PromptTokens%}} to %trace.stepPasses%`
Mapping: `list.add ListName([variable] %trace.stepPasses%), Value([json] {"goal":"%goal.Name%","index":"%step.Index%","response":"%compileResult%","usage":{"model":"%compileResult.Model%","promptTokens":"%compileResult.PromptTokens%"}})`

Step text: `insert 'first' at position 0 in %items%`
Mapping: `list.add ListName([variable] %items%), Value([string] first), AtIndex([int] 0)`
