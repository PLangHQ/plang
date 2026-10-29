Step text: `list files in docs/ recursive, write to %files%`
Formal: `file.list(Path="docs/", Recursive=true)`

Step text: `list files in %folder%`
Formal: `file.list(Path=%folder%)` — `Recursive` is absent because the step does not ask for it.
