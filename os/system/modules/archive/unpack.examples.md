Step text: `unpack %archived%, write to %user%`
Formal: `archive.unpack(Value: archive = %archived%)` — an archived value comes back as itself; the trailing `write to %user%` is its own action.

Step text: `unpack /backup/photos.tar.gz into /photos`
Properties: `{"Value": "/backup/photos.tar.gz", "Into": "/photos"}` — the format is the one the file's name ends in.

Step text: `unpack %layer% into %rootfs% as oci.layer, at most 2 GiB`
Formal: `archive.unpack(Value: archive = %layer%, Into: path = %rootfs%, Format: choice<archive> = "oci.layer", Max: size = "2 GiB")`
