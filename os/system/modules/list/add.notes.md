Step text `add <X> to %list%` → `list.add(ListName=%list%, Value=<X>)`. The Value parameter takes whatever the step text supplies, including JSON object/array literals — there is NO need for a separate "construction" action. The literal `{key: value, ...}` or `[a, b, c]` IS the value, emitted directly as `type:"json"`.

**Common wrong shapes to avoid:**

- Don't add a trailing `variable.set` unless the step text has `, write to %x%`. Step text ending with `to %list%` is the target list, not a capture variable.
- Don't fill `Value=%!data%` as a placeholder. Set Value to the actual literal from the step text.
- Don't invent an `AtIndex` parameter when the step text doesn't say "at position N" / "insert at N". Default behavior is append.
- Don't raise `insufficientContext` because the literal is "complex" — a multi-field `{...}` is still just one Value parameter.
