# Step 0 — findings (current code, before edit)

1. **Already off-stream / already demolished:** `LiftDataIfShaped`,
   `LiftArrayElements`, `HasDataMarker` are gone (only stale comment refs in
   `Wire.cs:126,367`). Container raw-slot backing (Part B) landed:
   `list._items : List<object?>`, `dict._value : Dictionary<string,object?>`.
2. **Still eager (what this change replaces):** `Wire.ReadBody` value slot —
   `JsonDocument.ParseValue` → `item.serializer.json.Parse` (a JsonElement DOM
   then a re-read), plus the `deferredRaw` + `GetRawText` re-stringify behind
   `IsDeferrableShape`, plus `_readDepth`/`MaxReadDepth`.
3. **`@schema:data` nested-Data is real, keep it:** `item.serializer.json.Parse`
   reconstructs a marked element via `IsDataMarked` → `Deserialize<data>`; the
   signature layer reads via `ReadSignatureLayer` (auto-verify-on-read). Both stay.
4. **Object-raw registry has 4 consumers** (all must move when it's deleted):
   `type.Deserialize:410`, `source.Value:73,79`, `json/converter.cs:77`
   (mid-graph path field), `kind.TypeOf:34` (kind→inner-type-name).
5. **The recursion seam:** `list`/`dict` element read = a full Data-envelope read.
   Needs a generic `ReadData<TReader>(ref TReader, ctx)` entry the containers can
   call, that Wire (STJ side) also adapts to. JsonReader must wrap
   `ref Utf8JsonReader` (a **ref field**, C# 11+) so element advances thread back
   to STJ's original reader — copying the struct would desync STJ's position.
