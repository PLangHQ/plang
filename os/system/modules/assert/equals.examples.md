Step text: `assert %message% equals 'hello plang'`
Properties: `{"Expected": "hello plang", "Actual": "%message%"}`

Step text: `assert %count% equals 2, "the first attempt and one retry"`
Properties: `{"Expected": 2, "Actual": "%count%", "Message": "the first attempt and one retry"}` — the quoted text after the comma is the Message.

Step text: `assert %name% is empty`
Properties: `{"Expected": "", "Actual": "%name%"}` — "is empty" is equals against the empty value; there is no assert.isEmpty.
