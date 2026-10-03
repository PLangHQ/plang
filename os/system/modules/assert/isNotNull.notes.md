Value — the value that must not be null (`assert %x% is not null` → Value=%x%).
Message — only when the step names a custom error message (the quoted text after the comma).

- `assert X is not null` → isNotNull. The other two "is not …" forms are different actions: "is not empty" → assert.notEquals against the empty value; "is not <literal>" (`is not "ServiceError"`) → assert.notEquals.
