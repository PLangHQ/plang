# codeanalyzer — json-lines, review 1 (0a178687b → 9ed572965)

**Verdict: FAIL** — one blocker (F1), one small naming finding (F2). The kind, the writer, the setting and the walk are clean. Coder next.

## F1 (blocker, traced): the headless cursor no longer reaches the Windows window
- `headless/this.cs:202` now sends `new cursor.@this(css)` (an `item`, not a `Text`) to OnFrame. Before it was a `Text` holding `{"cursor":…}`.
- The consumer of OnFrame's `%!data%` is `screen.draw` → `window/this.cs:112 Draw`. Its cursor path is `if (value is Text t) { … else if (Native.Field(line, "cursor") is { } css) _window.Cursor(css); }` (:117–125). A `cursor` item is not a `Text`; it falls to `value?.RawBytes is not { }` (:128) → "not a frame: ignored", `context.Ok()`. The pointer silently stops changing.
- The cursor's own doc (`cursor/this.cs:3–5`) says the window showing the frames turns it into the system's pointer; `draw.cs:7` says draw takes what OnFrame gives. Nothing in the branch or its tests reaches `Draw` with a cursor (HeadlessCursorTests/CursorLineProbeTests stop at a framed channel); nobody consumes the cursor from `os/` goals either, so the window is the one consumer.
- Not run: the Windows window can't run here (on the Windows list already). Traced from the code; no probe possible on Linux.
- Root: the producer changed its shape (Text line → value) and the one consumer still reads the old shape. Fix at the consumer: `Draw` asks the value, `value is cursor c` → `_window.Cursor(c.ToString())` (and `Native.Field(line, "cursor")` + its `Window.Field` go if nothing else sends the text line). A test that reaches `Draw`'s cursor (a window needs Windows; if `Draw` can't be tested off Windows, say so in the decision).

## F2 (small): creative name, `window/this.cs:51`
`var lines = file.Kind(context).kind;` holds a kind; name it what it is (`kind`) — beside `json` (the json kind) and `line` (a string) two lines below, `lines.Encode` reads as a list of lines.

## Notes, not blocking
- Older, tracked to this file, not this branch: `Said` (`window/this.cs:86–96`) still hand-builds stats.jsonl and media.jsonl lines (`"{\"at\":…" + said[1..] + "\n"`) from guest json text — a splice into text, a different shape than errors.jsonl's value; and `Kept`'s `continue` on a failed encode swallows the answer (same line existed with json).
- `OpenAi.cs:534` (line this branch rewrote) drops `Encode`'s answer, so a failed encode sends "" to the model. The line did so before; OpenAi's older smells go with llm-provider (Ingi, bugs 23).
- `jsonl/writer.cs`: the two `Utf8JsonWriter`s are never disposed (json's Encode `await using`s its one), and `_line` is allocated per writer though only a pasted record needs it. Cheap; make it lazy if the writer is touched again.
- jsonl's `Encode` catch pair repeats json's; two copies, same shape.

## Clean (read whole, smells run by name)
`jsonl/this.cs`, `jsonl/writer.cs` (the 20 refusing tokens ruled in architect 5), `json/this.cs`, `json/setting/this.cs`, `json/layout.cs`, `kind/this.cs` (`Get` skips `setting`, `Setting` is the class's own), `node/root/this.cs`, list's and item's single new `Write(jsonl.Writer)` (read at the methods; the two files are 1064/925 lines, older smells not read), headless, window. Surface golden: every addition is in a commit that names Ingi's yes (3, 11); the setting class and `layout` follow bugs 23. The six `.llm.json` differ only by the final LF (decision 8). Decider templates pipe scalars through `| json`, so formatting leaves their prompts as they were.

## Measured
- Bench (parts: request, ptest, sweep; `BENCH_SKIP="build birth"`), against bugs-tip: sub request 1.79 ms / 538 KB (1.85 / 543), root request 17.51 ms / 3865 KB (17.61 / 3857), held flat 1.6→2.1→2.1→2.1, ptest 4.5 s (297 pass, 1 fail), sweep 41.7 s, red only Wire. All in band: the setting read once per Encode costs nothing measurable.
- Green: JsonlKindTests 12, JsonLayoutSettingTests 8, JsonlInjectionProbeTests, HeadlessCursorTests, CursorLineProbeTests, QueryToolTests, LlmAnswersFileTests.
- Mutations (announced, reverted, tree clean): `Ended` without the re-lay → JsonContent_IsOneLine and JsonContentInADict_IsOneLine red; `Indented = false` → Json_IsFormatted_ByDefault red.
- Not run: the Windows window; `plang --test` with the llm tag (the coder's 303/308 stands).
