`call X, param=value` (standalone or inside `foreach`) → the named params are the CALLEE's params: they go **inside `GoalName.value.parameters`**, NOT as top-level `goal.call` params.

Top-level `goal.call` has only:
- `GoalName` (required) — `{"name":"X", "parameters":[...]}`; `parameters` carries the args to X.
- `Actor` (optional, almost always omitted) — explicit cross-actor delegation only.

**`Actor`: omit unless the step text NAMES an actor** (`call X on actor "logger"`, `call X as %userActor%`, `actor=%audit%`). Otherwise the slot does not exist — never invent it: no `null`/`"system"`/`%!actor%`/`%goal%`, and not because the call is in `/system/...`, a sub-goal, a recovery, or a foreach (none of those name an actor).

`foreach %list%, call X, section=%item%` → set is `loop.foreach` + `goal.call`; `section=%item%` goes inside the payload:
`{"name":"GoalName","value":{"name":"X","parameters":[{"name":"section","value":"%item%","type":"string"}]},"type":"goal.call"}`

**Payload `name` = the goal identifier VERBATIM from the step text** — copy the path exactly (`Goal`, `Folder/Goal`, `../X`, `/root/Y`); dropping/rewriting a segment → runtime NotFound. Never put a type token in the value slot: `"goal.call"` is the parameter's TYPE descriptor, not the name; the name is the user's goal identifier. Any dotted identifier here is wrong.
