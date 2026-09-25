`Template` is the template as the step names it — a file path or the inline template text.

- `Parameter` only when the step passes values to the template (`render x.html, name=%name%`) — one row per value. Never an empty list: a step that passes nothing leaves `Parameter` out.
- `IsFile` is left out: the render finds out itself whether `Template` is a file. Write it only when the step says so — `inline` / `as text` → `false`, `from file` → `true`.
