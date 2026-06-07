Step text: `set %x% = 5`
Mapping: `variable.set Name([variable] %x%), Value([number] 5)` — no `Type`; the value's own type `{number,int}` rides the Value wrapper.

Step text: `set default %enabled% = true`
Mapping: `variable.set Name([variable] %enabled%), Value([bool] true), AsDefault([bool] true)` — the value is plain.

Step text: `set %birthday% = "2026-01-01"`
Mapping: `variable.set Name([variable] %birthday%), Value([date] 2026-01-01)` — judged a date from its ISO form; no `as` needed.

Step text: `set %start% = "1st jan 2026"`
Mapping: `variable.set Name([variable] %start%), Value([date] 2026-01-01)` — a *written* date, normalized to canonical ISO in the Value (NOT the words "1st jan 2026").

Step text: `set %timeout% = "PT30S"`
Mapping: `variable.set Name([variable] %timeout%), Value([duration] PT30S)` — an ISO-8601 duration.

Step text: `set %count% = "42"`
Mapping: `variable.set Name([variable] %count%), Value([number] 42)` — a quantity; the `%count%` intent confirms number over text.

Step text: `set %version% = "2.0"`
Mapping: `variable.set Name([variable] %version%), Value([text] 2.0)` — a version label stays text (intent: `%version%`), even though it looks numeric.

Step text: `set %iso%(duration) = "PT5M"`
Mapping: `variable.set Name([variable] %iso%), Value([duration] PT5M), Type([type] {"name":"duration"})` — a `(<kind>)` token on the write target is STRIPPED from the Name (PLang ignores `(...)` in a `%var%` name) and sets the `Type` force. Name is `%iso%`, never `%iso%(duration)`.

Step text: `set %doc% = "2026-01-01" as text`
Mapping: `variable.set Name([variable] %doc%), Value([text] 2026-01-01), Type([type] {"name":"text"})` — `as` FORCES text, overriding the date judgement.

Step text: `set %img% = "real.gif" as image/gif strict`
Mapping: `variable.set Name([variable] %img%), Value([text] real.gif), Type([type] {"name":"image","kind":"gif","strict":true})`

Step text: `set %n% = "42" as int`
Mapping: `variable.set Name([variable] %n%), Value([text] 42), Type([type] {"name":"number","kind":"int"})` — `int` is a kind of `number`, never a top-level name.
