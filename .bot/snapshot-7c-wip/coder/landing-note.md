# snapshot-7c-wip — landing note (surface re-pin)

The surface golden (`PLang.Tests/Wire/App/Surface/surface_golden.txt`) was last re-pinned at b4b562053 (snapshot 103).
This is every line the re-pin moves, taken from the uncapped diff of the current surface against that pin
(the test shows only the first 20 each way), each with the decision that allowed it. 19 lines added, 34 removed.

Re-pin: `OPENAI_API_KEY= PLang.Tests/Wire/bin/Debug/net10.0/PLang.Tests.Wire --treenode-filter "/*/*/SurfaceTests/AcceptTheSurface"`,
and the commit names the decisions below.

## Approved by Ingi (was held)

Known red that lands with 7c: the plang `FreshAppGoesOn` (resume reshaped as an app instance, its own stage after 7c).

| change | decision |
|---|---|
| `kind.Read(obj, reader, kind, ReadContext ctx)` replaces `(…, actor.context context)`; json's override likewise | review 8 (2f263160e); approved by Ingi, snapshot 158 |

## Removals

| surface line | decision |
|---|---|
| `app.this.Start(goal, context, ct)` | 143 (review 31) — a goal starts itself |
| `app.type.reader.this.Typed(typeName, kind)` (the view-less one) | 123 (review 3) |
| `app.call.this.static Create(object raw)` and the six frame kinds' (`action`, `binding`, `composed`, `goal`, `isolated`, `step`) | 140 (review 35) — ICreate's own default |
| `app.call.Diff` positional `ctor(Name, Before, At)`; its members `{ get; init; }` → `{ get; }` | 153 (review2 N4) — one birth |
| `app.type.item.variable.list.this`: `Clone()`, `GetChangedSince(since)`, `GetNames()`, `Restore(snapshot)`, `Save()`, `ToDictionary(includeSystem)` | 153 (review2 O1) — no runtime code for tests |
| action `signing.sign` property `StoreView : bool` | 117 (Ingi's "action.StoreView — obpv"): `sign.View` is the one member |
| `app.call.list.this`: `Diff()`, `History()`, `Tags()`, `Timing()`, `Variables { get; }` | 111 (boy-scout ruling: test-only members) / 114 (a11d5b71e) |
| `app.call.this.Variable : IEnumerable<data> [Store]` | 113 (Ingi's 4) — replaced by `Parameter` |

## Changed (a removal and its addition are one change)

| from → to | decision |
|---|---|
| `app.this.Snapshot(context)` / `Snapshot(error, context)` : `data<snapshot>` → `Task<data<snapshot>>` | 113 (Ingi's 3: taken once, as bytes) |
| `app.call.list.this.Push(…, action held = …)` / `Isolate(…, action held = …, caller = …)` → parameter named `call` | 111 (creative name) / 112 (`_call`/`call`) |
| `Tagged.PropertiesFor(Type type, View mode)` → `View view` | 111 (creative name) / 114 (e426b0df1) |
| `property.list.this.static Read(ref json.Reader reader, …)` → parameter `json` | 111 / 114 (ba8312e6e) |

## The signature's view (review2 item 1, Ingi 156)

| from → to | decision |
|---|---|
| `signature.this.View : text?` → `View : app.View`, born with the signature; the public constructor takes `View view` (required, after `signature`), no longer an optional `text? view` last | 153 (review2 item 1) |
| a signature that names no view is refused (`SignatureInvalid`) | Ingi 156 (strict, no backward compat) |

## Additions

| surface line | decision |
|---|---|
| `app.call.this.Parameter : property.list [LlmBuilder, Store]`, and `.parameter` on each frame type (`call`, `call.action`, `call.binding`, `call.composed`, `call.goal`, `call.isolated`, `call.step`) | 113 (Ingi's 4); its `[LlmBuilder]` 121 |

## Not on the golden, for the record

These change members outside the golden's locked namespaces, or internal ones, and need no re-pin:
- `action.Synthetic` removed (review 4); `app.error.CallbackGoalNotFound` / `CallbackActionMismatch` removed (review 33).
- `Data.Error` an auto-property (same signature); Data's private `Take` (review 13); the observed-error tracking removed (127).
- `goal.Pr` is now the path (`path?`), the `file` item wrapper gone (review 32); goal's stamp moved into `goal.list` (131).
- Each frame's `Rest` on binding removed (review2 N2); `View.Make`/`Reader` with one private `Registered`, and the registry's internal `Registered` (149, review2 N1).
- The ICode, Copy/Clone, `Uninitialized`, `IncomparableException` and module-registry proposals are Ingi's and change nothing yet.
