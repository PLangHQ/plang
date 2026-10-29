# snapshot — coder v4 — stage 7b: what finds it (82, 83, 86, 80, 88)

Pattern per change, as Ingi asks.

1. **The one door** (a view decides, no value reads it): `item.Output(writer) => writer.View.Output(this, writer)`;
   the default View answers `writer.Write(value)`, so Store, Out and Debug are unchanged. `item.Members(writer)` is
   the tagged walk the default `Write` was, callable by a view whatever a value's own look.
2. **A tag like [Store] and [Out]**: `[Snapshot]` marks what a value is found by. View.Snapshot: a type that tags any
   writes those members alone (`Members`), whatever its own write; a type that tags none shows [Store] minus
   [Sensitive], as now.
3. **The tags**: goal Name, Pr, Hash; step Index; action Index (its place in its step's code), Module (its module
   writes its Name), Name; content's Path (file, url).
4. **A birth fact set where it's made** (86 d): `goal.Pr`, its .pr as a `file`, set by the five makers that set Path
   (parser ×2, the .pr reader, test.list ×2), `internal set`; a sub-goal answers its parent's. Path's setter stops
   working out `_pr`. PrPath goes; its uses read `goal.Pr.Path`.
5. **Each type owns its birth** (80 a): file, url and directory each make an unread reference from their key record
   (`{path}`) by their own Create.
6. **Selected by a key, the key a record** (49): a goal's Select takes `{name, pr, hash}`: the .pr read, the goal or
   sub-goal named, the hash checked.

Tests, red first: a goal in a variable comes back as the live goal; a file read before the save comes back unread and
reads the disk again; the wire goldens unchanged; a goal's .pr unmoved; surface re-pinned (83, 86).
