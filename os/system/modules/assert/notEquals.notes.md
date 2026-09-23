**Omit `Message` unless the step text names a custom error message.** `Expected` is taken from the step text literal — do NOT duplicate it into `Message`, and do NOT write `Message=%!data%`. `Expected` carries the real literal, never `%!data%`.

Non-emptiness is an inequality check against the empty value: `is not empty` / `is present` / `has a value` → `notEquals` with `Expected` empty. There is no `assert.isNotEmpty`.
