Step text: `set %x% = 5`
Mapping: `variable.set Name([variable] %x%), Value([number] 5)` — no `Type`; runtime stamps `{name:"number",kind:"int"}` from the literal.

Step text: `set default %enabled% = true`
Mapping: `variable.set Name([variable] %enabled%), Value([bool] true), AsDefault([bool] true)` — no `Type`; the value is plain.

Step text: `set %doc% = "readme.md" as text`
Mapping: `variable.set Name([variable] %doc%), Value([text] readme.md), Type([type] {"name":"text"})`

Step text: `set %img% = "real.gif" as image/gif strict`
Mapping: `variable.set Name([variable] %img%), Value([text] real.gif), Type([type] {"name":"image","kind":"gif","strict":true})`

Step text: `set %n% = "42" as int`
Mapping: `variable.set Name([variable] %n%), Value([text] 42), Type([type] {"name":"number","kind":"int"})` — `int` is a kind of `number`, never a top-level name.
