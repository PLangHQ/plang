Step text: `archive identity 'alice'`
Properties: `{"Name": "alice"}`

Step text: `archive identity 'alice', force`
Properties: `{"Name": "alice", "Force": true}`

Step text: `archive identity 'alice', force, on error ignore`
Properties: `{"Name": "alice", "Force": true}` — identity.archive keeps its Name even when a clause (`on error ignore` → a separate on.error) follows; the trailing clause never swallows the name the step gives.
