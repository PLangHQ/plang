# docs summary — runtime2-channels

## Version
v1 — merge-gate docs pass on coder v10 + auditor v2 PASS.

## What this is

`runtime2-channels` rebuilt I/O around per-actor `Channels` registries with redirectable role-channels (`output`/`error`/`input`/`debug`). Coder v10 was the final pass: purging `Console.*` from production C# so every write actually flows through the redirectable surface. The branch is auditor-PASS; my job here was the merge gate — fix the auditor's one docs nit, capture the canonical Console.* rule v10 established, and write a CHANGELOG-equivalent.

## What was done

1. **Auditor N1 fix** — `Documentation/Runtime2/cool.md`: rewrote the "Channels that migrate across devices" entry. Was present-tense ("the channel.migrate action snapshots…"); coder v9 deleted that surface. Now reads clearly as roadmap with an explicit deletion note pointing forward to Stage 9.
2. **Console.\* discipline rule** — added long-form section "Console.* Is Banned in Production C#" to `Documentation/v0.2/good_to_know.md` (3 write flavours: `Debug.Write` for diagnostics, `Channels.WriteTextAsync("output",…)` for user-facing chatter, two-call pattern across the split `output`/`input` pair for interactive prompts; 2 exemptions: `IsInputRedirected`-style queries and `PlangConsole/Program.cs:26` last-resort sink; test-fixture pattern using memory channels). Added one-line bullet to `/CLAUDE.md` Runtime2 Conventions linking to it.
3. **CHANGELOG-equivalent** — `v1/result.md` summarises the user-visible deltas of the whole branch (per-actor Channels registry, role channels, Debug.Write surface, deletion of Channel.Migrate/MigrationEnvelope, Console.* purge).
4. **Flagged but not filled** — `Documentation/v0.2/io-channels.md` is materially stale (predates OBP restructure and per-actor split; names `App.IO.IO`, `actor.IO`, factory list out of date). Logged as `major / stale-doc / flagged-for-coder-or-architect`. Calling this branch merge-blocked on an unrelated stale doc is the wrong tradeoff — it's its own architect+docs cycle.

## Code example — the canonical Console.* purge pattern

```csharp
// Before — bypasses channels, ignores --debug routing and redirection
Console.WriteLine($"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s)");

// After — routed through the actor's output channel; redirectable,
// captured by tests via memory channels, free to be re-registered to a file
await app.CurrentActor.Channels.WriteTextAsync(
    global::App.Channels.@this.Output,
    $"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s){Environment.NewLine}");
```

For diagnostic chatter (lifecycle banners, `--debug` traces) the same shape but through `await context.App.Debug.Write(...)` — gated on `--debug`, falls back to `error` if no `debug` channel registered.

## Verdict

**PASS** — ready to merge.

No claude-md or character proposals on this branch to evaluate.
