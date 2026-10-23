# item-doors — the docs pass before merge

Everything else in the line is done: codeanalyzer, tester, security (re-check 4 passes, 89650b2e5) and the auditor (the re-check of X1–X3 passes, 37f82042c). This pass is the last step before the merge into runtime2 (decision 316: "you can merge when doc and you are happy"). The decisions are in `.bot/item-doors/architect/summary.md`, newest first. Its "rule" column names the rule each decision set.

Docs say what is, in docs' own words. No history and no "previously".

## What to write

1. **What a permission prompt guards** (315d; security, 314a). Wherever the docs describe the permission prompts, the stores' folder or consent as protecting the user, say what they guard. A prompt guards what a goal does as the User actor. An app's own code can choose to run a goal as System (313e) or wire its own channels, an input that answers asks included (313f). So running an app means trusting its code. The prompts protect the user from what the User actor would do unasked, not from the app itself.
2. **An app folder's `.data/`** (317, Ingi: "if they give us zip with .data then that is ok, it is what it is"). One line: plang makes `.data/` on an app's first run, and an app folder that arrives with one is used as it is, its identity and its settings included.
3. **How the gate judges a link** (319b, 319c), in `Documentation/v0.2/filesystem-permission.md`:
   - An open (read, write, execute, list, stat) is judged where the OS lands for the path: each link followed, the last one too.
   - A delete, and a move's two ends, act on the entry itself (a link is removed or replaced, never what it leads to), so they are judged at the entry: its folder's landing and its own name.
   - A copy's ends follow the links.
   - A move's source is asked read and delete, and a yes keeps one grant on that place holding both.
   - A grant is on the place asked about, for the verbs asked there. A delete grant on a link never answers a read or write of its target.
   - A trailing separator names no other place (`dl/` is the entry `dl`).
   - Deleting a folder link removes the link alone.
4. **A signed value that arrives** (319a, 319c), wherever signing or verify is documented (`Documentation/v0.2/callbacks.md` and its neighbours). A replay is a wire arriving twice. The read that brings a signed value in presents its nonce once, after its hash and signature pass. The value it produced verifies, and names its signer (`%x!signature.identity%`), as many times as it is read, its copies too. The same bytes arriving again answer NonceReplay.
5. **H** (315d): bin to a tty is by design (decision 214). Give it one line on its setting.
6. **Stale names** (the auditor's N18, `.bot/item-doors/auditor/final.md:133–136`):
   - `Documentation/v0.2/bans.md:47-48`, `app-tree.md:63,74`, `filesystem-permission.md:46,71`, `debug.md:212,234`, `scripts/check-app-tree.sh:79`, `os.targets:2`.
   - `App.AbsolutePath` / `OsAbsolutePath` became `app.filesystem.root` / `app.filesystem.os` (314b); they navigate as `%!app.filesystem.root%` / `%!app.filesystem.os%`.
   - `--debug={…}` is refused now (`ExecutorTests.cs:300-307`); teach the form that works.
7. **CLAUDE.md**, through `.bot/item-doors/claude-md-proposals.md`. Review its six proposals and apply the ones that hold up. Add N18's two CLAUDE.md lines: `CLAUDE.md:11,27` (the App-anchor strings), and the Debugging section's `--debug={…}`.
8. **good_to_know**: carry over the rules from decisions 300–319 that a reader of the code needs and the docs don't hold yet. The gate's link rule (3) and the nonce rule (4) are the first two.

## Not in this pass

- `identity.export`'s teaching (N1): it goes with the critical-actions branch (307).
- N1–N17, R3/198 and everything in `.bot/item-doors/architect/backlog.md`: next-branch work.
- The snapshot's `[Sensitive]` rule is already in `test/plan/snapshot/start.md` (311).

## Rules

- A change to `os/system/modules/**/*.md` or `os/system/type/**/*.md` follows the teaching-edit rule (CLAUDE.md "Build"): re-run the decider fixtures and the page goldens, and read every diff before re-pinning. If one would be needed, tell the architect first.
- No production C# and no `.pr` edits.
- Commit and push, then report to the architect: what changed, file by file; what you left, and why; the hash.
