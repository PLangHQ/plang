# docs — fast-apps v1 — result

## CHANGELOG (fast-apps, developer-facing)
- **An app is made from an origin** (the code, or the actor that calls); its type list, module registry and goals are layers over it. A sub app is born in about 0.15 ms and 110 KB (root app: about 19 ms, 2.4 MB).
- **A sub app is made in its maker's folder or under it,** never in its maker's stores folder; a sub app of a testing app tests from birth.
- **A binding made while a program runs is held on its actor's context** (`actor.on`); a type takes no history. A run never writes an object its app shares.
- **A birth makes only what's asked;** each part is made at its first use.
- **`plang --test` takes about 5 s** (was 24–31 s); the C# build drops 56 unused packages and `Sdk.Web`.
- **`Tools/Bench/run.sh`** records the speed numbers; a number outside its band at a review is a finding.

## File by file
| File | Change |
|---|---|
| `Documentation/v0.2/apps.md` | new: origin, folder and testing rules, the hold, shared writes, a birth that makes what's asked, Tools/Bench, packages/SDK, the rebase trap |
| `Documentation/v0.2/good_to_know.md` | topic doc and six section lines |
| `Documentation/v0.2/architecture.md` | the removed `CreateChild()`/`Clone()` replaced by `context.Child(runner, token)` |
| `CLAUDE.md` | "Branch rules" (boy scout, no runtime code for tests, a run never writes a shared object) and a "Speed record" section |
| `test/plan/fast-apps/start.md` | origin instead of parent; the folder rule; the layers; stage 6; deferred stage-4 items |
