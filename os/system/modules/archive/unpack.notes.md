Value — the archive to unpack: what pack answered, or a file in a format · say: the archive or path, inline · builder: as the step writes it — a path is the path literal (`unpack /backup/photos.tar.gz` → `Value="/backup/photos.tar.gz"`), a `%variable%` the variable; NEVER `%!data%`.
Into — the folder a bundle or archived file unpacks into · say: `into <folder>` · builder: the path the step names (`into /photos` → `Into="/photos"`); left out, an archived Data comes back as the Data.
Format — the format, when the archive doesn't say it (a container image layer is `oci.layer`) · say: `as <format>`.
Max — the most it may unpack to · say: `at most <size>` (`at most 2 GiB` → `Max="2 GiB"`).
