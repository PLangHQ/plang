# Security Audit v1 — data-envelope-architecture

## What this is

Security analysis (blue team + red team) of the data-envelope-architecture branch. This branch adds a Data envelope pipeline (Wrap/Compress/Encrypt chain), partial class restructuring of Data, Engine.Types type registry, ValueNavigators for path navigation, and Variables updates. The audit focuses on how untrusted data enters and flows through these new components.

## What was done

Read all 10 changed production files, 3 test files, all 9 required architecture docs, and the existing auditor/tester/coder reports. Mapped the attack surface, then constructed exploit scenarios for each vector.

**12 findings total: 4 high, 6 medium, 2 low.**

### Key findings:

1. **Unbounded recursion (HIGH x3)** — UnwrapJsonElement, RehydrateNestedData, and GetChild all recurse without depth limits. Each can be triggered with crafted input to cause StackOverflowException (unrecoverable crash). RehydrateNestedData is the worst because it sits at the transport boundary processing decompressed untrusted data.

2. **System variable injection (HIGH)** — Variables.Set() has no guard against overwriting `!`-prefixed system variables (!engine, !fileSystem, !context). A crafted .pr file can nullify or replace core runtime references.

3. **ObjectNavigator reflection (MEDIUM)** — Exposes ALL public properties of any object via reflection, including Engine.FileSystem, Engine.System, Engine.Libraries. No property blocklist exists.

4. **Signature/Verified not implemented (MEDIUM)** — Properties exist but no crypto verification logic. A future code path checking `Verified == true` would be a false positive.

### Files reviewed:
- `PLang/App/Memory/Data.cs` (core + constructor)
- `PLang/App/Memory/Data.Envelope.cs` (pipeline)
- `PLang/App/Memory/Data.Navigation.cs` (path traversal)
- `PLang/App/Memory/Data.Result.cs` (merge)
- `PLang/App/Memory/Variables.cs` (variable storage)
- `PLang/App/Types/this.cs` (type registry)
- `PLang/App/Memory/Navigators/*.cs` (all 5 navigators)
- `PLang/App/View.cs` (serialization views)
- `PLang/App/Context/PLangContext.cs` (context + system vars)
- `PLang/App/actions/variable/set.cs` (variable set action)
- `PLang/App/actions/convert/fromJson.cs` (JSON parsing)

## Code example — the pattern

Most findings share a pattern: **recursive processing without depth limits**.

```csharp
// Data.Navigation.cs:14-65 — represents all recursion findings
public Data? GetChild(string path)
{
    // ... parse next segment ...
    var child = new Data(segment, childValue, parent: this);
    return child.GetChild(remaining);  // recurse — no depth counter
}

// Fix pattern (applies to all):
public Data? GetChild(string path, int depth = 0)
{
    if (depth > MaxNavigationDepth)
        throw new InvalidDataException("Navigation depth exceeded");
    // ...
    return child.GetChild(remaining, depth + 1);
}
```

## What's next

- Coder should address the 4 HIGH findings before merge
- MEDIUM findings should be tracked as follow-up work
- All findings documented in `security-report.json` with file:line references and proposed fixes
