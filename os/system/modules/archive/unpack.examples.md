Step text: `unpack %archived%, write to %user%`
Formal: `archive.unpack(Value=%archived%)` — an archived value comes back as itself; the trailing `write to %user%` is its own action.

Step text: `unpack /backup/photos.tar.gz into /photos`
Properties: `{"Value": "/backup/photos.tar.gz", "Into": "/photos"}` — the format is the one the file's name ends in.

Step text: `unpack %layer% into %rootfs% as oci.layer, at most 2 GiB`
Formal: `archive.unpack(Value=%layer%, Into=%rootfs%, Format="oci.layer", Max="2 GiB")`
