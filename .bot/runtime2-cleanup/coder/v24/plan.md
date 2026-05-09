# v24 — Stage 24: Callback wire options eviction

Per architect's stage-24 spec.

1. Create `PLang/App/Callback/Wire/this.cs` with internal `Options` property (JsonSerializerOptions, camelCase + case-insensitive + WhenWritingNull + Filters.Sensitive.Strip).
2. Mount on `Callback.@this` as `Wire { get; } = new()` alongside existing `Signature`.
3. AskCallback.cs: drop `_options` static; Serialize/Deserialize read `ctx.App.Callback.Wire.Options`.
4. ErrorCallback.cs: drop `_options` static; thread `JsonSerializerOptions` through `SerializeSnapshot` / `DeserializeSnapshot` private static helpers as a parameter.
5. Sweep unused usings.
6. Build clean, both test suites green.
