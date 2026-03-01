# Auditor v7 Summary — DiscoverAsync Review

## What this is

Re-review of coder v6 fix: replacing eager `LoadFromDirectoryAsync` with scoped `Setup.DiscoverAsync`.

## What was done

Verified the fix addresses the finding:

- `DiscoverAsync` scans `*.pr` recursively, parses each, only adds `IsSetup == true` goals to the collection
- Non-setup goals are discarded — they remain lazy-loadable via `GetAsync`
- `Executor.Run2` calls `Setup.DiscoverAsync` instead of `LoadFromDirectoryAsync`
- OBP rule 1: Setup owns its own discovery behavior
- Inner bare `catch` skips unparseable files — they'll produce proper errors when lazy-loaded later

3 tests verify: only setup goals loaded, non-setup goals remain lazy-loadable, empty directory handled.

1493 C# tests pass. No findings.

## PLang integration test recommendation

The setup system is now ready for end-to-end PLang testing. `plang p build` produces v0.2 `.pr` files with `isSetup` set by the builder. `plang p Start` goes through `Run2` which calls `DiscoverAsync` then `Setup.RunAsync`.

Suggested test: create a Setup.goal that inserts a row, a Start.goal that counts rows. Build with `plang p build`, run `plang p Start` twice, verify the setup step only executed once (count stays at 1).

## Verdict: PASS
