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
- area: the serializer — PLang/app/type/format/, type/serializer/, type/item/wire/
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
