Step text: `split %text% into lines`
Formal: `list.split(Value: text = "%text%", Separator: text<separator> = "line")` — "into lines" is the named separator `line` (the newline), not the default comma.

Step text: `split %csv% by ","`
Formal: `list.split(Value: text = "%csv%", Separator: text<separator> = ",")`
