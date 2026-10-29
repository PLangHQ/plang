Step text: `archive identity 'alice'`
Formal: `identity.archive(Name: text = "alice")`

Step text: `archive identity 'alice', force`
Formal: `identity.archive(Name: text = "alice", Force: bool = true)`

Step text: `archive identity 'alice', force, on error ignore`
Formal: `identity.archive(Name: text = "alice", Force: bool = true)` — identity.archive keeps its Name even when a clause (`on error ignore` → a separate on.error) follows; the trailing clause never swallows the name the step gives.
