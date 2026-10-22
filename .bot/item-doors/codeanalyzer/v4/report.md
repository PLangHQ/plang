# v4 report: re-check of v3 section A (decision 279)

Scope: coder commits 46f7965b5..HEAD (24 commits, 223 production files). Read only; I did not build or run tests. Two readers checked the items against my v3 root fixes; I re-verified the A7 claim myself (no `Equals` override on `actor` or `item`). Line numbers are the readers'; the shapes hold. B, C and D are not re-raised.

**Verdict: FAIL, short list (coder next).** Seven items fixed at the root (A1, A2, A8, A10, A12, A13, A14, A15), four open for one thing each (A3, A4, A6, A7), two partly (A9 names, A11 comments). Nothing structural; the list is small.

## Per item

| Item | Verdict | Evidence |
|---|---|---|
| A1 view always asked | Fixed at the root | `View.cs` `Write(data,…)`/`Write(value,…)` decide; `data/this.Output.cs` is `=> writer.View.Write(this, writer, layer)`; the property row and plang `EndRecord` call the view unconditionally; the depth guard moved into `Out`. `Store.Variables` keeps its one guard. |
| A2 row key | Fixed at the root | `actor/this.cs` `Row(address)` / `Address(row)`; row Data named by the path (`setting/this.cs:190`); `_addresses` gone, `_built` keyed by class (32c6232ec); one `ToLowerInvariant()`. |
| A3 command-line walk | Partly | `Flags(flags, context)` owns Find/Apply/Entries, both callers use it. Both callers still repeat the tail `foreach (entry in …) store.Set(entry.Name, entry)` (`Executor.cs`, `terminal/type/program/this.cs`). Root: `Flags` also sets. |
| A4 settings-or-fallback | Mostly | One door `Get(setting)`. The callers build a node on every call (`new setting.action.@this(catalog)` in `action/this.Item.cs`, `new setting.module.@this(Name, …)` in `module/this.cs`) used only for `.Path` when the class exists. Root: pass the fallback lazily (`Get(address, Func<setting>)`) or build the node only when no class answers. |
| A6 frames build themselves | Partly | No positional-null overload; the step list's three `goal != null ? Push` are gone. But each of the three `Push` bodies builds `new call.@this(_call, goal, step: null, action: null, caller, this, _datetime, _dict, caller, Variables, setting, names)`: one class marked by null `step`/`action`, a 12-argument constructor written three times, `caller` passed twice. Root: each kind of frame is its own constructor/type, or `call.@this` has three named factories. |
| A7 (unlocked part) | Partly | Throw caught by its own catch fixed (`source.cs:228-236` `data.Fail(… "MaterializeFailed" …); return data.Empty;`), `ToLowerInvariant` gone. `test/list/this.cs:43` is `actor.Equals(user)`, but `actor` and `item` have no `Equals` override (grep), so it is still reference equality, renamed. Root: the actor answers `IsUser`, or compares by `Name`. |
| A8 sentinels in debug | Fixed at the root, one left | `Set(variable, stored)`/`Remove(variable)` replace "SET"/"DELETED"; `step.Label` (`step/this.cs:22`) replaces `"?"`; `debug/setting/this.cs:24 Matches(name)`; `View.cs:245` uses `Status.IsNotFound`. Left: an empty `Grep` meaning "no grep" at `debug/this.cs:60`. |
| A9 names | Mostly | `SpeaksPlang`, `FormatValue`, `WriteFiltered`, `WriteLlmBlock`, `Fresh`, `Mark` gone. Remaining role words in code item-doors wrote (not B): `data/this.Navigation.cs:50,56,68 written`; `module/signing/code/Ed25519.cs:35 unread`; `number/serializer/Reader.cs:30 made`; `variable/this.cs:227 born`; `data/reader/this.cs:43 kept`; `terminal/type/program/this.cs:65 Found(bool sandbox)`; `number/this.Arithmetic.cs:42 Derived`; `View.cs` `Declares/Undeclared/Unread`. Renames that introduced misleading names: `DynamicData._data` is a `Func<Context, Data?>`; `variable.set` local `clause` holds a type entity; `IEnumerable<@this> Action` is a collection named as its own type; `Annotation(prop, slot)` in `formal/reader.cs` is a noun that calls `Fail`; `Loaded(string raw)` in `goal/list` is a participle. Not checked: behaviour in all ~100 renamed files (coder reports green apart from the six known Wire reds). |
| A10 debug statics | Fixed at the root | No `static` left in `debug/this.cs`, no `App?.Debug` reach; local `envelope` is `data`. |
| A11 comments | Partly | No decision/ruling/stage refs in added comments. Still: `actor/setting/this.cs:370` "774 A"; "until the implicit-conversion / implicit-operator branch" in 13 files (`data/this.cs:300,602,826`, `error/this.cs:26`, `bool:30`, `text:324`, `number:35`, `separator:35`, `short:11`, `item/this.cs:397`, …): a branch reference. State what is, or drop. |
| A12 `catch … when` | Fixed at the root | `wire/kind/plang/this.cs:61,63,107,109` catch `AppException` (Error whole) and `JsonException` alone (System.Text.Json's own throw, no Error to carry); `data/schema/this.cs Reader` throws `AppException`. No plang-side `NotSupportedException` thrown that the dropped catch used to absorb. |
| A13 unnamed duration/size | Fixed at the root | `duration.span` (`span/this.cs:15`), `size.count` (`count/this.cs:16`) are kinds that write themselves; bases abstract (`duration/this.cs:19`, `size/this.cs:11`); no write checks for an empty kind. |
| A14 names | Fixed at the root | `carried` is `attachment` (`data/attachment.cs`); `View.Settled/Written` gone, `Used` folded into `View.Settle` (`View.cs:100`); `Opened` is `Open`, `Ignored` is `IsIgnored`. `View.Settle`/`Data.Settle` share Ingi's name (257). |
| A15 call setting once | Fixed, sites confirmed | `foreach.cs:76` asks once for the loop; `OpenAi.cs:518` once for the tool call's two pushes. Sites that keep their own ask are single pushes or child contexts (`goal/this.cs:414`, `step/this.cs:134`, `action/this.cs:198`, `Gate.cs:29`, `foreach.cs:138`, `goal/call.cs:97`). `OpenAi.cs:559` not traced. |

## New smells these commits introduced, ranked
1. **Mode test parses a Data's Name** (`Executor.cs`): `values.Any(entry => entry.Name.StartsWith(store.Address(modes[word]) + ".", …))`, was `settings.Contains(modes[word])`. A second answer in `Name`, a raw hand-off. Root: `Flags` answers which settings it wrote. (The mode table itself stays accepted; the new string test is the finding.)
2. **A13 reflection by name and a tri-state** (`kind/subclass/this.cs`): `GetProperty("Internal", DeclaredOnly)` into `bool? _internal`, null = "ask the base" (`OwnInternal`). The same `!type.Internal` filter at `choice/set/family/this.cs:23`, `duration/this.Parse.cs:15`, `size/this.Parse.cs:19`. Root: the kind answers `IsInternal` once. `OwnInternal` is a new kind virtual (locked surface); the commit cites A13/280, confirm that covers the virtual.
3. **A13 copies of the R5b fork:** `span/this.cs:17-20` and `count/this.cs:16-22` repeat the `list[(…, standard.Name)].Make(…) as … ?? throw new InvalidOperationException` block; `count.Create` repeats the leaf unwrap and adds a `long/int/string` type switch. These extend the D backlog entries; fix with them.
4. **A4 throwaway node** (above) and **`program/this.cs` still builds `new actor.setting.@this(context)`** to read `.Format.IsPlang` (a store made to read one member, and disposable).
5. **`actor.Address(string row)`** shares a name with `setting.Address(Type, option)` and does the inverse; `Row("") is var prefix && …` is a pattern-variable trick to get the prefix. Root: a plainly named prefix member.
6. **Linear scans replace the mirror:** `Address(Type)` is `Classes().FirstOrDefault(…).Key`, `Get(Type)` is `Classes().ContainsValue(host)`; comment says "thirty classes, a look is cheap". Accepted as a trade; the per-class `Activator` (B3) is untouched.
7. **A8 broadened behaviour:** `View.cs:245` treats any not-found error as "unread" in a dump (was `VariableNotFound` only). A path or file not-found from a value's settle now shows as unset. Probably wanted; the commit message does not say so.
8. **Names:** `Steps(goal)` local function (`goal/step/list/this.cs:181`) is named for what it iterates, not what it does; four `Get` doors on the store (`Get(setting)`, `Get(address)`, `Get(Type)`, `Get<T>()`).
9. **View still asks `HasVariable` at `View.cs:65,78`:** that is the view deciding (A1's design); fine, noted only so no one re-raises it.

No history/task comments were added by these commits. No locked-core additions seen except the `OwnInternal` virtual (item 2).

## The short list for the coder
1. A7: `test/list/this.cs:43` — the actor answers (`IsUser`), no `Equals`/`ReferenceEquals`.
2. A3: `Flags` also sets; delete the two `foreach … Set` tails.
3. New 1: `Executor.cs` mode test — `Flags` answers which settings it wrote.
4. A4: no throwaway node (lazy fallback).
5. A6: each frame kind its own constructor; no 12-argument constructor with nulls.
6. A9 leftovers and the five misleading names; A11 leftovers ("774 A", 13 "until the … branch" comments).
7. New 2/3: one `IsInternal` filter; `span`/`count` through the shared fork (or note them on D).
