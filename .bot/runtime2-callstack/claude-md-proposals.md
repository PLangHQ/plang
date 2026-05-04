## tester — v1 — 2026-05-04
**Target:** /CLAUDE.md
**Why:** Tester v1 filed a wrong `needs-fixes` verdict on this branch — 5 phantom PLang failures (`Action 'debug.tag' not found`, `%!callStack.Audit` null/cyclic) that vanished after rebuilding PlangConsole from a clean `bin/obj`. Root cause: bot runners inherit a pre-built `PlangConsole/bin/Debug/net10.0/plang` across sessions; reflection-based module discovery scans the loaded assembly, so a stale binary produces phantom failures for symbols that already exist in source. The C# suite is immune (`dotnet run` recompiles in-place); only `plang --test` is exposed. Affects every bot that runs the PLang test suite, not just tester. User explicitly approved adding this to `/CLAUDE.md` after we tracked down the env-divergence between coder's 181/181 and tester's 176/181.

**Proposed change:** Append to the existing "## Running plang Tests" section in `/CLAUDE.md`, after the existing bullets:

```markdown
### Stale-binary trap

`plang --test` uses `PlangConsole/bin/Debug/net10.0/plang` — a pre-built
executable, **not recompiled per session**. Bot runners inherit this binary
across sessions. Phantom failures with shapes like `Action '<module>.<action>' not found`
or `(null)` reads of `%!<infra>%` properties — for symbols that exist in
source on the current commit — mean a stale binary scanned via reflection,
not a real bug.

Before claiming any PLang test result, rebuild from clean:

```bash
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

The C# suite is immune (`dotnet run --project PLang.Tests` recompiles
in-place). Only `plang --test` is exposed to the trap.

Do **not** delete `Tests/**/.build/` — those are tracked `.pr` files, not
build artefacts. The "NEVER delete .build folders" rule above applies.
```

---

*Note: per the reviewer-bot rule in /CLAUDE.md ("Reviewer bots … do NOT propose CLAUDE.md changes"), this entry is filed at the user's explicit request after a real incident on this branch, not at the tester's discretion.*
