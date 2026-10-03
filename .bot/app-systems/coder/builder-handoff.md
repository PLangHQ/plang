# Builder handoff — the archive module (decision 559)

## New module: `archive`

Teaching: `os/system/modules/archive/{module.description,pack.description,pack.examples,unpack.description,unpack.examples}.md`.

### `archive.pack`

| Parameter | Type | Notes |
|---|---|---|
| `Value` | data (whole) | what to pack. The value decides: a value packs as its Data whole; a file (path) as its contents and its name; a folder needs a bundle format |
| `Format` | `choice<archive kind>`, optional | gzip, deflate, brotli, tar, tar.gz, zip, oci.layer. Left out: the one `To`'s name ends in, else gzip |
| `To` | path, optional | written as it is packed; the answer is then the path |
| `Level` | `choice<compression>`, optional | fastest, optimal, smallest, none; falls back to `%!archive.setting.level%` |

Answers an `archive` (`archive<gzip>`…) when there's no `To`, else the path.

Natural language that should map here:
- `pack %user%, write to %archived%` → `{"Value": "%user%"}`
- `pack /photos to /backup/photos.tar.gz` → `{"Value": "/photos", "To": "/backup/photos.tar.gz"}`. The value is a **path** here (a folder), not text.
- `pack %report% as brotli, smallest, write to %small%` → `{"Value": "%report%", "Format": "brotli", "Level": "smallest"}`
- `compress %x%` / `zip the folder /a` should land here too (the old `variable.compress` is gone).

### `archive.unpack`

| Parameter | Type | Notes |
|---|---|---|
| `Value` | `archive` | what pack answered, or a file in a format (a path, or a path written as text) |
| `Into` | path, optional | the folder a bundle or an archived file lands in. Left out, an archived Data comes back as the Data |
| `Format` | `choice<archive kind>`, optional | when the archive doesn't say. A container image layer is `oci.layer` |
| `Max` | size, optional | e.g. `2 GiB`; falls back to `%!archive.setting.max%` (100 MiB) |

Natural language:
- `unpack %archived%, write to %user%` → `{"Value": "%archived%"}`
- `unpack /backup/photos.tar.gz into /photos` → `{"Value": "/backup/photos.tar.gz", "Into": "/photos"}`
- `unpack %layer% into %rootfs% as oci.layer, at most 2 GiB` → `{"Value": "%layer%", "Into": "%rootfs%", "Format": "oci.layer", "Max": "2 GiB"}`
- `decompress %x%` / `extract %zip% to /dir` should land here.

## Removed
- `variable.compress` and `variable.decompress` (and their teaching). Step text that says compress/decompress should now pick `archive.pack` / `archive.unpack`.

## To build and check
- `test/serialization/CompressRoundTrip.test.goal` was rewritten to pack/unpack. It needs a build (I can't: the decider's key isn't in my settings). Check the `.pr`:
  - step 2 → `archive.pack` with `Value=%original%`;
  - step 3 → `assert %archived!type.name% equals 'archive'`. If `!type.name` doesn't read the type's name, tell me and I'll fix the step or the runtime;
  - step 4 → `archive.unpack` with `Value=%archived%`.
- `/photos` in `pack /photos to …` may arrive as text. A bundle format reads it as the path it names. With no bundle format (no `Format`, no `To` suffix), text packs as a string value, which is right for `pack "hello"`.
