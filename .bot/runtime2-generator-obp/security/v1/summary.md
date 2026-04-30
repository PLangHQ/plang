# Security v1 — runtime2-generator-obp

## What this is

First security pass on this branch. Branch is the v4 OBP refactor of the
PLang source generator: resolution moves from `Data.Value`'s side-effect
getter into a fresh-walk `Data.As<T>(context)`; generator splits into
`Discovery/` + `Emission/Action,Property/`; build-time PLNG001 enforces
`Data<T>` / `[Provider]` / `[VariableName]` only on action properties.
Coder went through 4 rounds (codeanalyzer cleared v3, tester approved v4).
My job was to red-team the security delta, not the whole runtime.

## What was done

Reviewed the four security-relevant surfaces:

1. **`Data.As<T>(context)` resolution path** (`PLang/App/Data/this.cs:383`).
   Cycle protection via `[ThreadStatic] _resolvingValues` HashSet plus
   `ResolveDepthLimit=32` for expanding chains where each level produces a
   new string. Verified the HashSet leak corner case (depth-trip return
   without try/finally): the root frame's finally nulls the HashSet, so no
   cross-execution residue.
2. **Source-generator emission**. Trust boundary is the developer's own
   source code — interpolation of identifiers into generated C# is a
   build-time, supply-chain concern, not a runtime injection vector.
3. **`App.Run` dispatch + catch filter** (`PLang/App/this.cs:415`).
   Excludes NRE/OOM/StackOverflow correctly; CallStack frame snapshot in
   finally is guaranteed.
4. **JSON ingestion**. `MaxJsonDepth=128` in `Data.UnwrapJsonElement`;
   inner `Type.Convert("json", raw)` falls through to STJ default depth
   (64). Both protected.

Four findings written to `security-report.json`: 1 medium, 3 low. None
critical/high. Verdict **PASS**.

The dominant finding (Medium #1): `__SnapshotParams()` emitted by both
`Emission/Property/Data/this.cs` and `Emission/Property/Legacy/this.cs`
captures `PrValue` and `FinalValue` into `Error.Params` verbatim, ignoring
`[Sensitive]` on the property. Currently dormant — no handler uses
`[Sensitive]` on a `Data<T>` parameter — but a footgun for the first
developer who reaches for the attribute. Same recurring pattern as the
standing `Variables.Snapshot()` finding (memory: "not acceptable" per
Ingi). Per security-debt rule, both emission sites are listed.

## Code example — the gap pattern

`Emission/Property/Data/this.cs:51`:

```csharp
public override void EmitSnapshotEntry(StringBuilder sb)
{
    var declaredType = TypeName.Replace("global::", "");
    sb.AppendLine($"        {{");
    sb.AppendLine($"            var __pr = __action?.Parameters?.FirstOrDefault(...);");
    sb.AppendLine($"            __list.Add(new global::App.Errors.ParamSnapshot {{");
    sb.AppendLine($"                Name = \"{Name}\",");
    sb.AppendLine($"                PrValue = __pr?.Value,");          // ← raw, no mask
    sb.AppendLine($"                FinalValue = {SetFlag} ? (object?){Backing} : null,");  // ← raw
    sb.AppendLine($"                ...");
    sb.AppendLine($"            }});");
    sb.AppendLine($"        }}");
}
```

Proposed: detect `[Sensitive]` in `Discovery.BuildProperty`, plumb as
`IsSensitive` on the property record, branch in `EmitSnapshotEntry`:
`PrValue = __pr?.Value != null ? \"******\" : null` when sensitive.
Mirror the convention already used by
`Channels/Serializers/SensitivePropertyFilter`.

## What's next

If pass: recommend running the **auditor**.
If reviewer wants the medium fixed first: send to **coder** with the
proposed fix in finding 1 (small, localised — Discovery + two emitters).
