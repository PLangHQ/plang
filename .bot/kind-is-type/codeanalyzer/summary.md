# codeanalyzer — kind-is-type

**Version:** v3 (latest). v1 = stages 0–3 + 767 + 789/790 (PASS, `82d8866ae`); v2 = stages 4–6 + 814 + stage-5 C# + security F1 (PASS, `560fc0a80`); **v3 = the render fix + the tester's F1/F2 test pins (PASS with one carried finding).**

## v3 (latest round)

Scope: only what landed after v2 — the coder's render fix (`2e83d21ff`, decision 849; `ui/code/Fluid.cs:165`) and `d03051109`'s test pins for tester v1's F1/F2.

**Verdict: PASS with one carried finding. Next bot: tester.**

- **F3.v3 (carried, not blocking):** the render fix decides "rests as a structure vs plain text" by `format is { Name: "item" }` — an *"is X? by name"* / symptom-patch (Pass 4.5). The question should be a virtual the format/kind answers; there's no clean existing predicate (`IsText` is `true` for json, so it can't distinguish). Same root as the deferred #3 floor (the text-format birth path); the clean fix is a kind virtual = a locked-surface proposal, converging with branch A's one-make-door. Behaviour is correct and verified; recorded, not downgraded.
- **The F1/F2 test pins are both correct:** F1 (`FileHandlerTests.Read_AsText`) now asserts the declared type + `Peek()` **before** materializing (distinguishes a landed text from a file reference; red under M5a); F2 (`KindFollowUpTests`, new) drives `pick.list` with canned answers and asserts `Pick.Formal` holds the kinded `number<decimal>` (red under a no-op `Refine`, M8). Neither asserts a workaround.

Build clean (0 errors, PLNG001/002 at 0); no locked-core shape touched beyond the render call site, so the v1/v2 verdicts stand.

---


## What this is

The OBP review of branch `kind-is-type` (decision 774, *a kind is a type*) before it carries into runtime2. The branch reshapes the locked core so a kind is a type under its parent (`kind<T> : kind : type`), a format type owns its bytes (json rests until a use opens it), settings read themselves back from their stored record, and a goal call's name can't climb out of its folder. Reviewed in two rounds: **v1** stages 0–3 (+767, 789/790); **v2** stages 4–6, decision 814, the stage-5 builder C#, and security's F1.

## What was done (v2)

Reviewed in the codeanalyzer's passes against HEAD `aa353656b`: every touched core type read in full, callers walked, a forced build, and a compiler-backed CS0108 hide sweep. `v2/report.md` is the artifact of record; `v2/verdict.json` the one-liner.

**Verdict: CLEAN — PASS. No new findings. Next bot: tester.**

The stage-4–6 shape work is OBP-exemplary:
- **json rests, a use opens — one door.** `data.@this.Open()` (`data/this.cs:322`) is the single parse-once door (parse → `history.Add(source)` → narrow; a bad parse answers `Fail(Unread)` with a narrow catch, never thrown to a courier). All 7 use-sites call it; none re-parses. `source.cs` keeps a resting kind unread and owns its own re-typing (`Declared`); its `Parse` uses the parent-scoped `Kind(type,ctx)`.
- **The navigator is gone** with no value-nav residue; the json kind's surviving `Read` bridge is kept-by-design (reached via `clr.cs:131`), its `JsonElement` the carried host-kind machinery (673).
- **`OwnInternal` (814/831)** follows the `Own*` pattern, terminating on the family's stored field (loop-free), on a decision-approved locked surface; host kinds override it `true`.
- **file.read's Type slot** returns the `Type!` carrier whole and passes raw to `declared.Create` (the birth-door contract) — no opened box.
- **F4.2** done (`ToString` is `name<kind>` for all kinded types); **render** asks the value's own kind, no format switch; **security F1** refuses a non-bare `.pr` `file` loudly.
- **My v1 F1–F3 are all fixed** (dead `TypeDescription` const gone; stale `BuildTypeEntries`/`Promote` doc gone; `subclass.Offers` binds `Offer` once at construction).

Deterministic backing: `dotnet build PLang -t:Rebuild` → **0 errors**, PLNG001/002 clean; of 90 pre-existing `CS0108` hides, **none** is in the stage-4–6-touched core files — **no new hide** (the 776 discipline holds).

### Findings (v2)
**None.** Deferred and confirmed on branch A's list (not re-raised): F8.1 (the action-slot name checks), the `source.cs:115` text-face floor. Carried baseline (not this branch): the 6 Wire snapshot tests, `After_EachRetry…` timing.

## Code example — the one door done right (the v2 crux)

```csharp
// data/this.cs — a USE opens a resting source once; the courier never calls this.
internal async ValueTask<item.@this> Open()
{
    var value = await Value();
    if (!Success || _context is null || value is not item.source rest) return value;
    try {
        if (rest.Parse(_context) is not { } parsed) return value;
        parsed.history.Add(rest);            // the source rides as history
        if (rest.Cacheable) _item = parsed;  // parse-once (787)
        return parsed;
    }
    catch (Exception ex) when (ex is JsonException or FormatException or ...) {
        Fail(rest.Unread(ex, this));         // failure is an answer, never a throw to a courier
        return value;
    }
}
```
Every use-site (navigation, enumeration, compare, typed ask, `is`) reaches this one door — the "if removing one line of choreography takes three files, those files are one missing type" smell is absent: the open logic lives once.

## Environment

Isolated worktree `/workspace/plang-kind-is-type` on `kind-is-type`; the shared `/workspace/plang` checkout must not have its branch switched. `Tools/ObpScan`'s MSBuildWorkspace can't load `PLang.csproj` here (NuGet prune/vuln warnings) — the member sweep was full-read + grep + the real compiler.

## Next

PASS → **tester**. Nothing routes to the coder. Both review rounds (v1 stages 0–3, v2 stages 4–6) are clean; the branch is merge-safe on behaviour. The two deferred items (F8.1, the floor) are branch A's "one make door" work, recorded there.
