# codeanalyzer summary (json-lines)

**Version:** v1

**What this is:** json-lines: a jsonl kind, errors.jsonl written through it, the headless cursor sent as a value, json formatted by default (`%!app.type.kind.json.setting.layout%`), the walk reaching a kind's settings.

**What was done:** v1 FAIL: F1 the Windows window's `Draw` reads the cursor only from a Text line, so the cursor item the headless browser now sends is ignored; F2 a creative name (`lines`). Rest clean; bench in band (request 1.79 ms / 538 KB, 17.51 ms / 3865 KB; flat); announced mutations red.

**Next:** coder fixes F1/F2, then re-review. Not reviewed: the Windows run.

**Code example:**
```csharp
await _emit!(new cursor.@this(css));   // headless: a value now; window Draw must ask the value, not the old text line
```
