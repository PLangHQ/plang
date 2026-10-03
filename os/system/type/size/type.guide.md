# Sizes

A `size` is a count of bytes that writes itself with a unit. Its value is the exact number of bytes; the text rounds for reading.

## Two standards

A size's kind is the standard it is written in:

- `iec` — powers of 1024: `500 KiB`, `95.4 MiB`, `1 GiB`.
- `si` — powers of 1000: `512 kB`, `100 MB`, `1 GB`.

A size read from text keeps the standard its suffix names — `95.4 MiB` is IEC, `100 MB` is SI. A size made from a bare count — a file's length, a download's bytes — is written in the standard the setting names: `%!app.type.size.setting.standard%` (`iec` or `si`, `iec` by default).

## Comparing and testing

A size compares and sorts by its exact bytes, whatever unit each was written in, so `100 MB` and `95.4 MiB` order correctly against each other. A size of zero bytes is falsy, so `if %size%` asks "is there anything".
