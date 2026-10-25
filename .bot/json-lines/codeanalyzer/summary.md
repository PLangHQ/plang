# codeanalyzer summary (json-lines)

**Version:** v2

**What this is:** json-lines: a jsonl kind, errors.jsonl written through it, json formatted by default (`%!app.type.kind.json.setting.layout%`), the walk reaching a kind's settings.

**What was done:** v1 FAIL: the headless cursor became an item the Windows window's `Draw` ignored. v2 PASS: the cursor is a compact text line again (written by the text kind), `kind` named, the jsonl writer disposed. Bench in band at v1 (request 1.79 ms / 538 KB, 17.51 ms / 3865 KB, flat).

**Next:** tester and security. Not reviewed: the Windows run (a real Draw).

**Code example:**
```csharp
await context.App.type.list["text"].kind.Encode(stream, context.Ok(new cursor.@this(css)), context);   // one compact line, whatever json's layout
```
