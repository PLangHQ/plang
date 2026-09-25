Step text: `set %x% = 5`
Properties: `{"Name": "%x%", "Value": 5}` — no `Type`; the value is plainly a number.

Step text: `set default %enabled% = true`
Properties: `{"Name": "%enabled%", "Value": true, "AsDefault": true}` — "default" is what `AsDefault` means.

Step text: `set %birthday% = "2026-01-01"`
Properties: `{"Name": "%birthday%", "Value": "2026-01-01", "Type": {"name": "date"}}` — read as a date from its ISO form.

Step text: `set %start% = "1st jan 2026"`
Properties: `{"Name": "%start%", "Value": "2026-01-01", "Type": {"name": "date"}}` — a written date, rewritten in ISO. Never the words "1st jan 2026".

Step text: `set %timeout% = "PT30S"`
Properties: `{"Name": "%timeout%", "Value": "PT30S", "Type": {"name": "duration"}}`

Step text: `set %count% = "42"`
Properties: `{"Name": "%count%", "Value": 42}` — a quantity; `%count%` confirms number over text.

Step text: `set %version% = "2.0"`
Properties: `{"Name": "%version%", "Value": "2.0"}` — a version label stays text though it looks numeric.

Step text: `set %iso%(duration) = "PT5M"`
Properties: `{"Name": "%iso%", "Value": "PT5M", "Type": {"name": "duration"}}` — the `(duration)` token is not part of the name; it forces the type. The name is `%iso%`, never `%iso%(duration)`.

Step text: `set %doc% = "2026-01-01" as text`
Properties: `{"Name": "%doc%", "Value": "2026-01-01", "Type": {"name": "text"}}` — `as` forces text over the date reading.

Step text: `set %img% = "real.gif" as image/gif strict`
Properties: `{"Name": "%img%", "Value": "real.gif", "Type": {"name": "image", "kind": "gif", "strict": true}}`

Step text: `set %n% = "42" as int`
Properties: `{"Name": "%n%", "Value": "42", "Type": {"name": "number", "kind": "int"}}` — `int` is a kind of `number`, never a name of its own.

Step text: `get 'https://example.com/rates.json', write to %rates%`

Step text: `hash %password%, write to %passwordHash%`

Step text: `call GetUser id=%userId%, write to %user%`

Step text: `sort %names%, write to %sortedNames%`
