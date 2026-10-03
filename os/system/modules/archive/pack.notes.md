Value — what to pack · say: the value, file or folder, inline · builder: as the step writes it — a path is the path literal (`pack /photos` → `Value="/photos"`), a `%variable%` the variable; NEVER `%!data%` when the step names the thing to pack.
Format — the archive kind (gzip, deflate, brotli, tar, tar.gz, zip, oci.layer) · say: `as <format>` · builder: the one the step names; left out, the one `To`'s name ends in, else gzip.
To — the file/path to pack INTO · say: `to <path>` · builder: the path the step names (`to /backup/photos.tar.gz` → `To="/backup/photos.tar.gz"`); left out, the archive comes back as the answer.
Level — the compression effort (fastest, optimal, smallest, none) · say: `smallest`, `fastest` · builder: the one the step names.
