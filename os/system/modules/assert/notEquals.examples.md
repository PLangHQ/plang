Step text: `assert %key% is not "ServiceError"`
Formal: `assert.notEquals(Expected: text = "ServiceError", Actual: item = %key%)` — "is not X" is notEquals.

Step text: `assert %name% is not empty, "has a value"`
Formal: `assert.notEquals(Expected: text = "", Actual: item = %name%, Message: text = "has a value")` — "is not empty" is notEquals against the empty value; there is no assert.isNotEmpty. ("is not null" is assert.isNotNull, a different action.)
