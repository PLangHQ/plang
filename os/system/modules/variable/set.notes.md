Name — the variable to set, with its % signs. A `(<kind>)` written after it is not part of the name: it is the Type.
Value — the value as the step writes it, typed as what it is (a quoted date is a date, "42" a number, a label stays text). After another action in the same step it is %!data%.
Type — only when the step forces a type: `as <type>` or `(<kind>)`. · ask: which type does the step coerce the value to (`as <type>`)?
Default — true when the step says "set default", "default to" or "only if unset"; the variable keeps a value it already has · ask: does the step set the variable only when it is unset ("set default", "default to", "only if unset")?

- `set %x% = …` is one variable.set.
- `<action>, write to %x%` is that action followed by variable.set(Name=%x%, Value=%!data%) — its own action, never a modifier, and without a Type.
