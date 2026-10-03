Step text: `split %text% into lines`
Properties: `{"Value": "%text%", "Separator": "line"}` — "into lines" is the named separator `line` (the newline), not the default comma.

Step text: `split %csv% by ","`
Properties: `{"Value": "%csv%", "Separator": ","}`
