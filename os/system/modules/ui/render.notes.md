Template — the template as the step names it: a file path or the template text itself.
Parameter — the arguments: every `name=value` after the template is one argument, the same as goal.call's (`render x.html, name=%name%, n=5` → name and n). Left out when the step passes none.
IsFile — only when the step says so: `inline` / `as text` → false, `from file` → true. Otherwise left out; the render finds out itself.
