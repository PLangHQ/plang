Step text: `set %total% = %total% + %item%`
Formal: `math.add(A: item = %total%, B: item = %item%)` — the `+` is math.add; a variable.set after it keeps the sum in `%total%`.

Step text: `add %price% and %tax%, write to %sum%`
Formal: `math.add(A: item = %price%, B: item = %tax%)`
