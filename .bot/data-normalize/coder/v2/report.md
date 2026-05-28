# coder v2 — addresses codeanalyzer v1 (M1-M3 + P1-P8)

**Status:** all 3 majors fixed, all 8 polish items absorbed. C# 3380/3380 (one new test); PLang 233/233.

## M1 — `json.Writer.Value` STJ fallback

`json/writer.cs` default branch now throws `NormalizeException("NormalizeUnexpectedLeafType")` instead of handing unrecognized objects to `JsonSerializer.Serialize`. The wire-view filter is closed — anything Normalize misses fails the write loud and typed at the gate instead of leaking reflected property dumps.

## M2 — `Properties` slot bypass

`Wire.Write` (line ~391-405) and `json.Writer.EndRecord` both routed `Properties` values through raw `JsonSerializer.Serialize`, sidestepping the wire filter for whatever a caller deposited there. Both now route through `Data.NormalizeValue + Writer.Value`, the same pipeline as the value slot. Asymmetry gone.

## M3 — `EnsureInnerSigned` dictionary blind spot

`IDictionary` enumerates as `DictionaryEntry` boxes, not values — the existing `IEnumerable` branch in `EnsureInnerSigned` never reached inner Datas held as dict values. Added an explicit `IDictionary` branch ahead of the generic `IEnumerable` check that iterates `dict.Values`. New test in `CanonicalizationTests.EnsureInnerSigned_RecursesIntoDictionaryValues` proves the inner Data in a dict gets sealed before the wire bytes leave.

## P1 — bare `catch` in `Reconstruct.ReconstructObject`

Split into `catch (MissingMethodException)` → `NormalizeNoReconstructionStrategy` (strategy-missing case) and `catch (System.Exception ex)` → `NormalizeReconstructFailed` with the inner exception attached (ctor-body-threw case). The original swallowed both into "no strategy."

## P2 — `IsDefined(..., inherit: true)` dead-cargo

`PropertyInfo.IsDefined`'s `inherit` flag is a no-op — base property attributes don't propagate to overrides regardless of the flag. Dropped all 11 callsites in `Tagged.Compute` to `inherit: false` (no behavior change), with a comment block explaining the quirk so a future reader doesn't reintroduce it. Stage 1's `FilePath`/`HttpPath` workaround of explicitly re-applying `[Out]` on the override is the right pattern — the comment points there.

## P3 — IWriter contract narrower than Normalize's leaf set

Two parts:

1. **`IWriter` widened** with explicit slots for `Float`, `Enum`, `DateTimeOffset`, `TimeSpan`, `Guid`. The contract now matches what Normalize actually produces; a protobuf/MsgPack writer that conforms to `IWriter` can encode every leaf Normalize emits.
2. **`json.Writer.Value`** dispatches through the new methods instead of falling to the STJ default for BCL leaves. Combined with M1 (default → throw), the writer is now closed: every leaf has a typed slot; anything else fails loud.

## P4 — `[Out, Store]` on Data envelope properties is documentation

Wire writes `{name, type, value, properties, signature}` by hand and never consults `Tagged.PropertiesFor(typeof(Data))`. Normalize's nested-Data branch short-circuits before NormalizeObject runs on `@this`. The tags advertise the intended wire shape; the actual emission is hard-coded in `Wire.Write`. Tests assert their presence (`OutAttributeInventoryTests`, `DataTests.Signature_HasOutAttribute`), so removal would regress the contract surface. Added a comment block above `Data.Properties` (this.cs:185) documenting that these annotations are documentation, not active filtering.

## P5 — `Reconstruct<T>` unconditional `Normalize()` on entry

Added a cheap leaf-shape pre-check (`IsLeafShape`) that skips Normalize when `Value` is already a tree-native leaf (null, primitive, BCL leaf struct, enum, byte[], nested Data). Domain objects and raw collections still need Normalize to decompose into property bags. A deeper "is the whole graph normalized?" check would itself require a walk and saves nothing in the common path.

## P6 — silent drop on dict-key conversion failure

`Reconstruct<Dictionary<K,V>>` previously ignored entries whose `child.Name` couldn't convert to `K` — the receiver got a partial dict with no signal. Now throws `NormalizeException("NormalizeReconstructFailed")` with the offending key + target type in the message. Matches the rest of Reconstruct's error model.

## P7 — `IsTreeLeafType` ≠ `NormalizeValue`'s leaf set

`IsHomogeneousPrimitive` consulted `IsTreeLeafType` which lacked `DateTimeOffset`, `TimeSpan`, `Guid`, and enums — a homogeneous `List<Guid>` fell into the wrapped-list path instead of the bare-list fast path. Lists now in lockstep; comment above `IsTreeLeafType` flags the dependency.

## P8 — Path scheme is informational on the wire

The path hook reads only `"relative"` from the normalized tree — `path.Resolve` recovers the scheme from the path string itself (`"http://…"`, absolute file paths, etc.). The `scheme` child exists for receivers that want to dispatch without parsing, but our reconstruction doesn't. Added a comment block in `DiscoverHook`'s path branch (this.Reconstruct.cs:250) documenting this so the next reader doesn't assume scheme is load-bearing.

---

## Tests

| Suite | Result | Notes |
|-------|--------|-------|
| C# | 3380/3380 | +1 test (M3 dict-recursion fixture) |
| PLang | 233/233, 0 stale | unchanged |

## Verdict for next bot

Coder pass v2 complete. All v1 findings addressed; the M-series no longer leaves a wire-leak or signing-gap latent. Back to codeanalyzer for v2.
