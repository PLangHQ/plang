# Stage 3 shape — what isn't text: a media value as `[mime, size]`, never inlined

Coder: tester (plang-f2). On the kind-is-type base.

## What stage 3 delivers (plan)

```
- ask llm "what is %photo%"  (an image)       → "[image/png, 2.1 MB]", never its bytes
- ask llm "read %report%"    (an unread file) → its name, type and size
```

So a binary/image/file written into an llm message renders as a short description the model can read,
never its raw bytes (which would blow the context and the cost).

## Trace — how they render today, and the gap

- `binary.Write` → `writer.Bytes(Value)`; `image.Write` → `writer.Bytes(Bytes)` loaded, or `Path.Write`
  → `writer.String(path)` unread; `path.Write` → `writer.String(ToString())`.
- The llm writer's `Bytes` currently delegates to the text writer → `[N bytes]` (size only, **no mime**).
- The VALUE knows its mime (`image.Mime`, `path.Mime(context)`) and size (bytes length), but the
  `Bytes(byte[])` / `String(string)` primitives **carry neither** — the same wall as stage 2's column
  types (a writer gets a primitive, not the value's type/metadata).

## The decision (same family as the branch-A "type without the envelope" item)

To render `[image/png, 2.1 MB]` the llm writer needs the **mime**. Options:

- **(A) one new IWriter member — `Bytes(byte[] value, kind mediaKind)`** (default delegates to
  `Bytes(byte[])`, so text/json/plang are byte-equal). image/binary write their bytes WITH their kind
  (the format kind carries the mime); the llm writer renders `[kind.Mime, size]`, plain `Bytes` →
  `[N bytes]`. Clean, uses the mime the value already knows; but it is a **locked serializer addition →
  your/Ingi's yes** (like BeginText under 800).
- **(B) the writer sniffs the mime from the magic bytes** in `Bytes(byte[])` (png/jpg/gif/pdf/…). No
  surface change; but it re-derives what the image already knows, is imperfect for unknown formats
  (→ `application/octet-stream`), and can disagree with a strict-kind image.
- **(C) size-only** `[N bytes]` now; real mime deferred to branch A. Meets "never its bytes" but not
  the plan's `[image/png, 2.1 MB]`.

I recommend **(A)** — it reuses the known mime, keeps non-llm writers byte-equal, and is one member
(mirrors the 1b door). If you'd rather not touch IWriter again, **(B)** gets the plan's output with no
surface change.

## The unread file/image (`%report%`) — name, type, size

An unread file/image is a `path` → `path.Write` → `String(path)`. The llm writer can show the path
string (which carries the **name**) and derive the **type** from `path.Mime(context)` — but `Write` is
sync and passes neither the mime nor a size (size needs a `Stat`, i.e. I/O). Options: render
`[name, mime]` from the path string + extension (no size, no I/O at write time), or give `path` the same
kind-carrying write as (A). I lean "path string + mime from extension, no size for an unread file" —
confirm.

## Tests (red for their own reason), writer-level + end-to-end

- a loaded image value through the llm writer → `[image/png, <size>]` (not base64, not `[N bytes]`).
- a raw binary → `[… , N bytes]` (no inlined bytes).
- `ask llm "what is %photo%"` end-to-end → the request body shows the label, never base64.
- an unread file/image → its name + type (+ size per the decision).
- guard: the same binary through the json/text writer is byte-equal to today (base64 / `[N bytes]`).

## OBP / surface
| surface | check |
|---|---|
| (A) `Bytes(byte[], kind)` | one IWriter member, default byte-equal for non-llm; re-pin surface under its decision |
| the label | lives only in the llm writer; the value writes its bytes + kind, never a format switch |
