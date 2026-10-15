# App Tree — retired

This high-level snapshot of the `app` object graph is **retired**. It predated
the lowercase-member rename and no longer matches the code.

The current, authoritative app tree lives inside the code, as `start.md` next to
each concept's `this.cs`:

- **Start here:** [`PLang/app/start.md`](../../PLang/app/start.md) — the one
  root, path = namespace = plang name, the `/this.cs` pattern, the
  collection-node pattern (`app.X` / `.list` / `.current`), and the full member
  table of what `app` holds.
- Each concept's own `start.md` under [`PLang/app/`](../../PLang/app/): `module`,
  `type`, `goal`, `data`, `channel`, `event`, `actor`, `call`, `error`, and the
  rest.

The historical content of this file is in `git` history.
