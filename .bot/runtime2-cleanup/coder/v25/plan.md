# v25 — Stage 25: Default.cs static eviction

Per architect's stage-25 spec.

1. Delete `_jsonOptions` alias (degenerate ref to `App.Utils.Json.CaseInsensitiveRead`); inline 2 read sites to long form, matching the third already-long-form site.
2. Convert `_transportInOptions` from `private static readonly` to `private readonly` instance field.
3. **Deviation from brief:** brief assumed the 3 read sites were inside instance methods so `_transportInOptions` would just become `this._transportInOptions` syntactically unchanged. They're actually inside `private static async` helpers (`ParsePlangResponseAsync`, `TryExtractSignedErrorIdentity`, `StreamPlangAsync`). Cleanest fix is to convert those 3 helpers + their 2 callers (`ParseResponseAsync`, `HandleStreamingAsync`) to instance methods — they're called from instance lambdas, so call sites need no change. Threading the options as a parameter (the brief's other suggestion for similar pattern in stage 24) would have meant 5 method signatures + 5 call sites updated.
4. Build clean, both test suites green.
