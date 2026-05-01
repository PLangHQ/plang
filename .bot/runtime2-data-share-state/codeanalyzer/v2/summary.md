# codeanalyzer v2 — runtime2-data-share-state

## What this is

Re-review of coder/v1's review-response commit `60b8d1f3 coder v1 review:
address codeanalyzer/v1 findings`. Verifies the four v1 findings are
fixed and runs five passes on the new code itself for fresh issues.

This is NOT a re-review of the whole branch — that was v1.

## What was done

- Read all five touched files post-fix.
- Confirmed each of the four v1 findings closed: `Data.cs:471` collapsed
  return; `Data.cs:644` IEnumerable transient gone; `Variables.cs:71`
  type-mutation deleted; `Data.SnapshotClone(object)` extracted as the
  single helper, used by all three call sites.
- Ran Pass 1–5 on the diff itself.
- Pass 4 (Behavioral) caught one quiet behavior unification — see below.

### v2 verdict: **CLEAN — pass**

All five files clean. Two cosmetic carryovers from v1 sub-findings still
present (defensive `??` in `set.cs:117–118`; redundant `global::`
qualification in 3 callsites) — non-blocking.

### Files

- `PLang/App/Data/this.cs` — CLEAN
- `PLang/App/Utils/Json.cs` — CLEAN
- `PLang/App/Variables/this.cs` — CLEAN
- `PLang/App/modules/list/add.cs` — CLEAN (1 cosmetic)
- `PLang/App/modules/variable/set.cs` — CLEAN (2 cosmetic)

## Code example — the behavior unification

The OLD diff at `PLang/App/Variables/this.cs:150–172`:

```csharp
// OLD — Variables.cs dot-path:
var jsonOpts = new JsonSerializerOptions {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
};
var json = JsonSerializer.Serialize(rawValue, jsonOpts);
rawValue = JsonSerializer.Deserialize<object?>(json, jsonOpts);
// rawValue is now a JsonElement graph, NOT a Dictionary/List.
```

vs. the NEW one-liner:

```csharp
// NEW — same site, calling the extracted helper:
rawValue = Data.@this.SnapshotClone(rawValue!);
// rawValue is now a Dictionary<string, object?> / List<object?> graph
// because Data.SnapshotClone routes through UnwrapJsonElement.
```

The same delta applies at `modules/list/add.cs`. Only `modules/variable/set.cs`
already had `UnwrapJsonElement` in its private helper — so the
extraction *unified* the divergent behavior under the well-formed shape.
The commit message frames this as pure dedup; in reality, it's dedup +
behavior unification. The new behavior is the right one (no JsonElement
leaks downstream), tests pass, no fix requested — just flagged in
`result.md` so future readers know the commit did more than its message
implies.

## Suggested next step

**tester** — to confirm:
1. `list.add` snapshot tests cover the now-Dictionary list-entry shape.
2. `Variables.Set` dot-path tests cover the now-Dictionary value handed
   to `SetValueOnObject`.
3. The 9 stub C# tests remain properly stubbed (deferred Phases 5b/5c/6).

If tester passes → ready for auditor and merge.
