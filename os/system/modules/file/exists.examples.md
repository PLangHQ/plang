Step text: `check if file.txt exists, write to %fileInfo%`
Properties: `{"Path": "file.txt"}`

Step text: `if 'list.json' exists, write out "there"`
Properties: `{"Path": "list.json"}` — "if <file> exists" is file.exists feeding the condition: file.exists(Path="list.json"); condition.if(Left=%!data%) { output.write(Data="there") }. A quoted file name tested on its own is text (always true) — ask file.exists.
