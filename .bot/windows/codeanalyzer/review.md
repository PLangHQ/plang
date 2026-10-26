# codeanalyzer — windows, review 1 (b4a549d1c → 1610440fc)

**Verdict: FAIL** — one finding, a comment that narrates a task. The coder is next; everything else holds.

Scope: 20 production files (read whole at the touched members), the generator emission, and the tests that pin them. Passes 1a/1b, 2, 3, 4, 4.5 and 5 run.

## F1 — a comment names the task (Pass 2)

`PLang.Tests/Runtime/App/FileSystem/PermissionTests/StoreFolderModeProbeTests.cs` (the class summary): "Security probe (doc2 trust slice) … RED until the store's Open sets the folder's mode." The doc2 slice and the red state are provenance; the code states what is. Rewrite it as the present fact: a saved key sits in plain text in the setting store, sqlite makes its files with the umask, so the store's folder must carry no group or other bits (not on Windows). The class name's `Probe` can go with it: it is a test of the store, not a probe.

## Passes

- **OBP 1a/1b:** no smell hit. The typed form's math is one owner (`path.file.Typed`, a private struct), not a helper beside `PathHelper`. `Level` is decided by each owner (goal, step, action), no check in `on.event`: the owner decides its own levels. `Under(folder)` is one private member, three facts read it. No new locked-surface member: `Address` and `Matches(goal)` are debug's own internals, `Messages` is `internal` on `query`, `IsSystem` on step is `internal`.
- **Pass 4 (silent breaks):**
  - The `[IsNotNull]` guard (`Emission/Action/this.cs:401–417`) reads a required variable once more through `Start`. A plain variable costs a lookup; **a variable with a method hop is evaluated twice** (`%items.random()%` would give the guard one pick and the action another). The guard only judges null, so no wrong answer, but a hop with an effect runs twice. The architect ruled the second read (decision 6). Not blocking; worth a line in the backlog if a method with an effect is ever written in a required slot.
  - `permission.PathMatches` exact now compares by `RootComparison`: on Windows an exact grant on a URL is case-insensitive in its path, while the glob is case-sensitive. Fails open by one letter case on a URL only; security reviewed the grant.
  - `Typed.Combine` is trimmed twice: `Derivation.Combine` trims the child, then `Typed.Combine` trims again. Harmless; one of them is enough.
  - Debug by address binds the named goal only: the system goals it calls are not shown (decision 13, Ingi agreed).
- **Pass 4.5:** the line-end and separator fixes are at the producers (`.gitattributes`, goal/`.pr` writer, stdin), not at each consumer. Several files for one cause, but the cause is one rule (a line ends at LF; the plang form uses `/`) and each file is a producer.
- **Pass 5 / mutations** (announced, reverted, tree clean):
  - action `Level` without the `IsSystem` line → `BeforeEachAction_FiresForTheAppsActions_NotASystemGoals` red.
  - step `Level` without `IsSystem` → `BeforeEachStep_FiresForTheAppsSteps_NotASystemGoals` red.
  - the store's mode `0700` → `0775` → `ASettingStoresFolder_HasNoGroupOrOtherBits` red.
  - Not provable here: the glob's separator, the exact grant's comparison, the LF line ends and the `Resolve` canonical compare all equal the old behaviour on Linux, so only the Windows host's run pins them (its stage 7 and rerun are green per the coder).

## Bench (windows-tip 149256d61; diff touches birth, request, tests; no store pool change, so no rest)

| Measure | windows | decider-key tip |
|---|---|---|
| Sub birth | 0.14 ms / 109 KB | 0.14 / 110 |
| Root birth | 18.93 ms / 2392 KB | 18.48 / 2488 |
| Sub request | 1.84 ms / 543 KB | 1.85 / 541 |
| Root request | 17.92 ms / 3839 KB | 16.56 / 3720 |
| `plang --test` | 4.5 s (298 pass, 1 fail) | 4.5 s |
| Sweep | 41.8 s, red only Wire | 40.6 s |

Held memory flat (1.6 → 2.1 → 2.1 → 2.1). All in band; the events' per-start `IsSystem` and the typed math cost nothing measurable.
