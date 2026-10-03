Step text: `pack %user%, write to %archived%`
Properties: `{"Value": "%user%"}` — no format named: gzip; the trailing `write to %archived%` is its own action.

Step text: `pack /photos to /backup/photos.tar.gz`
Properties: `{"Value": "/photos", "To": "/backup/photos.tar.gz"}` — the format is the one To's name ends in (tar.gz).

Step text: `pack %report% as brotli, smallest, write to %small%`
Properties: `{"Value": "%report%", "Format": "brotli", "Level": "smallest"}`
