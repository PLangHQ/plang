Step text: `assert %key% is not "ServiceError"`
Properties: `{"Expected": "ServiceError", "Actual": "%key%"}` — "is not X" is notEquals.

Step text: `assert %name% is not empty, "has a value"`
Properties: `{"Expected": "", "Actual": "%name%", "Message": "has a value"}` — "is not empty" is notEquals against the empty value; there is no assert.isNotEmpty. ("is not null" is assert.isNotNull, a different action.)
