# docs — item-doors v6 — result

## CHANGELOG (user-visible, item-doors)

- **A command-line flag is a setting's path.** `--<path>=<value>` sets `%!<path>%`, `app.` optional: `--debug.setting.goal=Start`, `--channel.setting.format=plang`, `--build.setting.cache=skip`, `--test.setting.parallel=1`. Several options at once are a dict at the setting's path. `--debug={…}`, `--callstack={…}` and the other old forms are refused, naming the path to write. A setting flag alone switches its mode on.
- **The app's anchors are paths:** `%!app.filesystem.root%` (the app's folder, what `/` is) and `%!app.filesystem.os%` (what ships with plang). `App.AbsolutePath`, `OsDirectory` and `OsAbsolutePath` are gone.
- **The permission gate judges where the OS acts.** An open follows every link; a delete and a move's two ends act on the entry itself; a move's source is asked read and delete; a grant is on the place asked about; a trailing separator names no other place; deleting a folder link removes the link alone.
- **A signed value that arrives verifies and names its signer any number of times;** the same bytes arriving again answer `NonceReplay`.
- **Consent comes from the actor's input channel only,** never from a variable or an event. Running an app means trusting its code: the prompts guard what the User actor would do unasked.
- **An app folder that arrives with its own `.data/` is used as it is.**
- **`[Own]` settings take no run value;** an unsigned `[Own]` row is refused; an identity's private key leaves only through `export`, which asks every time.
- **The console's `bin` format** (a value's own bytes, raw) is chosen by the user's own flag or row.

## File by file

| File | Change |
|---|---|
| `Documentation/v0.2/filesystem-permission.md` | new "What the prompts guard" (315d); Authorize flow rewritten to the place a door hands the gate; in-root section on `app.filesystem.root`/`.os`; "The stores' folder — `.data/`" (317); new "Where a verb is judged" (319b/c) with the transfer, link and per-entry rules (303, 304, 306, 308); file map points at the real gate files |
| `Documentation/v0.2/callbacks.md` | new "A signed value that arrives" (319a/c); the line saying a read does not verify now says what the read does |
| `Documentation/v0.2/wire-serialization.md` | the read's verify step names the one nonce presentation per arrival |
| `Documentation/v0.2/io-channels.md` | one line on `%!app.channel.setting.format%` and `bin` (315d/214) |
| `Documentation/v0.2/bans.md` | anchors are paths (N18); new "Own settings and identity keys" (300, 306, 310, 312); three rows in the limits table (regex, kept list, archive unpack link) |
| `Documentation/v0.2/app-tree.md` | `AbsolutePath, OsDirectory` line removed; `filesystem` line states root/os/store (N18) |
| `Documentation/v0.2/debug.md` | the flag forms that work; `--callstack` heading and closing line → `--call.setting` (N18) |
| `Documentation/v0.2/cli-changes.md` | the flag rule stated as it is; old forms in two bullets replaced; `OsDirectory` dropped (added to N18's list: same stale class) |
| `Documentation/v0.2/scripts/check-app-tree.sh` | the dead `OsAbsolutePath` skip removed (N18) |
| `os.targets` | comment names `app.filesystem.os` (N18) |
| `Documentation/v0.2/good_to_know.md` | topic docs + 8 section lines for the rules above |
| `CLAUDE.md` | proposals 1, 3, 4, 5, 6 applied; N18's two lines (anchors, Debugging) |
