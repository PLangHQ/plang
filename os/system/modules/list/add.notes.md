ListName — the list variable to add to, with its % signs: in `add X to %list%` it is %list%.
Value — what to add, as the step writes it; a `{…}` or `[…]` literal is one value, typed dict or list.
AtIndex — the position to insert at, only when the step names one ("at position 2"); otherwise it appends.

- `add X to %list%` has no variable.set: %list% is the target, not a result to keep.
