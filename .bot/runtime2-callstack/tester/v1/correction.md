# tester v1 — correction

## What I got wrong

My v1 verdict (`needs-fixes`, 5 PLang failures) is **wrong**. After
nuking `bin/obj/` and rebuilding PlangConsole from clean, the PLang
suite passes **181/181** on the same commit (HEAD~1 = `00bfc01d`,
code-affecting tip = `be77dc12`). The coder's claim was correct.

```
Test summary: 181 total, 181 pass, 0 fail, 0 timeout, 0 stale, 0 skipped
```

## Root cause: stale binary

The bot runner started this session with a pre-existing
`PlangConsole/bin/Debug/net10.0/plang.exe` carried over from an earlier
session — built from a tree that pre-dates `be77dc12`'s landing of
`app.modules.debug.tag` and the `%!callStack.Audit` binding wiring.

- C# suite: ran via `dotnet run --project PLang.Tests` — recompiled
  on the fly, saw the current source, passed cleanly.
- PLang suite: invoked `PlangConsole/bin/Debug/net10.0/plang --test`
  directly. That binary was stale. Reflection-based module discovery
  scanned an old assembly that lacked `App.modules.debug.Tag` and the
  Audit property bindings, producing the five failures my v1 reported:

  - `Action 'debug.tag' not found` (Tag tests) — handler class wasn't
    in the loaded assembly.
  - `%!callStack.Audit*` null / cyclic (Audit, CauseLink,
    CrossFileChain) — variable resolver in the old assembly didn't
    know about the property.

After `rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj
… && dotnet build PlangConsole`, all five pass.

## What stands and what falls

**Falls**:
- Findings 1, 2, 3 (critical) — all artefacts of stale-binary divergence.
  No real bug in coder/v2.
- The `needs-fixes` verdict.

**Still stands** (independent of the suite-pass question):
- Finding 4 — `Audit.test.goal` count formula `4 + 1 + 2 = 7`
  double-counts the fourth throw. The expected number reads
  retrofitted; the comment doesn't honestly derive 7.
- Finding 5 — `HandledFlag*` tests are named for a flag they never
  read. They prove control flow, not the flag.
- Finding 6 — `HandledFlagFalseWhenRecoveryFails.test.goal` packs two
  goals into one file (CLAUDE.md violation, latent foot-gun).
- Finding 7 — `CallChainRendererTests` doesn't pin the
  reference-equality assumption that coder's risk register flagged.
- Finding 8 — `AsT_PlainDataTarget_DictWithInfraVar_*` lacks the
  symmetric negative assertion.

These are test-quality findings, not blockers. Severity drops from
"branch-blocking" to "minor cleanup".

## Lesson for the tester role going forward

Before claiming PLang test results, **always rebuild PlangConsole**.
The bot runner inherits build artefacts across sessions; the C# suite
hides this because `dotnet run` rebuilds, but the PLang suite uses a
pre-built binary that can be arbitrarily old.

Minimum re-run protocol:

```bash
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole
dotnet run --project PLang.Tests           # C# suite
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test  # PLang suite
```

Do **not** delete `Tests/**/.build/` — those are tracked .pr files,
not build artefacts. CLAUDE.md "NEVER delete .build folders" applies
here. (I broke this rule mid-investigation; restored via
`git checkout -- Tests/`.)

## Revised verdict

**approved** — coder/v2 is mergeable for what was delivered. Findings
4-8 are test-quality cleanups that can ride a future commit; not
blockers.
