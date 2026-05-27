# test-designer — data-serialize-cleanup

**Version:** v1

## What this is

Data-serialize-cleanup is an architectural pass on PLang's serialization boundary. Today the variable→wire→variable round trip is tangled across four files and three concepts: the plang+data serializer builds a parallel `Envelope` class just to bypass `[JsonIgnore]` on `Signature`; STJ options are duplicated in three places; `Stream.WriteCore` strips the Data wrapper before serializing then the plang serializer reconstructs it from scratch; `Compress` double-wraps as `Data{archived, Data{gzip, byte[]}}`.

The architect's design un-tangles it by accepting that **Data is the universal currency** — the wire shape IS Data's own shape, and any nesting lives in the byte stream, not the JSON document. Five stages: (1) ISerializer input tightened to Data + OBP renames, (2) merge plang serializers + sign-in-converter + canonicalization fix, (3) flatten Compress, (4) Properties get a wire scope, (5) vocabulary sweep.

This v1 produces the failing-test contract that pins the architecture: the coder makes these pass.

## What was done

Wrote 11 C# test files + 4 integration-cut classes + 7 PLang `.goal` files. ~90 test method stubs total, every body `Assert.Fail("Not implemented")` / `- throw "not implemented"`.

**Files written:**

C# under `PLang.Tests/App/Serialization/`:
- `ISerializerInputContractTests.cs` — Stage 1 input tightening
- `SerializerRenameTests.cs` — `Type` / `Extension` / `GetByType` / `Types` renames
- `ChannelHookRenameTests.cs` — `Write` / `Read` / `Ask` rename on 6 channel subclasses
- `MergedPlangSerializerTests.cs` — single `application/plang`, Envelope deleted
- `WireConverterSigningTests.cs` — sign-if-missing in the converter walk
- `CanonicalizationTests.cs` — crypto.Hash uses Transport.ForOutbound
- `JsonCompositionTests.cs` — `WithConverter`, `WithModifier`, `ForInbound`
- `CompressFlattenedTests.cs` — flat Compress (no nested gzip Data)
- `PropertiesWireShapeTests.cs` — nested `properties` object on the wire
- `TransportRenameTests.cs` — `this.Envelope.cs` → `this.Transport.cs`
- `FailureMatrixTests.cs` — typed negative paths

Integration cuts under `PLang.Tests/App/Serialization/IntegrationCuts/`:
- `Cut1_PlainRoundTripTests.cs` — plain Data round-trip with implicit signing
- `Cut2_SignThenCompressTests.cs` — sign-then-compress preserves inner attestation
- `Cut3_MultiActorForwardingTests.cs` — forwarding chain preserves provenance
- `Cut4_PropertiesWireTests.cs` — Properties wire shape + canonicalization binding

PLang goals under `Tests/Serialization/`:
- `CompressRoundTrip.test.goal` (3.9)
- `PropertiesBangNavigation.test.goal` (4.10)
- `ValueVsPropertiesDisjoint.test.goal` (4.11)
- `PropertiesBangChainedNavigation.test.goal` (4.12)
- `NegationPrefixStillParses.test.goal` (4.13)
- `VariableRendersValueOnly.test.goal` (4.14)
- `DoubleBangParseError.test.goal` (failure-matrix double-bang)

## Decisions diverging from the architect's matrix

Ingi confirmed up front that the matrix is suggestion, not gospel — the test surface is mine to own. Specific moves:

- **Pruned non-tests.** Rows 5.2 (`git grep -i envelope` returns no matches), 5.4 (local variables renamed), 5.5 (projects build clean), 5.6 (existing tests pass) — these are CI/grep invariants, not unit tests. The Transport rename + behaviour assertions cover what's actually verifiable from C#.
- **Grouped by topic, not by stage.** A future reader looking for "where is the Properties wire shape tested" shouldn't have to know that maps to Stage 4. File names follow the concept (`PropertiesWireShapeTests`, `WireConverterSigningTests`), not the stage number.
- **Added a wire-converter byte[]-leaf test.** Stage 3's flat Compress puts a raw `byte[]` in `Value`; the wire converter has to emit those bytes without wrapping in a nested Data. Architect implied it; I made it explicit.
- **Added `Json.ForInbound` symmetry test.** Listed in the new-surfaces inventory but no matrix row pinned it. Added because it'll be the first thing someone wires up after `ForView`.
- **Reflection over compile-fail.** Rows like 1.2 ("non-Data input fails to compile") and 4.18 ("old IList<Data> indexer fails to compile") can't be expressed as a TUnit body. The compile-time guard is the real assertion (calling sites die); these tests express it via reflection — the absence of the old surface on the type.

## Code example

```csharp
// PLang.Tests/App/Serialization/WireConverterSigningTests.cs

public class WireConverterSigningTests
{
    // 2.6 — Unsigned Data → converter calls EnsureSigned, emits the populated signature.
    [Test] public async Task WireConverter_OnUnsignedData_FiresEnsureSignedAndEmitsSignature()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.7 — Already-signed Data → converter leaves the signature unchanged (idempotent).
    [Test] public async Task WireConverter_OnSignedData_LeavesSignatureUnchanged()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
```

PLang test goal style:

```
Start
/ %response!cost% must resolve to Properties["cost"] — the bang operator reaches into
/ the Properties scope, not into Value. Coverage row 4.10.
- throw "not implemented"
```

## Build state

`dotnet build PLang.Tests` → 0 errors, only pre-existing warnings unrelated to this branch. The contract is ready for the coder.

## Next

Run **coder** to make the tests pass, in stage order (1 → 2 → 3 → 4 → 5). Stages 1 and 2 are tightly coupled — likely a single PR.
