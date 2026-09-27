# No throwaway channels: one door that turns bytes into a value (decision 110), the architect's own trace

Written before reading the coder's sketch. The comparison is at the end.

## Why

Five production places build a channel only to call its `Read(byte[])`, which turns bytes already in hand into a value. A channel is something you talk through, so decoding is the type's job. Ingi: "aha, like it".

## What exists today (read 2026-09-27)

**Two doors already do the same decode:**
- `channel.Read(byte[] raw)` (`channel/this.cs:226-245`): if the MIME's serializer is the transport (`application/plang`, a Data container), `serializers.Transport.DeserializeAsync`; else `type.list.Mime(Mime, ctx)` → `type.Create(raw, ctx)`. When the channel has no actor, the type isn't looked up and it falls back to binary.
- `path.ReadText(ctx)` on a file path (`path/file/this.Operations.cs:59-95`): `type = type.list.Mime(Format.Mime(Extension), ctx)` → `type.Create(bytes, ctx)`, the same two lines, lazy. That's how `goal.Load` reads a `.pr` ("the path reads itself and parses by MIME").

**The throwaway sites:**
1. `type/item/file/this.cs:86-98`: the file's value door does `Path.ReadBytes` (cached in `_bytes`), then `new file.channel(Path, ctx).Read(bytes)`.
2. `type/item/url/this.cs:80-87`: a fetched body, decoded by Content-Type through `new http.channel(ct, bytes, ctx).Read()`; else by the URL's extension through `new file.channel(Path, ctx).Read(bytes)`; else as `text/plain` through an http channel.
3. `module/action/http/code/Default.cs:447-455`: the response body through `new http.channel(contentType, bytes, ctx).Read()`. This duplicates 2's Content-Type rule.
4. `module/action/file/read.cs:58-63`: `if (mime.StartsWith("application/plang"))` → `new file.channel(path, ctx).Read()`. A handler branching on a MIME prefix (*fork*) to get a `.pr` deserialized as a container.
5. `channel/type/file/this.cs:55-56`: a public `new Read(byte[])` over the base's protected one, which exists only for sites 1 and 2.

**The transport MIME is also checked by its string elsewhere (*raw hand-off*):** `http/code/Default.cs:428,641` (`StartsWith("application/plang")`), `file/read.cs:59`, and the serializer lookups `GetByType`/`GetOrDefault("application/plang")` at `data/this.Transport.cs:57`, `app/this.cs:455`, `build/code/Default.cs:217` and `ui/code/Fluid.cs:111`.

## Shape

1. **One decode door: the type the MIME names makes the value.** `type.list.Mime(mime, ctx).Create(bytes, ctx)`, which is what `path.ReadText` already does. The one exception, the transport container (`application/plang`, a whole Data rather than a value), is decided **once, inside that door**, not by callers comparing strings. Check: does `type.list.Mime("application/plang")` answer an existing type whose `Create` reads a container (the internal `wire` type, "a still-encoded slice", is the candidate)? If so, the container is just another type and the door has no special case. If not, the door holds the one transport case.
2. **Sites:**
   - 1, file item: read through the path (`Path.ReadText`, which already decodes), and keep its one-read sample.
   - 2 and 3, url item and http action: decode through the door by Content-Type (else the extension's type, else text). The two copies of "response → value" become one, most likely the http path's own read (`path/http/this.cs:174`, `ReadText` → `Send(…, readBody: true)`), which the url item already sits on.
   - 4, file.read: the `StartsWith("application/plang")` branch goes. A `.pr` reads like any file, and the door knows the container.
   - 5: the file channel's public `new Read(byte[])` goes.
   - The base's protected `Read(byte[])` becomes a one-line hand-over to the door, so real channels (stream, http) that receive bytes decode the same way, or it goes if every kind can call the door.
3. **Result:** every channel left is a real, registered one (a context, events). "A channel in no list has no context" disappears. The stage-12 note narrows to tests that build a bare channel.

## What to watch

- **Laziness:** today the decoded value is lazy (materialized on first touch by the reader). The door must keep that (`type.Create(raw)` holds the raw form), not parse eagerly.
- **The build-mode `.pr` snapshot** inside `path.ReadText` (`:74-79`, `:91-92`, a TODO about build-mode inversion): keep the behaviour, and don't copy it.
- **Naming, for stage 9:** `path.ReadText` answers typed content (a goal, a dict), not only text. The honest name is `Read`.
- **The consumers:** twins/goldens don't read decode paths. Run `plang --test`, the builder rebuild check, and the http/url/file tests.

## Comparison with the coder's sketch

(to fill in after reading it)
