# codeanalyzer: app-systems

Read `common.md` beside this file first.

## Why you first

The line has 977 changed C# files since runtime2. The locked core is where a shape mistake costs the most, because everything else is built on it and its surface can't change without Ingi. So your pass is an OBP scan of the core's diff first, then the builder pipeline this branch added.

## Scope, in this order (one report section per area, so a long run can stop and resume)

1. **data and the wire:** `PLang/app/data/` (+1.8k −3.7k since runtime2), `PLang/app/data/Wire.cs`.
2. **channel and stream:** `PLang/app/channel/`.
3. **the core value types:** `PLang/app/type/item/{text,number,bool,list,dict,path,datetime,duration}/`.
4. **the type system:** `PLang/app/type/this.cs`, `type/list/`, `type/kind/`, `type/property/`, `type/item/this*.cs` (item's base virtuals).
5. **the serializer:** `PLang/app/type/format/`, `type/serializer/`, `type/item/wire/`.
6. **call and variable memory, events:** `PLang/app/call/`, `PLang/app/type/item/variable/list/`, `PLang/app/event/`.
7. **the module registry and catalog, app:** `PLang/app/module/this.cs`, `PLang/app/this.cs`.
8. **the builder pipeline (new on this branch, +4.2k):** `PLang/app/goal/step/` (Cover/Validate, the decider's pick list, the formal reader and writer), `PLang/app/module/build/`.

For each area: `git diff 0ea5a4b94..app-systems -- <path>`, then read each touched type in full, not only the diff. Run `dotnet run --project Tools/ObpScan -- <type-or-file>` on the changed types so every member arrives with its name flags and callers. Follow `Documentation/v0.2/obp-scan.md`'s triage order.

## What to look for

Your character's passes, with these weighted highest (each was found on this branch in the last two days, so there are likely more):

- **an exception used as an answer:** a `throw` where the caller needs an error result, or a `try { … } catch { }` asking a yes/no question. A module's check returns `Error?`; it never throws.
- **stored twice:** the same walk or rule written in two places (Cover once copied `action.Freeze` line for line).
- **"is X?" by name or type:** `Name == "…"`, `property.Name is "Type" or "Default"`, `is SomeType` choosing behaviour. The fix is moving the behaviour onto the type, never a better comparison.
- **clr leak:** System.Text.Json, `JsonElement`, raw CLR values travelling where a plang value should.
- **reflection:** a new `GetProperty`/`GetMethod` on a plang item (Ingi: "doing reflection should be avoided as possible"; the existing ones are carried in next-branch.md).

## Output

Per finding: smell name (CLAUDE.md's names), file:line, one sentence on what's wrong, the fix direction, **blocks the merge / carry**, and **surface change: yes/no** (yes = a proposal for Ingi). Skip anything already in next-branch.md or obp-cleanup.md (cite it).
