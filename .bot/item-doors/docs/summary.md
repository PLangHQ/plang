# docs — item-doors

**Version:** v6

## What this is

The docs pass before item-doors merges into runtime2. The branch changed how the permission gate judges a path, how a signed value that arrives is verified, what a command-line flag is, and how the app names its folders. The docs said the old things, or nothing. This pass writes what is, against the code and decisions 300–320.

## What was done

All in `Documentation/v0.2/` unless noted. No production C#, no `.pr`, no `os/system` teaching markdown (so no re-pin).

- **`filesystem-permission.md`:** what the prompts guard (the User actor's doing, not the app's own code; consent comes from the actor's input only); the `.data/` line; "Where a verb is judged": an open at `Location`, a delete and a move's two ends at `Spot`, a move's source read and delete, a grant on the place asked, trailing separator, folder-link delete, a file as a tree of one, ask once check all then do.
- **`callbacks.md`** (+ a pointer in `wire-serialization.md`): a signed value that arrives: one nonce presentation per arrival, after hash and signature; it verifies and names its signer any number of times; the same bytes again answer `NonceReplay`.
- **`io-channels.md`:** one line on `%!app.channel.setting.format%` and `bin`.
- **`bans.md`:** anchors are paths; a new "Own settings and identity keys" section.
- **N18 stale names:** `app-tree.md`, `debug.md`, `cli-changes.md`, `check-app-tree.sh`, `os.targets`, and `CLAUDE.md` (anchors, Debugging).
- **`CLAUDE.md`:** proposals 1, 3, 4, 5, 6 applied; 2 rejected (withdrawn by the next entry).
- **`good_to_know.md`:** index lines for all of the above.
- **Reports:** `v6/plan.md`, `v6/result.md` (CHANGELOG), `v6/verdict.json` (pass), `../docs-report.json`.

## Not done, and why

- `identity.export`'s teaching (N1) and everything in `backlog.md`: not in this pass.
- `app-tree.md` drift beyond N18 (ten modules, `IsLeaf`, …) and the older sections of `filesystem-permission.md`: separate passes; flagged in the report.
- `Data.Merge()` in `CLAUDE.md`: fixed on `docs/file-module-guide`, not here.

## Code example

The pattern of the gate section: the door names the place it acts on, the gate judges that place.

```
open (read, write, execute, list, stat)  → Location  each link followed, the last too
delete; a move's two ends                → Spot      its folder's landing + its own name, never followed
a copy's two ends                        → Location
```
