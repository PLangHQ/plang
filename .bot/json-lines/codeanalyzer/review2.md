# codeanalyzer — json-lines, review 2 (9ed572965 → db75159fd)

**Verdict: PASS** — F1 and F2 fixed, the disposal note taken. Tester next.

- **F1** (19fb74d81): headless encodes the cursor through the text kind (`list["text"].kind.Encode`) and delivers a text of the app's text type, `_emit` back to `Func<string, Task>`: the window's `Draw` reads the line as before, one compact line whatever json's layout. The text kind writes a leaf bare and a structure as compact json, so the layout setting can't reach it. An encode that fails sends nothing (the same shape as before the branch).
- **F2** (bd2d1ac7a): `kind`.
- **Writer** (db75159fd): `IAsyncDisposable`; jsonl's Encode `await using`s it, disposing `_utf8`, `_line` (made on the first re-laid record) and the record buffer; golden re-pinned for `DisposeAsync`. Read whole: a reset writer has nothing pending, so the disposal flushes nothing onto the stream.
- Green: HeadlessCursorTests 2, JsonlKindTests 12, JsonlInjectionProbeTests 2, SurfaceTests 3, CursorLineProbeTests 1. No bench rerun: the diff touches no runtime path beyond the cursor line and a writer's disposal (the first row stands).
- Not blocking: HeadlessCursorTests build the line with their own call to the text kind, not through headless (Chromium can't run in a test), so they pin the text kind's output and `Window.Field`'s reading, not that headless still uses the text kind. The real Draw run stays on the Windows list.
