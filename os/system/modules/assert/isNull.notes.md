Value — the value that must be null or unset (`assert %x% is null` → Value=%x%).
Message — only when the step names a custom error message (the quoted text after the comma).

- `assert X is null` → isNull. "is empty" is a different check: assert.equals against the empty value, not this (null and empty are not the same).
