Step text: `assert %message% equals 'hello plang'`
Formal: `assert.equals(Expected="hello plang", Actual=%message%)`

Step text: `assert %count% equals 2, "the first attempt and one retry"`
Formal: `assert.equals(Expected=2, Actual=%count%, Message="the first attempt and one retry")` — the quoted text after the comma is the Message.

Step text: `assert %name% is empty`
Formal: `assert.equals(Expected="", Actual=%name%)` — "is empty" is equals against the empty value; there is no assert.isEmpty.
