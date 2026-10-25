# json-lines

Cut from runtime2 0a178687b (the bugs merge). Small on purpose: one new kind, the writers that need it, then the setting that was waiting for it.

## Why

Ingi, bugs 23: plang's json gets a setting, `formatted | compact`, **default formatted**. But some places write json as one record per line. An indented record spans many lines, so the reader on the other end breaks.

```
- start browser 'https://visir.is', on frame call Frame
Frame
- write out %!data%
```

**Before:** the cursor line `{"cursor":"pointer"}` goes up the pipe as one line. **After the setting, without this branch:** it goes up as three lines, and the host reads three broken messages.

JSON Lines is the format for this: one compact json value per line (`.jsonl`, `application/x-ndjson`). With it, a writer that means "a record per line" says so, and plain json is free to be formatted.

## Stage 1: the jsonl kind

A kind of item beside json: `type/item/kind/jsonl/this.cs`.
- **Names:** mimes `application/jsonl` and `application/x-ndjson`; extensions `.jsonl` and `.ndjson`. It is text and compressible.
- **Read:** each line is one json value, read by json's own Parse. A CR before the LF is part of the end, and a blank line is skipped. The content is the list of those values. A line that isn't json is refused as MaterializeFailed, naming its line number. Like json, it rests unread until a use opens it.
- **Write:** each record compact on its own line, ending with LF, every character as itself (a file is not a page). A list's elements are its records, and any other value is one record: a dict is one line, never its entries.
- **Ingi's question:** "a list writes its elements as records" is a list looking different in this writer. That's the established pattern (`item.Write(json.Writer)`, `Write(text.Writer)`, `Write(formal.Writer)`, `item/this.cs:901–919`), so it takes a base virtual `Write(jsonl.Writer)` on item, plus list's override. That is an addition to the locked core, so it waits on his yes. Without it, a list written to `.jsonl` is one line holding an array, and reading a file and saving it back doesn't give the same file.

## Stage 2: the line writers write jsonl

- **errors.jsonl** (`screen/type/screen/window/this.cs:44–67`): each record is written in the file's own format (jsonl, by its extension) and appended. The `+ "\n"` added by hand goes.
- **The cursor line** (`browser/type/browser/headless/this.cs:203`): a line beside the frames, so it's a jsonl record. The coder brings the shape for who ends the line. The record's LF and the channel's End must never both land: no value check, never two ends.
- **Not a line writer, so it stays json:** the errorlog channel (`screen/type/screen/display/this.cs:78`). Its message is framed by Flush (`display/code/Errorlog.cs`) and decoded whole on the host, so formatted json reads fine.
- **The survey:** every other `Mime("application/json").Encode` (`app/parent/this.cs:96`, `display/this.cs:293`, `llm/code/Saved.cs:118`, `http/code/Default.cs:808`, `ui/code/Fluid.cs:144`). For each, the coder says whether a formatted record would break its reader.

## Stage 3: the json setting

`%!app.type.json.setting.format%`: `formatted | compact`, default `formatted`. Read once at the start of json's Encode.
- `formatted`: indented, every character as itself, the `.pr` writer's look (`goal/this.Item.cs:21–34`).
- `compact`: one line, every character as itself.
- HTML escaping leaves the json kind. The `| json` filter escapes `<`, `>` and `&` itself (bugs 24, 44), and json is no page.
- **Untouched:** plang's wire (`application/plang`), a digest (`item.Pour`, its own writer) and `.pr` (its own writer).
- **Where the setting lives:** size and duration hold theirs as types (`type/item/size/setting/this.cs`, `%!app.type.size.setting.standard%`). How the json kind holds one is the coder's shape to bring.
- **Two checks first** (bugs 23): (a) nothing signs or hashes the bytes json's Encode writes; (b) where json's output lands in an html page besides `| json` (security).
- **Then the six `test/module/llm/*.test.llm.json`** are read and saved once (no OpenAI), in one commit, so they become plang-written. Saved's match of a request must not depend on the setting: a file saved compact is still served with the setting formatted, and the other way round.

## Out

A channel's json format in general (`--channel.setting.format=json` writes formatted, which is the user's choice; jsonl is there for lines). The windows branch comes next.

## Locked surface

The jsonl kind is a new kind in the serializer (Ingi, bugs 45). The setting is Ingi's yes in bugs 23. `item.Write(jsonl.Writer)` and list's override wait on his answer.
