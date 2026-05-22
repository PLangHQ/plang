# Coder plan — filesystem-permission

This branch adds PLang's filesystem permission system. It also unifies suspend/resume across the runtime (the work formerly done by `AskCallback` and `ErrorCallback`). Five stages, ordered below.

## Read first

1. **`summary.md`** — cross-cutting design decisions (Permission shape, Snapshot-resume mechanism, action-owns-execution, storage, FS surface). 5-minute read.
2. **`Documentation/v0.2/architecture.md`** and **`Documentation/v0.2/good_to_know.md`** — they're auto-loaded into CLAUDE memory but worth a fresh read before starting.
3. The stage file you're about to touch.

## Where to start

**Stage 1 first.** Pure types, fully self-contained, no engine integration. Lands clean and unblocks stages 2a, 2b, and 3. Half a day's work.

After stage 1, three streams can run in parallel:
- **Stage 2a** (engine round-trip) — independent of permission types; the biggest stage.
- **Stage 3** (storage view) — needs stage 1's types but no other dependency.
- *(Stage 2b is blocked on 2a's `Type.Exit()` and `Snapshot.Resume`. Tests can mock the surface in advance.)*

Then:
- **Stage 2b** — once stage 2a lands; small, ~1 file's worth of code.
- **Stage 4** — once 2b and 3 land; biggest by *volume* (~50–100 call sites), but mechanical.
- **Stage 5** — once 1–4 land; integration cut, no new C# code beyond the test fixture.

## Stage-by-stage

| Stage | Owns | Depends on | Approx size |
|-------|------|------------|-------------|
| 1 | Pure types (`Permission`, `Verb`, `Match`, `Covers`) | — | Small |
| 2a | Engine: `IExitsGoal`, `Type.Exit()`, `Snapshot.Resume`, `Goal.RunFrom`, `Channel.Ask`, action-owns-execution, `ShouldExit`. Drops `ICallback`/`AskCallback`/`ErrorCallback`/`App.Run`/`App.RunAction`/`cause` parameter | Existing `Snapshot`, `CallStack`, `Channels.Stream` | Large (~10 deliverables) |
| 2b | `Path.Authorize(verb)`; `PermissionDenied` error; expiry constant | Stage 1, Stage 2a | Small |
| 3 | `Actor.@this.Permission` view (`Find`/`Add`/`Revoke`) | Stage 1, existing `App.SettingsStore` | Medium |
| 4 | `IPLangFileSystem` v2 (Path in, Data out, verb baked); drops `ValidatePath(string)`, `FileAccessControl`, `IFileSystem` inheritance | Stage 2b, Stage 3 | Medium-large (~50–100 call sites, mechanical) |
| 5 | End-to-end fixture under `Tests/Permission/` proving the full flow + restart round-trip | Stages 1–4 | Small |

## Conventions to know

- **OBP folder layout:** singular folder name, `@this` is the type. (`Permission/this.cs` = the `Permission` record. `Permission/Verb/this.cs` = the `Verb` container.) See `CLAUDE.md` "OBP Shape Smells."
- **`Data` is self-owning.** No wrapper-with-contents framing; no "Envelope" abstraction. Data carries its own Signature + (new in this branch) Snapshot.
- **No `Console.*` writes in production C#.** Channels are the output path. See `CLAUDE.md` "Console.* Is Banned in Production C#."
- **PLang type names lowercase.** `Ask` (CLR) → `"ask"` (PLang Type.Value). The Type-extension predicates resolve through `App.Types.Clr(name)` — see stage 2a #1.

## Build / test

Always rebuild from clean before claiming a `plang --test` result — stale `PlangConsole/bin` binaries can produce phantom failures. See `CLAUDE.md` "Stale-binary trap."

```bash
# C# tests (recompiles in place)
dotnet run --project PLang.Tests

# PLang tests — rebuild first
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

## Followups baked into the design

These are intentional v1 limitations, tracked in `Documentation/Runtime2/todos.md`:

- `!ask.answer` sentinel-via-Variables → explicit `Answer` parameter when `output.ask` grows structured options.
- `output.ask` structured options → refactor `Path.Authorize` to skip the `BuildRequest`/`SignAndStore` reconstruction.
- `App.Snapshot()` orchestration → relocate to `Snapshot.@this.Capture(ctx)` static factory.
- Per-channel serializer for stateless suspend (error vs ask wire shapes).
- Revisit `Snapshot.ResumeChain` — works but clunky.

Don't sweep these into this branch.

## When you finish a stage

- Run both test suites (C# and PLang `--test`).
- Update `Documentation/Runtime2/good_to_know.md` if you hit anything an architect needed to know that wasn't in the stage file.
- Commit per natural boundary, push.
- For stage 4 specifically: follow the sub-staging in `stage-4-filesystem-surface.md` — each sub-stage compiles and tests green; this gives bisectability across 50+ call-site touches.
