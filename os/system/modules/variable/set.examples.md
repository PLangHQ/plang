Step text: `set %doc% = "readme.md" as text`
Mapping: `variable.set Name([variable] %doc%), Value([text] readme.md), Type([type] {"name":"text"})`

Step text: `set %x% = "a" as text/markdown`
Mapping: `variable.set Name([variable] %x%), Value([text] a), Type([type] {"name":"text","kind":"markdown"})`

Step text: `set %img% = "real.gif" as image/gif strict`
Mapping: `variable.set Name([variable] %img%), Value([text] real.gif), Type([type] {"name":"image","kind":"gif","strict":true})`

Step text: `set %n% = "42" as int`
Mapping: `variable.set Name([variable] %n%), Value([text] 42), Type([type] {"name":"number","kind":"int"})` — `int` is a kind of `number`, never a top-level name.

Step text: `output.ask "format?", write to %fmt%(json)`
Mapping: `output.ask Question([text] format?)` peer `variable.set Name([variable] %fmt%), Value([object] %!data%), Type([type] {"name":"text","kind":"json"})` — `(json)` is a kind of `text`.

Step text: `set %x% = 5`
Mapping: `variable.set Name([variable] %x%), Value([number] 5)` — no `Type` parameter; runtime stamps `{name:"number", kind:"int"}` from the literal.
