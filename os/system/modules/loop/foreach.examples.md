`loop.foreach` wraps a **body action** — whatever the remaining clause after the collection does. It contributes only `Collection`; the body is a peer action (and may itself be compound). Compile the body by **its own action's examples**, unchanged — `loop.foreach` does not alter it.

Shape: `foreach %collection%, <body>` → `loop.foreach Collection([object] %collection%) | <body action(s) compiled per their own examples>`

The body can be any action — `goal.call`, `output.write`, `file.save`, `db.*`, etc. Look up the leading verb of `<body>` and compile it normally; only `Collection` belongs to `loop.foreach`.

Example — body is a goal call:
Step text: `foreach %items%, call ProcessItem item=%item%`
Mapping: `loop.foreach Collection([object] %items%) | goal.call GoalName([goal.call] ProcessItem)`
(`item=%item%` is a `goal.call` argument — it lives inside `goal.call`'s `GoalName.parameters`, not on `loop.foreach`. See `goal.call`.)
