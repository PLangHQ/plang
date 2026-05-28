# security — data-normalize — summary

- **Version:** v1
- **Status:** PASS

## What this is

`data-normalize` lands structural normalization on `Data` so the transport
serializer becomes format-agnostic — `Normalize()` walks any C# value into a
uniform tree of `primitive | byte[] | Data | List<>`, and format encoders
(JSON today, protobuf/MsgPack/CBOR later) implement an `IWriter` over that
tree without reflecting. The reverse direction (`Reconstruct<T>` / `As<T>`)
is a tree-walker driven by the caller-supplied generic type, with per-type
`FromNormalized(Data, Context)` hooks for types that need custom
construction (`path.@this` → `Resolve(relative, ctx)`).

The branch also introduces `[Masked]` (emit `"****"` in place of value),
deletes the legacy `RawSignature` field in favour of `Signature`, switches
sqlite persistence to the new `Store` view (so `[Sensitive, Store]`
properties like `Identity.PrivateKey` round-trip), and tags the inventoried
domain types (`Identity`, `setting`, `Variable`, `path.@this`,
`http.Response`).

## What was done

Read end-to-end:

- `PLang/app/data/this.Normalize.cs` — bounded (128-depth cap, cycle set).
- `PLang/app/data/this.Reconstruct.cs` — bounded by `Wire.Read`'s 64-depth
  cap for wire input; T is caller-controlled.
- `PLang/app/data/Wire.cs` (renamed from `WireJsonConverter.cs`) — `MaxReadDepth=64`
  carried forward; `View` per-instance; `Properties` sidecar primitives-only.
- `PLang/app/channels/serializers/json/writer.cs` — `_view` threaded
  through constructor, **fails closed** on unknown leaf type.
- `PLang/app/channels/serializers/filters/Tagged.cs` — per-(type, mode)
  filter; transparent-fallback for untagged types is a design-aware
  footgun (memory note).
- Attribute migrations on `Identity`, `setting`, `Variable`,
  `path.@this`, `http.Response`.
- `PLang/app/modules/settings/Sqlite.cs` — switched to `Store` view.

Verdict: **PASS**. No critical or high findings open.

## Code example — the gate that survives untagged future types

`json.Writer.Value` is the OBP-cleanup that closes the
"unknown CLR type bypasses [Out] filter via STJ default object path" leak:

```csharp
default:
    // Reaching here means Normalize handed off a value whose runtime
    // type isn't in the tree contract. Falling back to STJ would
    // reflect every public property and bypass [Out]/[Sensitive]/[Masked]
    // discipline — the wire could leak fields the filter excludes.
    // Fail closed instead.
    throw new app.data.NormalizeException(
        $"json.Writer received a value of type {normalized.GetType().FullName} that isn't part of the tree contract...",
        "NormalizeUnexpectedLeafType");
```

The previous architecture had multiple sites that fell back to plain
`JsonSerializer.Serialize`, any of which could re-emit a domain type's
public properties without the tag filter applied. After this branch the
only path from a C# domain object to wire bytes runs through
`Normalize` → `Tagged.PropertiesFor` → `IWriter`. Fail-closed at the
writer's `default` case backstops the whole pipeline.

## Notes (not findings)

- **Tagged transparent fallback** — a type with no `[Out]/[Store]/[Sensitive]/[Masked]`
  ships all public properties in both Out and Store views. Architect-intentional
  for nested helper records; standing footgun for future domain types.
  Captured to security memory.
- **`BuilderPlannerFailed` plan dump uses `JsonSerializer.Serialize` with no
  options** — matches the existing 15-site semgrep baseline; `planValue` is
  LLM-JSON with no `path.@this` / `[Sensitive]` reachability. No separate
  finding.

## Next

```
VERDICT: PASS
Next: run.ps1 auditor data-normalize "Review the code on branch data-normalize" -b data-normalize
```
