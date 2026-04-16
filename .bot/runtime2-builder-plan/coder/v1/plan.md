# Coder v1 Plan — Data.Compare + Eval Suite

## What we're building

Three things, in order:

1. **`Data.Compare(Data other)`** — C# method on Data that compares two Data objects by serializing both to JSON and diffing field-by-field. Returns a Data with diff results.

2. **38 `.goal` eval files** — One file per builder pattern, covering all Runtime2 modules. Each `.goal` file has a single goal named `Start` with 1-2 steps testing one specific builder mapping.

3. **Eval runner** — A PLang goal that builds each eval, compares the `.pr` output against `.golden` files, and writes dated results.

## Implementation Details

### 1. Data.Compare (`PLang/App/Data/this.Compare.cs`)

New partial file on `@this`. Approach: serialize both Data objects to JSON via `Json.CamelCaseIndented`, then parse both as `JsonElement` and walk the tree comparing fields.

Returns a `Data` with Value set to a comparison result object:
```csharp
{
    "match": true/false,
    "fields": {
        "module": { "match": true, "expected": "file", "actual": "file" },
        "action": { "match": true, "expected": "read", "actual": "read" },
        "parameters": { "match": false, "expected": [...], "actual": [...] }
    },
    "missingFields": [...],
    "extraFields": [...]
}
```

The comparison is structural — keys must match, values must match, arrays must match in order. This is a JSON-level diff, not a Data-level diff (we compare the serialized .pr format, not the in-memory object graph).

**Key decisions:**
- Use `JsonDocument.Parse` on both JSON strings, walk recursively
- Return match=true only if ALL fields match
- Case-insensitive property name matching (consistent with `Json.CaseInsensitiveRead`)
- Number comparison: compare as `decimal` to avoid int/long/double boxing issues
- Null == missing for optional .pr fields (intent, onError, cache can be null or absent)

### 2. Eval Goal Files (`Tests/Builder/Evals/`)

Each file follows this pattern:
```
Start
- <step text from eval catalogue>
```

That's it — one goal, one or two steps. The goal is named `Start` (PLang convention for entry points).

**File naming:** `{eval-name}.goal` matching the catalogue names (e.g., `variable-set-string.goal`, `file-read.goal`).

All 38 cases from the architect's catalogue. I'll write them all at once.

**Golden workflow:** After writing all `.goal` files, we build them with `plang build`, manually verify the `.pr` output, then copy verified `.pr` files to `.golden` files in the same directory.

### 3. Eval Runner (`Tests/Builder/Evals/RunEvals.goal`)

PLang goal that:
1. Lists all `.golden` files
2. For each, derives the `.pr` path and compares using `Data.Compare`
3. Collects results
4. Saves dated results JSON

This depends on Data.Compare being available at runtime.

## Files to create/modify

| File | Action | Description |
|------|--------|-------------|
| `PLang/App/Data/this.Compare.cs` | create | Compare method on Data |
| `PLang.Tests/App/Data/CompareTests.cs` | create | C# tests for Data.Compare |
| `Tests/Builder/Evals/*.goal` | create | 38 eval goal files |
| `Tests/Builder/Evals/RunEvals.goal` | create | Eval runner goal |

## What I will NOT do

- Formalization + return removal (Part 1 step 5 — later)
- Pipeline redesign with level/confidence (Part 1 step 6 — later)
- Modify the builder prompt
- Change any existing .pr files

## Order of work

1. Implement `Data.Compare` + C# tests
2. Write all 38 `.goal` files
3. Build them with `plang build`
4. Read and verify `.pr` output
5. Copy verified `.pr` to `.golden`
6. Write the eval runner goal
7. Summary + commit + push

## Questions / Blockers

None — the plan is clear from the architect's document.
