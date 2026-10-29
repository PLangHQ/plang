Step text: `split %text% into lines`
Formal: `list.split(Value="%text%", Separator="line")` — "into lines" is the named separator `line` (the newline), not the default comma.

Step text: `split %csv% by ","`
Formal: `list.split(Value="%csv%", Separator=",")`
