Step text: `pack %user%, write to %archived%`
Formal: `archive.pack(Value: item = %user%)` — no format named: gzip; the trailing `write to %archived%` is its own action.

Step text: `pack /photos to /backup/photos.tar.gz`
Formal: `archive.pack(Value: text = "/photos", To: path = "/backup/photos.tar.gz")` — the format is the one To's name ends in (tar.gz).

Step text: `pack %report% as brotli, smallest, write to %small%`
Formal: `archive.pack(Value: item = %report%, Format: choice<archive> = "brotli", Level: choice<compression> = "smallest")`
