# Reviewer findings on app-systems, as the architect checked them

Each finding the reviewer bots report, checked against the code before it goes anywhere. Status: **fix** (sent to the coder, batched), **carried** (already on `next-branch.md`), **blocks** (to Ingi), **rejected** (with why).

## codeanalyzer (plang-ae), area 1: data and the wire (26fa8f4e7)

| finding | checked | status |
|---|---|---|
| the set-child bridge, `data/this.Navigation.cs:51–63` | cited, on next-branch.md (59's remainder) | carried |
| readers throw their refusal, `data/schema/signature.cs:83` | cited, on next-branch.md (714) | carried |
| parsed json is a clr carrier, `Operator.IsType`'s promise | cited, on next-branch.md (the `{item, json}` door, 717) | carried |
| dead `MaxNormalizeDepth`, `data/this.Output.cs:21` | grep: referenced only at its declaration | fix (batch 1) |
| `IsDataMarked(JsonElement)` on data, `data/this.cs:56` | grep: only caller is `type/item/serializer/json.cs:94,187`; a static taking a `JsonElement` on data is a stray helper and a clr reach; it belongs on the json serializer beside its caller; `internal`, so not a surface change | fix (batch 1) |

## codeanalyzer (plang-ae), area 2: channel and stream (29864e666)

| finding | checked | status |
|---|---|---|
| the channels' `catch (Exception) when (not fatal)` | read: the base doors (`channel/this.cs:143,166,190`) turn a transport throw into a typed error and let `AppException` keep its key; a real I/O boundary, not a smell | cleared |
| F2.1 `question?.Clr<string>()` for an emptiness check, `stream/this.cs:160` | read: lowers the question to a CLR string to ask "is it empty"; the value answers its own emptiness (opened box / clr leak at a leaf) | fix (batch 1) |
| F2.2 the transport boundary twice | read: `AskAsync` (`channel/this.cs:189–194`) catches what `stream.Ask` (`stream/this.cs:197–202`) already catches, and `ReadAsync` (`:165–170`) what `stream.Read` (`:135–139`) does; the two disagree: stream lets `OperationCanceledException` pass, then the base turns it into AskError (stored twice, and the cancel is lost). One boundary, in the base door, cancel excluded; the coder checks for direct callers of `stream.Read`/`Ask` first | fix (batch 1) |
| F2.3 `ReadAllBytesAsync` throws on `!CanRead`, "kept for v1 callers", `stream/this.cs:245–264` | grep: `ReadAllTextAsync` has no production caller (two tests); `ReadAllBytesAsync` has one, the stream's own `Read` (`:130`), whose catch turns the throw into ReadError. Both are public on stream, a locked surface: removing them changes the surface golden | carry (a surface removal is Ingi's; batch it with other surface asks) |
