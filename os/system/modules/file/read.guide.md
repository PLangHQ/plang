### Reading as a template

With `load vars`, the read treats the file's text as a template: each `%variable%` in it is filled with that variable's current value. While the text still holds `%variables%` to fill, the read stays lazy — re-read and re-filled at each use, so it reflects the latest values; text with no variables left to fill is kept once opened.

Infrastructure variables are never filled. A `%!…%` name — engine internals like `%!app%`, `%!trace%`, `%!fileSystem%` — is left exactly as written, because a file's content may be untrusted; only the program's own `%variables%` resolve. To put an `%!…%` value into a string, build the string in `.goal` code rather than reading it from a file.

For example, a `greeting.txt` of:

```text
Hello, %name%!
Trace: %!trace.id%
```

read with `load vars`:

```plang
Start
- set %name% = "World"
- read 'greeting.txt', load vars, write to %greeting%
- write out %greeting%
```

prints:

```text
Hello, World!
Trace: %!trace.id%
```

`%name%` is filled; `%!trace.id%` is left exactly as written.
