Name — the variable to set, with its % signs. A `(<kind>)` written after it is not part of the name: it is the Type. · ask: which variable does the step keep its result in?
Value — the value as the step writes it, typed as what it is (a quoted date is a date, "42" a number, a label stays text). After another action in the same step it is %!data%.
Type — only when the step forces a type: `as <type>` or `(<kind>)`. A step that says `type=json` / `as json` forces `Type=json`: the value stays the literal text of kind json, written as the step wrote it, never parsed into a `dict`. · ask: which type does the step coerce the value to — none unless the step writes `as <type>`, `type=<type>` or `(<kind>)` (a value that merely looks like a dict/list/json is NOT a coercion; leave the type none)?
Default — true when the step says "set default", "default to" or "only if unset"; the variable keeps a value it already has · ask: does the step set the variable only when it is unset ("set default", "default to", "only if unset")?

- `set %x% = …` is one variable.set.
- `<action>, write to %x%` is that action followed by variable.set(Name=%x%, Value=%!data%) — its own action, never a modifier, and without a Type.
- `set %x% as list of <type> = [...]` keeps the element type: Type is `list<type>` (e.g. `as list of path` → Type `list<path>`), never a bare `list` — the element is born the element type at its first read.
