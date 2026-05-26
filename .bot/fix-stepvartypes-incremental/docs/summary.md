# docs — fix-stepvartypes-incremental

**Version:** v1

## What this is

Final merge-gate docs pass on a branch where auditor v1, codeanalyzer v3, tester v6, and security v1 all returned PASS. The coder and tester had already written the substantive documentation (build_process.md rewritten for the planner/compiler split, new per-action `notes/examples/description.md` under `os/system/modules/`, XML docs on new tester types). My job was the proposal merge gate: codeanalyzer filed two CLAUDE.md proposals (new OBP smells) and two character self-proposals (Pass 1b dereference, new Pass 4.5).

## What was done

All four proposals applied — each had real branch evidence and was well-scoped.

**CLAUDE.md (`/workspace/plang/CLAUDE.md`):**
- Added OBP Shape Smell #5: *Producer hands back raw; consumers transform identically.*
- Added OBP Shape Smell #6: *Holds a reference AND a flat copy of properties reachable through it.*

**`Documentation/v0.2/good_to_know.md`:**
- Mirrored both smells (as items 6 and 7 in the existing 5-item checklist, since that doc already includes a 5th item "Helper-soup" not in CLAUDE.md). Each with a worked example from this branch.

**`characters/codeanalyzer/character.md`:**
- Pass 1b: replaced inline 4-item list with a CLAUDE.md pointer + "re-read at start of each Pass 1b" instruction — so codeanalyzer doesn't silently miss the new smells.
- Pass 4.5 (new, between Pass 4 and Pass 5): root-cause-vs-symptom 14-tell checklist with structured report shape.

No other documentation gaps. The coder + tester closed the loop on every code change:
- `PLang/app/tester/Timing.cs`, `Timings.cs` (new) — full XML docs.
- `PLang/app/tester/File.cs`, `Run.cs` — XML docs updated for the slim and the `Output`/`Timings` additions.
- `Documentation/v0.2/build_process.md` — fully rewritten for the new planner/compiler split.
- `Documentation/v0.2/building-the-builder.md`, `building_plang_tests.md` — references updated to the new file layout.
- `os/system/modules/**/{notes,examples,description}.md` — per-action LLM teaching added (condition/if, list/add, variable/set, output/write, channel/set, error/handle, event/on).

No CHANGELOG file exists in this repo; per-version `result.md` files under `.bot/<branch>/<bot>/v<N>/` carry the user-visible-change story for each step of the pipeline.

## Code example

OBP smell #6 in action — the actual collapse on this branch (`PLang/app/tester/File.cs`):

```csharp
// before — Goal reference AND 5 flat copies of properties reachable through it
public sealed class File {
    public string Path { get; init; } = "";
    public string PrPath { get; init; } = "";
    public string EntryGoalName { get; init; } = "";
    public string Directory { get; init; } = "";
    public Goal? Goal { get; init; }
    public string? GoalHash { get; init; }
    public string? BuilderVersion { get; init; }
    ...
}

// after — Goal owns identity; only discovery-only state stays flat
public sealed class File {
    public required Goal Goal { get; init; }
    public Status Status { get; set; } = Status.Ready;
    public string? StatusReason { get; set; }
    public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
}
```

The smell heuristic: read every scalar property on a class that has a reference field, and ask "is this `Foo.X`?" — if yes for three or more, the flat fields are the smell.

## Verdict

**PASS** — ready to merge.
