# Action

An action is what PLang does when it runs a step. Each step you write runs one action — it could be showing text on screen, saving a value, reading a file, calling an API, or hundreds of other things.

You don't choose the action yourself. You write a plain English instruction, and PLang figures out which action it maps to.

## Examples

**Show something on screen**

```plang
- write out "Hello %name%"
```

PLang sees this as: display the text to the user. The `%name%` part is replaced with whatever value `name` holds at that moment.

**Remember a value**

```plang
- set %total% = %total% + %item%
```

PLang sees this as: take the current value of `total`, add `item` to it, and store the result back in `total`.

## How it works

When you build your program, PLang reads each step and decides which action fits. That decision is saved so it doesn't have to happen again at runtime — your program runs the saved action directly.

If PLang picks the wrong action, you can reword the step and rebuild.

---

- [All available actions →](../../../../../os/system/modules/) — the full list of what PLang can do
- [How the builder picks actions →](../../../../../Documentation/v0.2/action-catalog.md)
