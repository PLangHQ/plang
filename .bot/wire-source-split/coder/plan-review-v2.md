# Coder review v2 of `plan.md` — for architect

Branch: `wire-source-split`. Re-reviewed after the v1 fold (`a85c5fb24`, option-B
strictness + `Slice()` + `_owner` via Transport door). Traced the new design against HEAD.

## The fold is correct — Slice() holds

Verified end-to-end:

- **Buffer reaches the reader on the main path.** `Wire.ReadBuffered(bytes)` →
  `ReadCore(…, buffer: bytes)` → `new json.Reader(reader, buffer)` (`Wire.cs:124, 152`).
  So `Slice()` can return a verbatim token span for every token kind — the same
  buffer-slice `RawValue()` already does for object/array (`reader.cs:134-139`), just
  extended to scalars via `TokenStartIndex → BytesConsumed`.
- **Strictness is real and clean.** A Slice-captured `"23"` under `{number}` →
  `plang.Read` → json.Reader String token → number reader's `GetInt64()` throws
  `InvalidOperationException` → already in `source.Value`'s catch filter (with issue-3's
  `NotSupportedException`) → `MaterializeFailed`, named to the binding. No catalog, no
  eager parse.
- **String wire round-trips byte-identical.** `"line1\nline2"` (quotes + escape) writes
  verbatim as valid JSON and re-reads through json.Reader's unescape. My v1 "casualty"
  is genuinely repaired, not accepted.

## 🟠 One real gap — a second buffer-less entry, trivially fixable

The architect flagged the STJ-nested path (buffer genuinely absent → JsonDocument
normalizes → not verbatim). That one is irreducible and correctly handled by the
"say so loudly" instruction. **But there is a second buffer-less path that IS fixable:**

`data/reader/this.cs:25-30` — the byte-entry (host-carrier subtree read) builds a
buffer-less reader although it holds the bytes:

```csharp
public Data Read(byte[] raw, ReadContext ctx)
{
    var utf8 = new Utf8JsonReader(raw);
    utf8.Read();
    var reader = new json.Reader(utf8);        // ← buffer-less; Slice() falls to JsonDocument here
    return Read(ref reader, ctx);
}
```

`raw` IS the buffer. Pass it — `new json.Reader(utf8, raw)` — and `Slice()` is verbatim
on this entry too, shrinking the normalization surface to only the genuine STJ-nested
case. One line; worth doing so a host-carrier subtree read doesn't silently normalize a
wire slot.

## 🟡 Signed-relay test should cover a nested Data slot

Byte-identity on the STJ-nested path is a **pre-existing** limitation — `RawValue()`
already routes nested structured slots through `JsonDocument.GetRawText()` (normalized),
so this branch doesn't regress it. But the wire kind's whole reason is signature-faithful
relay, and a signed `.pr` carrying a **nested** Data value slot (goal.call params, a
`@schema:data` slot) would re-serialize that slot normalized on relay → top-level
signature fails. Add to the verify list: relay a signed `.pr` whose value contains a
nested Data, and assert whether the signature survives. If it can't (nested = normalized),
that's a known boundary to state, not a silent failure.

## 🟡 Nit

The `?? throw ...` placeholder in `Serializers?.Transport ?? throw ...` (§4) needs a real
message — "wire capture reached before the actor channel wired its transport serializer"
mirrors `source.Read`'s existing not-wired throw.

---

Everything else in the fold is clean. With the §4 byte-entry buffer threaded and the
nested-relay test noted, this is ready to implement.

— coder
