# codeanalyzer: app-systems

Traces to: decisions 651 and 689 (the locked core), 673 (avoid reflection; one door to a type's members), 714 (the OBP read of the builder's C#, the source of the smells weighted highest) and 717 (the json kind's clr carrier), in `.bot/app-systems/architect/summary.md`.

Read `common.md` beside this file first.

## Why you first

The line has 977 changed C# files since runtime2. The locked core is where a shape mistake costs the most, because everything else is built on it and its surface can't change without Ingi. So your pass is an OBP scan of the core's diff first, then the builder pipeline this branch added.

## Scope, in this order (one report section per area, so a long run can stop and resume)

1. **data and the wire:** `PLang/app/data/` (+1.8k −3.7k since runtime2), `PLang/app/data/Wire.cs`.
2. **channel and stream:** `PLang/app/channel/`.
3. **the core value types:** `PLang/app/type/item/{text,number,bool,list,dict,path,datetime,duration}/`.
4. **the type system:** `PLang/app/type/this.cs`, `type/list/`, `type/kind/`, `type/property/`, `type/item/this*.cs` (item's base virtuals).
5. **the serializer:** `PLang/app/type/format/`, `type/serializer/`, `type/item/wire/`, and the item codecs `type/item/kind/{json,dict,list,reflection}/` (+1,376, all new; added after area 5's first pass, which missed the json writer's second write door, F5.2, because this folder wasn't in the brief).
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

## In plang

The same brief as steps. The comments under it show what a finding looks like.

```plang
OBP scan of app-systems' locked core before the merge to runtime2 (decision 718)
Start
- read .bot/app-systems/architect/briefs/common.md
- scan each area below in order, one report section per area, so a long run can stop and resume
- for each area, read `git diff 0ea5a4b94..app-systems -- <path>`, then read each touched type in full, not only the diff
- run `dotnet run --project Tools/ObpScan -- <type-or-file>` on each changed type, so every member comes with its name flags and callers
- follow Documentation/v0.2/obp-scan.md's triage order, and your character's passes
- area: data and the wire — PLang/app/data/, PLang/app/data/Wire.cs
- area: channel and stream — PLang/app/channel/
- area: the core value types — PLang/app/type/item/{text,number,bool,list,dict,path,datetime,duration}/
- area: the type system — PLang/app/type/this.cs, type/list/, type/kind/, type/property/, type/item/this*.cs
- area: the serializer — PLang/app/type/format/, type/serializer/, type/item/wire/, type/item/kind/{json,dict,list,reflection}/
- area: call and variable memory, events — PLang/app/call/, PLang/app/type/item/variable/list/, PLang/app/event/
- area: the module registry and app — PLang/app/module/this.cs, PLang/app/this.cs
- area: the builder pipeline (new on this branch, +4.2k) — PLang/app/goal/step/, PLang/app/module/build/
- skip anything next-branch.md or Documentation/Runtime2/obp-cleanup.md already holds; cite it instead
LIMITS: read only; no fixes; a fix that changes a locked surface is a proposal for Ingi, never an edit
DONE WHEN: every finding has its smell name (CLAUDE.md's names), file:line, the fix direction, blocks or carry, and surface change yes/no
REPLY: your report in .bot/app-systems/codeanalyzer/, and a message in the Done/HASH/CHECK/OPEN shape
WHY: the core carries everything else and its surface can't change without Ingi, so a shape mistake there costs the most; 977 C# files changed since runtime2 and no OBP pass has run on them since 06-07

/ weighted highest: each was found on this branch in the last two days, so there are likely more
/
/ an exception used as an answer (step/this.Validate.cs, fixed in 0eb097e00)
/   try { if (JsonSerializer.Deserialize<string>(m.Value) is { } s) literals.Add(s); }
/   catch { }
/   → the value answers: a decode that returns a failure, never a catch asking yes/no
/   → a module's check returns Error?, it never throws
/
/ stored twice (Cover's Defaults() copied action.Freeze line for line, fixed in b982cbc94)
/   two walks answering "what does this action freeze?", already giving different answers
/   → one member on the owner, both callers read it
/
/ "is X?" by name (pick/list/this.cs:587, carried with 677)
/   if (capture && property.Name is "Type" or "Default") continue;
/   → the behaviour moves onto the type; never a better comparison
/
/ clr leak (type/item/kind/json/this.cs, carried as the {item, json} door)
/   parsed json held as clr(JsonElement), so %cfg% is dict answers false
/
/ reflection: a new GetProperty/GetMethod on a plang item (Ingi: "doing reflection should be avoided as possible")
```
