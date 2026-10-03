Step text: `split %text% into lines`
Properties: `{"Value": "%text%", "Separator": "\n"}` — "into lines" splits on the newline, not the default comma.

Step text: `split %csv% by ","`
Properties: `{"Value": "%csv%", "Separator": ","}`
