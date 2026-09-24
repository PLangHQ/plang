`loop.foreach` has three properties: `Collection` (required), and `Item` / `Key` (optional variables). Each element is bound to `%item%` unless the step names another variable (`as %product%` → `Item`). The key — a dict key or a list index — is bound only when the step names one (`with key %sku%` → `Key`). Leave `Item` and `Key` out when the step names neither.

Step text: `foreach %sections%, call ParseSection`

```json
{"module":"loop","name":"foreach","property":[{"name":"Collection","value":"%sections%"}]}
```

Inside the called goal (`ParseSection`), `%item%` is the current element. `foreach %sections%, call ParseSection section=%item%` passes it under another name — `section=%item%` is an argument of the `goal.call`, not a `loop.foreach` property.

`loop.foreach` is a **peer** of its body action — `goal.call` (or whatever runs per iteration) sits as a separate top-level entry in the step's `action` list. Never nest the body inside `loop.foreach`'s `modifier` list; the runtime rejects that with "goal.call is not a modifier".
