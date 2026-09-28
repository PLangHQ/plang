# Stage 9a.3 — the `new …type.item.X.@this(` sites, sorted by the birth rule

The rule (`type.Create`'s doc): a birth is a program value coming in from outside the type system: an action
making a value, or content decoded off I/O. A `.pr`/wire slot read, a value materializing, or a re-type inside
the type system stays `Make` or the reader's own construction.

Grep: `new (global::)?app.type.item.<x>.@this(` in `PLang/app`, outside `app/type/` → 87 sites (the architect's
count of 55 excluded generic forms). Plus the sites inside `app/type/` that birth another type's value.

## Births — moved in 9a.3 (awaited `type.Create`, `on.create` fires)

| Site | Value | Now |
|---|---|---|
| `path/file/this.Operations.cs` `Read` | `file` (with its template marker), `directory` | `type.list[{file, template}].Create(path)`, `type.list["directory"].Create(path)` |
| `path/http/this.cs` `Read` | `url` | `type.list["url"].Create(path)` |
| `variable/set.cs:262` (`as <type>` conversion, decision 175) | the converted value | `type.Create(converted)`; a before's refusal or answer is the result |
| `kind/this.cs:195` (decode) | decoded content | already a birth (8e) |

To make them possible:
- `file`, `url`, `directory` make themselves from a path (`static Create(raw, ctx)`), and each records the path in its history.
- `type.Make` lifts a non-leaf of another type through the declared type's own lift, but only when what it makes *is* the declared type **and** carries the raw as its prior. A container declared as another type is held as before. Without the guard, 43 tests broke: a dict declared `text` became text, and clr host carriers were re-wrapped.

## Births that move with their module in 9b (each module's handler becomes a one-line door; the owner births)

| Site | Value | Module |
|---|---|---|
| `list/{where,flatten,add×2,range,unique,split,group×3}` | the list an action makes | list (9b: members on `list.@this`) |
| `loop/foreach.cs:99` | the loop's result dict `{itemCount, completed}` | loop |
| `build/code/Default.cs:60,134` | `list<goal>` | build |
| `test/discover.cs:42,62` | `list<test>` | test (discovery → test's list) |
| `identity/code/Default.cs:187` | `list<identity>` (identity.list) | identity |
| `llm/code/TypeSafe.cs:27,64,66` | the answers dict (empty fallbacks) | llm |
| `llm/code/OpenAi.cs:467` via `context.Ok(raw, kind)` → `kind.Load`, fallback `new text(raw)` (`actor/context/this.cs:185`) | the LLM's decoded answer | llm (`kind.Load` isn't a birth door yet: it and its text fallback move there) |
| `signing/code/Ed25519.cs:45–58` | the `signature` (its fields are its own parts, not births) | signing (9b: `signing.sign` → the signature's owner) |

## Not births

| Kind | Sites |
|---|---|
| Tools, not values | `variable.parser` ×9 (`data/this.Navigation`, `step/this.Scope`, `step/this.Validate`×2, `pick/list`×3, `Formal`×3, `debug/this.cs:464`); `kind.reflection` writer ×5 (`app/this.cs:26`, `goal/this.Item`, `step/this.Item`, `action/this.Item`, `module/this.cs:38`, `channel/this.cs:31`); scheme kinds ×3 (`app/this.cs:311–313`) |
| Reads / typed absence / wire | `@null` in readers ×9 (`action/serializer/Reader`×2, `step/serializer/Reader`, `goal/serializer/Reader`×2, `snapshot/serializer/Reader`, `identity/serializer/Reader`, `code/registration`, `code/defaultoverride`, `data/reader/this.cs:114`); `data/schema/signature.cs` ×5 (the signature wire reader); `this.SnapshotWire.cs:37` (wire slice, `Make`); `data/this.Transport.cs:72` (archive transport) |
| Build-time rows / markers | `goal/call.cs:52,70` (rewriting the call's own `.pr` properties at build); `on/event.cs:45` (build warning → the build-warning door); `debug/tag.cs:49` (a tag marker); `output/ask.cs:74` (a variable to read, not a value) |
| An object's own fields / internal state | `test/this.cs:35,36` (Tags, Timings), `test/this.cs:58` (a lookup key), `test/list:76`, `test/report:67`, `snapshot/this.cs:35` (Entries), `actor/permission:127` (grants), `identity/code/Default:249` (setting), `actor/setting:293` (a setting module node), `action/this.Schema.cs:32` (schema capabilities), `store/sqlite:152,248` (the store's own rows) |
