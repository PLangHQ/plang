# PLang App Architecture — retired

This overview is **retired**. It described an earlier shape of the runtime and
no longer matches the code.

The current architecture reference lives **inside the code tree**, as `start.md`
next to each concept's `this.cs` (path = namespace = doc location):

- **Start here:** [`PLang/app/start.md`](../../PLang/app/start.md) — the app
  root, how a file path maps to a namespace and a plang name, the `/this.cs`
  pattern, the collection-node pattern (`app.X` / `.list` / `.current`), how
  `item.list` works, and where to find any source.
- **The subsystems**, each in its own folder's `start.md` under
  [`PLang/app/`](../../PLang/app/): `module` (+ `module/file`), `type`
  (+ `type/item`, + `type/item/variable`), `goal`, `data`, `channel`, `event`,
  `actor`, `call`, `error`, and the smaller concepts (`setting`, `store`,
  `snapshot`, `task`, `test`, `keepalive`, `service`, `warning`, `parent`,
  `shortcut`).

For cross-cutting runtime notes and gotchas, see
[`good_to_know.md`](good_to_know.md). The historical content of this file is in
`git` history.
