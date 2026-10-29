Step text: `set %x% = 5`
Formal: `variable.set(Name: variable = %x%, Value: number = 5)` — no `Type`; the value is plainly a number.

Step text: `set default %enabled% = true`
Formal: `variable.set(Name: variable = %enabled%, Value: bool = true, Default: bool = true)` — "default" is what `Default` means.

Step text: `set %birthday% = "2026-01-01"`
Formal: `variable.set(Name: variable = %birthday%, Value: date = "2026-01-01", Type: type = {"name": "date"})` — read as a date from its ISO form.

Step text: `set %timeout% = "PT30S"`
Formal: `variable.set(Name: variable = %timeout%, Value: duration = "PT30S", Type: type = {"name": "duration"})`

Step text: `set %version% = "2.0"`
Formal: `variable.set(Name: variable = %version%, Value: text = "2.0")` — a version label stays text though it looks numeric.

Step text: `set %iso%(duration) = "PT5M"`
Formal: `variable.set(Name: variable = %iso%, Value: duration = "PT5M", Type: type = {"name": "duration"})` — the `(duration)` token is not part of the name; it forces the type. The name is `%iso%`, never `%iso%(duration)`.

Step text: `set %doc% = "2026-01-01" as text`
Formal: `variable.set(Name: variable = %doc%, Value: date = "2026-01-01", Type: type = {"name": "text"})` — `as` forces text over the date reading.

Step text: `set %img% = "real.gif" as image/gif strict`
Formal: `variable.set(Name: variable = %img%, Value: text = "real.gif", Type: type = {"name": "image", "kind": "gif", "strict": true})`

Step text: `set %n% = "42" as int`
Formal: `variable.set(Name: variable = %n%, Value: text = "42", Type: type = {"name": "number", "kind": "int"})` — `int` is a kind of `number`, never a name of its own.

Step text: `set %files% as list of path = ["a.txt", "b.txt"]`
Properties: `{"Name": "%files%", "Value": ["a.txt", "b.txt"], "Type": "list<path>"}` — `as list of <type>` keeps the element type: Type is the whole `list<path>`, never a bare `list`; each element is born the element type.

Step text: `set %cfg% = '{"port": 8080}', type=json`
Formal: `variable.set(Name: variable = %cfg%, Value: text = "{\"port\": 8080}", Type: type = {"name": "json"})` — `type=json` (or `as json`) keeps the value as text of kind json, written exactly as the step's literal; never parsed into a dict, and `Type` is `json`, never `dict`.

Step text: `convert %json% from json, write to %dict%`
Formal: `variable.set(Name: variable = %dict%, Value: item = %json%, Type: type = {"name": "dict"})` — `from json` parses the json text in `%json%` into a dict; the source `%json%` is the Value (never dropped), and `Type` is the target structure that parses it. The opposite of `as json`, which keeps a value as json text.

Step text: `get 'https://example.com/rates.json', write to %rates%`

Step text: `hash %password%, write to %passwordHash%`

Step text: `call GetUser id=%userId%, write to %user%`

Step text: `sort %names%, write to %sortedNames%`
