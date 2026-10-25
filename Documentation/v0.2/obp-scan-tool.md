# ObpScan — the Roslyn util that makes an OBP scan an artifact, not an impression

Companion to `obp-scan.md`. The scan procedure fails when it's done by fluent reading — an impression gets reported instead of a per-member pass, and the smells with no syntactic fingerprint (misplaced behavior) and even the ones WITH a fingerprint (compound names, long methods) get glided past (worked example: `FirstConditionIndex`/`ComputeBranchChain`/`SplitAtConditions` on `actions.@this` were called "good name" and missed, 2026-07-17). This tool removes the fluent-skip: it emits the member table so every member arrives pre-listed with its mechanical flags and caller set, and the reader can only add the ownership verdict.

## Placement

New top-level dev-tool project: **`Tools/ObpScan/`** (`Tools/ObpScan/ObpScan.csproj`, `net10.0` console). NOT under `PLang.Generators/` — that's the shipped netstandard2.0 analyzer assembly; a console tool that loads a workspace doesn't belong there. `Tools/` is a new folder for dev utilities (nothing else needs to ship it).

- References: `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`, `Microsoft.Build.Locator`.
- Not in the main solution build path, **not a build gate, never runs on build.** **Runtime ~15s** (one MSBuild load + parallel per-member SymbolFinder) — no long timeout needed. On-demand ONLY — I run it when Ingi asks for an OBP scan: `dotnet run --project Tools/ObpScan -- <type-or-file>` → prints the table → done. Over a whole tree, the shape flags alone (no caller sets, ~40 s with the load): `dotnet run --project Tools/ObpScan -- --tree PLang/app` → one line per flagged member, `file:line  Type.Member  flags`, and the counts.

## What it computes (the three columns of the table)

Load the solution once (`MSBuildWorkspace.OpenSolutionAsync`) — needed because the caller column requires the whole compilation, not one file. Then for the target type(s):

1. **Enumerate members** — via the semantic model (`INamedTypeSymbol.GetMembers()`), so multi-line signatures, generic arity, and exact line spans are correct (my python/grep mis-measured facade members as "1 line"; Roslyn gets the real span from `Location.GetLineSpan()`).

2. **Mechanical flags — pure machine, no judgment** (this is the part that "can't be reassured into 'good name'"):
   - **Name:** tokenize the member name by camelCase → words; drop `Is/Has/Async`; flag if **>1 content word** OR any token is a **verb** (a small verb lexicon: Compute/Split/Get/Set/Run/Build/Make/Find/Resolve/Render/Validate/…). `FirstConditionIndex` → [First, Condition, Index] → flag. `ComputeBranchChain` → verb `Compute` → flag.
   - **Length:** method body line count > ~15 → flag.
   - **Shape:** what the signature shows about a collection, which a name can't. `RAW-COLLECTION`: a verb-named public method whose return (a Task's, unwrapped) is a raw `System.Collections.*` collection or an array; the collection should be the API, its own type. `GET-TWIN (X)`: a `GetX` beside a member `X`, one thing exposed twice. `MUTABLE-COLLECTION`: a public property or field typed `List`/`Dictionary`/`HashSet`/`SortedDictionary`/`Queue`/`Stack`, whose add and evict discipline lives in other files (public fields are scanned for this alone).

3. **Ownership data — machine computes, human judges:** for each member, `SymbolFinder.FindReferencesAsync(memberSymbol, solution)` → the set of **calling types + their namespaces**. Emit them. Auto-hint when **every caller's namespace ≠ the declaring type's namespace** (e.g. `SplitAtConditions` declared in `app.goal…actions`, called only from `app.module.action.condition`) — that's the misplaced-behavior signal. The tool flags the hint; the final "is that caller's *concept* different" call stays the reader's.

## Output — the artifact (the exact table format to keep)

Markdown to stdout, one row per member, no member omitted:

```
| Member | Lines | Name flag | Shape flag | Callers (namespaces) | Ownership hint |
|--------|-------|-----------|------------|----------------------|----------------|
| ComputeBranchChain | 19 | VERB+NOUN (Compute·BranchChain) | | condition, test.discover | callers ∉ declaring ns → MISPLACED |
| GetNames           | 4  | VERB+NOUN (Get·Names) | RAW-COLLECTION | (none/internal) | own |
| Add                | 1  | clean | | (internal) | own |
```

Plus a footer summary: "N members, M flagged (K name, S shape, L length, P misplaced-hint)" and the cluster call-out — **≥3 flagged members whose callers share one foreign namespace = a missing type** (the four condition methods → the orchestration type that should exist).

## The split this enforces (why it fixes the real failure)

- **Machine owns the fingerprints** it can't be talked out of: compound/verb+noun names, long methods, caller-set-vs-declaring-namespace. These are exactly the checks I have the rules for but skip when reading fluently.
- **I own only the ownership *judgment*** — "is this caller's concept genuinely a different concept?" — fed reliable caller data, with every flagged member on the table so none can be skipped.
