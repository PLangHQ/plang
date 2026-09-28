# coder v10 — stage 9 (decision 175)

Conversation mode: each slice is proposed to the architect, reviewed, then the next one starts.

## 9a — commit 1: the `Use` door + file.read as the template action
- `data<T>.Use(Func<T, Task<data>>)`: the carrier answers its own failure (it didn't resolve, or resolved to
  nothing), or else hands its value whole to the continuation. It resolves only that carrier, so laziness holds.
- `path.Read(data<bool> template, context)`: abstract on path, so the http/file fork becomes a virtual.
  - `path.http` lands a `url` reference.
  - `path.file` stats: NotFound (404), a `directory`, or a `file` born with the template marker.
  - The reference is born whole; Data takes the item's own type. The parallel `type.list[new type(...)]` and the
    no-op `TrimStart('.')` go away.
- `path.Expect(context)`: Read's build face, with no content I/O.
  - file: the reference's type, plus the missing-file warning on the builder channel.
  - http: the url type, with no probe.
- file.read: `Path` is `[IsNotNull]`; `Start` and `Build` are one line each.

## 9a — later commits (each proposed first)
- One read verb: `ReadText`/`ReadAsBase64` go, `ReadBytes` → internal `Bytes(context)`, data-uri moves to the
  item, the callers move to the landed reference, and the two sync-over-async sites are fixed as they move.
- ~58 births onto `type.Create` (conversion is a birth, `variable.set:262`); plan test `AsPathIsABirth`.
- Delete `channel/type/file` and `channel/type/http`.
- At 9a's end: build and run its plan tests (CreateFiresOnBirth, AsPathIsABirth) and do the revert checks.

## 9b / 9c
- 9b: the other modules per the worklist, one module per commit.
- 9c: `module/action/<m>/<a>.cs` → `module/<m>/action/<a>.cs` (watch the list module's folder clash).
