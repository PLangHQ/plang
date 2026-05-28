# test-designer — data-normalize

**Version:** v1

## What this is

`data-normalize` reshapes how `Data.Value` becomes bytes on the wire so PLang can carry arbitrary objects to non-reflection formats (protobuf, MsgPack, CBOR) without each format needing to introspect random C# types. Today JSON gets a free pass via STJ reflection; non-reflection formats hit a wall.

The architect's solution lands in three stages:

1. **Stage 1** — `[Out]` becomes a positive wire whitelist (was a "force JsonIgnore'd back on" flag). 13 in-scope domain types get `[Out]` per `wire-out-attributes.md`. New `[Masked]` attribute (canonical use: `setting.value`). `Data.RawSignature` deleted; 7 caller sites migrate to `Signature`.
2. **Stage 2** — New `Data.Normalize()` walks `data.Value` into a uniform tree of `primitive | byte[] | Data | List<>`. New `IWriter` protocol; `JsonWriter` as first impl. `WireJsonConverter` wraps Normalize → IWriter. `path.JsonConverter.Write` removed. Debug-mode bypass.
3. **Stage 3** — `As<T>` rewritten as a tree-walker (not STJ delegate). Per-type reconstruction hook (`path.Resolve` is the canonical case). Property-lookup cache.

This v1 produces the failing-test contract that pins the architecture.

## What was done

Wrote **15 C# test files** + **5 PLang `.goal` files**. ~120 test stubs total; every body is `Assert.Fail("Not implemented")` / `- throw "not implemented"`.

**C# under `PLang.Tests/App/`:**

Serialization layer (`Serialization/`):
- `OutAttributeInventoryTests.cs` — Stage 1 per-type `[Out]` placement (13 types)
- `RawSignatureDeletionTests.cs` — Stage 1 RawSignature deletion + 7 caller migrations
- `MaskedAttributeTests.cs` — Stage 1 new `[Masked]` attribute + `setting.value` tagging
- `IWriterContractTests.cs` — Stage 2 IWriter interface surface + JsonWriter byte output
- `JsonWriterDomainShapeTests.cs` — Stage 2 wire shape per domain type (path, Identity, setting…)
- `DebugModeBypassTests.cs` — Stage 2 `View.Out` vs `View.Debug` filter behavior
- `FailureMatrixNormalizeTests.cs` — typed-error residue
- `IntegrationCuts/Cut1_JsonRoundTripTests.cs` — full round-trip parity
- `IntegrationCuts/Cut2_DebugModeTests.cs` — Out vs Debug compare
- `IntegrationCuts/Cut3_SignWireVerifyTests.cs` — sign → wire → verify post-RawSig-deletion

Data internals (`DataTests/`):
- `NormalizeTreeShapeTests.cs` — Stage 2 Normalize per-input tree shape
- `NormalizeFilterTests.cs` — Stage 2 `[Out]`/`[Sensitive]`/`[Masked]` filtering
- `NormalizeCycleAndDepthTests.cs` — Stage 2 cycle + depth + getter-throws
- `AsTreeWalkerTests.cs` — Stage 3 `As<T>` reconstruction
- `AsReconstructionHookTests.cs` — Stage 3 per-type hook (path.Resolve)

**PLang goals under `Tests/`:**
- `Tests/Serialization/PathRoundTripAfterNormalize.test.goal`
- `Tests/Serialization/SensitivePropertyDoesNotLeak.test.goal`
- `Tests/Serialization/MaskedSettingOnWire.test.goal`
- `Tests/Serialization/DebugModePayloadIncludesNonOut.test.goal`
- `Tests/Signing/RoundTripAfterRawSignatureDeletion.test.goal`

## Decisions diverging from the architect's matrix

The matrix is suggestion; the test surface is mine to own (architect note confirms this). Specific moves:

- **Grouped by concept, not stage.** Future reader hunting "where is cycle detection tested" shouldn't have to know it's Stage 2 row 13. File names follow the surface (`NormalizeCycleAndDepthTests`, `AsTreeWalkerTests`).
- **Pruned redundant rows.** "Normalize on `List<int>` keeps homogeneous list" and "Normalize on `Dict<string,X>` produces `List<Data>`" collapse into separate tests in one tree-shape file; "the wire output for path is property-bag" sits next to "for Identity" next to "for setting".
- **Failure matrix split across topical files.** Cycle/depth/getter-throws live in `NormalizeCycleAndDepthTests` next to the happy-path Normalize tests; scheme-mismatch/missing-required/type-mismatch live in `AsTreeWalkerTests`; the residue (`Sensitive`-on-`Out` mutex, malformed wire bytes, unregistered MIME) lives in `FailureMatrixNormalizeTests`.
- **Reflection over compile-fail.** `Data.RawSignature` deletion is a compile-time guarantee; expressed as `typeof(Data).GetProperty("RawSignature")` returning null. Mirrors the `data-serialize-cleanup` precedent.
- **One goal per file.** Memory rule (`MEMORY.md` small-rules): multi-goal `.goal` files get overwritten by the builder.
- **Used existing `PLang.Tests/App/DataTests/`** namespace (not `Data/`) — the folder already exists and uses `*Tests` suffix to dodge the `Data`/`Variables` alias clash from `GlobalUsings.cs`.
- **Dropped the "existing tests pass" row.** That's a CI invariant, not a test.

## Code example

```csharp
// PLang.Tests/App/Serialization/OutAttributeInventoryTests.cs

private static bool HasOut(System.Type t, string prop)
    => t.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
        ?.IsDefined(typeof(global::app.OutAttribute), inherit: true) ?? false;

[Test] public async Task Path_Absolute_NotOut_LeaksFilesystemLayout()
    => await Task.FromResult(Assert.Fail("Not implemented"));
```

```plang
/ Tests/Serialization/MaskedSettingOnWire.test.goal
Start
/ A goal that serializes a setting{ key, value } through the channel. On the wire, key is
/ visible and value is the literal string "****" — never the configured secret. Receiver
/ knows the setting EXISTS without ever seeing what it holds.
- throw "not implemented"
```

## What's next

`coder` runs next. The test surface is the contract; the test bodies become real once the implementation lands. Suggested order: Stage 1 (mechanical attribute placement + RawSignature deletion) → Stage 2 (Normalize + IWriter + JsonWriter) → Stage 3 (`As<T>` tree-walker). Each stage's test files turn from `Assert.Fail` to real assertions as the corresponding code lands.
