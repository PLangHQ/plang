`loop.foreach` has ONE parameter: `Collection`. The runtime binds the current element to the well-known transient `%item%` (like `%!data%`). There is no `ItemName`, no `KeyName`, no rename — they don't exist.

Step text: `foreach %sections%, call ParseSection`

```json
{"module":"loop","action":"foreach","parameters":[{"name":"Collection","value":"%sections%","type":"object"}]}
```

Inside the called goal (`ParseSection`), `%item%` is the current element. If the called goal needs the value under a different name, pass it explicitly: `foreach %sections%, call ParseSection section=%item%` — that's a normal `goal.call` parameter (see `goal.call.notes` for where `section=%item%` lives in the JSON).

`loop.foreach` is a **peer** of its body action — `goal.call` (or whatever runs per iteration) sits as a separate top-level entry in `actions`. Never nest the body inside `loop.foreach.modifiers`; the runtime rejects that with "goal.call is not a modifier".
