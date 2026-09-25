Template — the template as the step names it: a file path or the template text itself.
Parameter — the values the step passes to the template (`render x.html, name=%name%`), one row {name, type, value} each. Left out when the step passes none.
IsFile — only when the step says so: `inline` / `as text` → false, `from file` → true. Otherwise left out; the render finds out itself.
