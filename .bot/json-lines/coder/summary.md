# json-lines — coder summary

**Version:** v1

## What this is
plang's json gets a layout setting, formatted by default (Ingi, bugs 23). A formatted record spans lines, so what
writes one record per line writes JSON Lines instead (Ingi, bugs 45). The plan is the architect's, at
`test/plan/json-lines/start.md` (`start.goal` its index); decisions are numbered in `.bot/json-lines/architect/summary.md`.

## What was done
- **Stage 1 — the jsonl kind** (4a2dbc805, 5fbb8124f). `type/item/kind/jsonl/this.cs`: `application/jsonl`,
  `application/x-ndjson`, `.jsonl`, `.ndjson`. Read (Parse only, no rest): each line through json's own Parse into a list; CR
  before LF is the end, a blank line skipped, a bad line MaterializeFailed naming its number. `jsonl/writer.cs`: each
  record compact on its own line, every character as itself; a record holding pasted multi-line json is laid again on one
  line (json's own paste stays the author's bytes: byte-identical saves, signed rows). The token members refuse (a line
  is a whole record); `Record` is internal. `item.Write(jsonl.Writer)` makes any value one record, list's override a
  record per element (Ingi's yes, json-lines 3). Plang tests ReadsItsLines, RoundTrip.
- **Stage 2 — the line writers** (95f408fc5, 477760b5d, 4a3cae193). errors.jsonl is encoded by the file's own kind and
  appended. The headless cursor goes to OnFrame as a value; the channel it is written to ends its line (no Encode).
- **Stage 3 — the setting** (aa3215335, 055c36722, c92abb7f7). `type/item/kind/json/setting/this.cs`
  (`%!app.type.kind.json.setting.format%`, a `choice<layout>`: formatted | compact, default formatted). json's Encode reads
  it once; UnsafeRelaxedJsonEscaping (json is no page; `| json` makes it safe), `NewLine = "\n"`. The wire, a digest, a .pr
  never read it. An llm tool result goes as text. Tests about shape ask for compact through
  `PLang.Tests/Shared/JsonLayoutExtensions.Compact(app)`. The six `.test.llm.json` re-saved by plang (only the final LF
  went; json ends at its brace, ruled).

## In progress / next
- Ingi names the member: `Format` holding a `layout` is named two ways. If `layout` both, the path becomes
  `%!app.type.kind.json.setting.layout%` (golden, the test door, FormatSetting.test.goal, docs).
- plang-builder builds FormatSetting's .pr; security reviews stage 3 (the nosniff question). Then the line.

## Code example
```csharp
// jsonl/writer.cs — a record is one line: only pasted json can hold a break, and then it is laid again
var record = _record.GetBuffer().AsMemory(0, (int)_record.Length);
if (record.Span.IndexOfAny((byte)'\n', (byte)'\r') < 0)
    await _stream.WriteAsync(record, Context.CancellationToken);
else
{
    using var document = JsonDocument.Parse(record);
    document.RootElement.WriteTo(_line);
    ...
}
```
