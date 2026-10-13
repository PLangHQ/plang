# Reviewer findings on app-systems, as the architect checked them

Each finding the reviewer bots report, checked against the code before it goes anywhere. Status: **fix** (sent to the coder, batched), **carried** (already on `next-branch.md`), **blocks** (to Ingi), **rejected** (with why).

## codeanalyzer (plang-ae), area 1: data and the wire (26fa8f4e7)

| finding | checked | status |
|---|---|---|
| the set-child bridge, `data/this.Navigation.cs:51–63` | cited, on next-branch.md (59's remainder) | carried |
| readers throw their refusal, `data/schema/signature.cs:83` | cited, on next-branch.md (714) | carried |
| parsed json is a clr carrier, `Operator.IsType`'s promise | cited, on next-branch.md (the `{item, json}` door, 717) | carried |
| dead `MaxNormalizeDepth`, `data/this.Output.cs:21` | grep: referenced only at its declaration | fix (batch 1) |
| `IsDataMarked(JsonElement)` on data, `data/this.cs:56` | grep: only caller is `type/item/serializer/json.cs:94,187`; a static taking a `JsonElement` on data is a stray helper and a clr reach; it belongs on the json serializer beside its caller; `internal`, so not a surface change | fix (batch 1) |

## codeanalyzer (plang-ae), area 2: channel and stream (29864e666)

| finding | checked | status |
|---|---|---|
| the channels' `catch (Exception) when (not fatal)` | read: the base doors (`channel/this.cs:143,166,190`) turn a transport throw into a typed error and let `AppException` keep its key; a real I/O boundary, not a smell | cleared |
| F2.1 `question?.Clr<string>()` for an emptiness check, `stream/this.cs:160` | read: lowers the question to a CLR string to ask "is it empty"; the value answers its own emptiness (opened box / clr leak at a leaf) | fix (batch 1) |
| F2.2 the transport boundary twice | read: `AskAsync` (`channel/this.cs:189–194`) catches what `stream.Ask` (`stream/this.cs:197–202`) already catches, and `ReadAsync` (`:165–170`) what `stream.Read` (`:135–139`) does; the two disagree: stream lets `OperationCanceledException` pass, then the base turns it into AskError (stored twice, and the cancel is lost). One boundary, in the base door, cancel excluded; the coder checks for direct callers of `stream.Read`/`Ask` first | fix (batch 1) |
| F2.3 `ReadAllBytesAsync` throws on `!CanRead`, "kept for v1 callers", `stream/this.cs:245–264` | grep: `ReadAllTextAsync` has no production caller (two tests); `ReadAllBytesAsync` has one, the stream's own `Read` (`:130`), whose catch turns the throw into ReadError. Both are public on stream, a locked surface: removing them changes the surface golden | carry (a surface removal is Ingi's; batch it with other surface asks) |

## codeanalyzer (plang-ae), area 3: the core value types (3d45490d8)

| finding | checked | status |
|---|---|---|
| the seams (number's Ladder, path's scheme registry, list's one door to C#) | its summary: one owner or a registry at each, no per-type fork; list's reflection is interop (building a `List<T>` for a runtime T), not the carried member-read reflection | cleared |
| F3.1 the parse-or-convert failure written nine times | grep: `((value as item)?.Clr<object>() ?? value) is string` in bool, datetime, date, time, duration, size, guid, image, list (`bool/this.cs:79–81` read). Stored twice, and each copy lowers the source to C# to ask "were you text?". Its suggested fix, a protected helper on `item`, is an addition to item's locked surface and a helper; the shape is the source answering its own failure (a text says "cannot parse 'x' as T", anything else "cannot convert Y to T"), T giving only its hint | carry (next-branch) |
| F3.2 `dict/this.cs:84` `catch (JsonException) { return null; }` | read: "is this text a dict" answered by a swallowed parse; the dict narrowing a `clr(JsonElement)` at `:89` accommodates the carried `{item, json}` door | carry (with the `{item, json}` door) |

## codeanalyzer (plang-ae), area 4: the type system (480ea48c1)

| finding | checked | status |
|---|---|---|
| no inherited fork in `type.@this` or item's base; their reflection is 673's carried member-read machinery; `type/convert` moved onto each type's `this.Owns.cs` | its census and summary | cleared |
| F4.1 `property.IsDefault` decides equality by type, `type/property/this.cs:118–138` | read: four arms (a bool by its word, `Type.Name == "variable"` by name without `%`, a double parse, else the raw text) plus a `System.Text.Json` decode of a quoted slice. A fork by type name; a new type whose written form needs normalizing falls to the raw-text arm and is silently "not default". **No new member is needed:** the value already answers equality, `item.Compare(other, context)` (`type/item/this.cs:135`), so IsDefault is `Compare(Type.Make(Default), context) == Equal`. But that is async and needs a context, so `IsDefault`'s signature changes (two callers: `pick/list/this.cs:216`, `property/list/this.cs:120`): a change to property's locked surface | carry; surface ask for Ingi (batch) |
| F4.2 `type/this.cs:547` writes `name<kind>` only for list, dict and choice | cited: a site of 711 (a kind is a type) | carried (711) |

## Surface asks for Ingi, collected (asked together at the end of the codeanalyzer's run)

- F2.3: remove stream's `ReadAllBytesAsync`/`ReadAllTextAsync` ("kept for v1 callers"; no production caller but the stream's own Read).
- F4.1: `property.IsDefault(value)` becomes async with a context, answered by the value's own `Compare`.

## codeanalyzer (plang-ae), area 5: the serializer (cd9381622)

| finding | checked | status |
|---|---|---|
| one wire serializer; Encode calls `data.Output`; sign-if-missing in Encode | its trace, followed from Encode into `data.Output` | cleared |
| F5.1 the writer contract still names `Normalize`, deleted this branch: `format/IWriter.cs:7,11,126`, `kind/json/writer.cs:12,129,155,194,200`, `NormalizeException` | grep: no live Normalize dispatcher; the docs and the exception's name describe a pipeline that's gone | fix (batch 1: comments and the error text; the exception's rename rides with F5.2) |
| **F5.2 (found by the architect checking F5.1; sent back to the codeanalyzer to write up)** a second write door decides by type: `IWriter.Value(object)` (`format/IWriter.cs:129`; json at `kind/json/writer.cs:131–203`) switches on the runtime type, with its own Data, dict and list cases beside the CLR primitives, while dict and list also write themselves (`dict/this.cs:175`, `list/this.cs:299`); and item has two self-write doors, the sync `Write(IWriter)` (`type/item/this.cs:838`, 61 overrides) and the async `Output` (`:916–918` calls the sync one "the OLD Peek().Write path"). Still reached from `type/kind/this.cs:340`, `type/item/this.cs:937`, `signature/this.cs:221`, `kind/dict/this.cs:45` | read in full; not on next-branch.md or obp-cleanup.md | pending the codeanalyzer's write-up; likely carry with a surface ask (IWriter and item are locked) |

**F5.2 confirmed** by the codeanalyzer (2c28ddcf7): fork and stored twice; surface change yes; doesn't block (both doors agree and are green), but the heaviest shape debt the merge carries, so named for Ingi. Its fix direction: one door, every value through its own `Output`; the writer's dict, list and item arms collapse to "the value writes itself"; the sync `Write`/`Value(object)` retire once their four callers move to `Output`. The miss was the brief's: area 5's paths left out `type/item/kind/` (+1,376, all new), now added; the codeanalyzer scans the rest of it.

- Added to the surface asks: **F5.2**, retire item's sync `Write(IWriter)` and `IWriter.Value(object)`'s plang arms (carry to the next branch; named as the largest shape debt).

## codeanalyzer (plang-ae), area 6: call and variable memory, events (e2417e57c)

| finding | checked | status |
|---|---|---|
| the `on/` events: a registry and one type per event kind, no dispatcher on the name; call/'s collections each lock their own private field; variable memory reading its own entries isn't a courier | its summary | cleared |
| F6.1 "is this a system variable" (`StartsWith("!")`) and "is it worth saving" (`is not DynamicData`) open-coded across variable memory | grep: 10 sites in `variable/list/this.cs` (`:362, 377, 394, 406, 408, 418, 438–441, 474, 496–497`), `this.Snapshot.cs:25–29`, `this.SnapshotAt.cs:39`. Two questions, not drift: a listing keeps `Now`/`GUID` (they're readable variables), a snapshot drops them (always fresh, not worth saving). The shape is each answering for itself: the name says it's a system name, the Data says it's always fresh (a member on data, locked). A private predicate would only gather the `is DynamicData` type check in one place | carry (next-branch) |

## codeanalyzer (plang-ae), area 5b: the item codecs, `type/item/kind/{dict,list,reflection}` (a397379d9)

| finding | checked | status |
|---|---|---|
| kind/dict and kind/list (the C# host codecs) | every behaviour on a carried list: member access via the reflection kind (673), Convert narrowing `clr(JsonElement)` (717), Output through `WriteReflected` (two more sites of F5.2), Set throwing on a bad index (59's remainder) | carried |
| kind/reflection | the carried 673 door; `Property`/`Method`/`Twice` (`reflection/this.cs:36/56/69`) are the stopgap statics 673 names; `Tagged.cs` the one owner of which C# properties ship ([Out]/[Sensitive]/[Masked], 681) | carried |
| F5.1 one more stale `Normalize`: `reflection/Tagged.cs:8` | grep confirmed | fix (batch 1, with F5.1) |

## codeanalyzer (plang-ae), area 7: the module registry and app (96e7f7955)

| finding | checked | status |
|---|---|---|
| actions resolve by a dictionary lookup, no switch on the action name; the clause/loop/keep/plain mint runs once at registration; `app` owns each collection once, no naked public collection, no service locator; path schemes register with factories (`app/this.cs:310–312`) | its reading | cleared |
| reflection at `module/this.cs:194, 203–215`, `app/this.cs:432`; the reader's thrown refusal at `app/this.cs:437` | cited | carried (673, 714) |

## codeanalyzer (plang-ae), area 8: the builder pipeline (5903dc85c), and the verdict

| finding | checked | status |
|---|---|---|
| the pick list's name forks (`Code()` on "loop.foreach"/"variable.set", IsCapture, the Type/Default/Name checks) plus one more of the family, the elseif/else chain read by name (`pick/list/this.cs:308`) | cited | carried (677) |
| Cover and Freeze: one answer (`action/this.Build.cs:92`), Cover doesn't re-walk it | read | cleared (b982cbc94) |
| the formal reader's parse by type name | the reader owns plang's surface grammar (the writer its inverse); parser design, not behaviour scattered on the types | cleared |
| F8.1 "is this an action slot": `Type.Name == "action" \|\| (Type.Name == "list" && kind.Name == "action")` | grep: four sites in three files, `action/this.Validate.cs:54–55`, `formal/reader.cs:221` and `:349`, `action/serializer/Reader.cs:153–154`. Stored twice, and "is X?" by name. The shape isn't a better predicate (`IsActionSlot`, a member on locked type); the action type and a list of actions each read their own value, so no site asks. A kind being a type (711) is where `list<action>` answers for itself | carry (next-branch, with 711) |

**Verdict (the codeanalyzer): PASS, merge-clean on behaviour.** Checked: agreed. Batch 1 (five fixes) is with the coder. The surface asks are F2.3, F4.1, F5.2 and F8.1, all carried to the next branch; none is needed for the merge. F5.2 was already on next-branch.md (added with 724).

## tester (plang-4f), part 1: the baseline

| finding | checked | status |
|---|---|---|
| the baseline reproduces: C# only the 6 carried; plang 245/16/12/3 in 2 of 3 clean runs, the same 16 names; `git status` clean after each run | its runs | cleared |
| run 2 was 244/17: the llm tests call api.openai.com live, and a transient "network unreachable" failed LlmCache | the llm tests need the network and a key | carry (next-branch: llm tests run offline, recorded answers or a skip without a key) |
| T1 `test/module/llm/LlmCache.test.goal` passes for the wrong reason: titled "verify second returns cached result", it only asserts both answers are not null | read the goal: the two asserts are `is not null` | fixed (builder, 8f8e0bebf: `assert %second!Cached% is true`, the query result's own cache-hit signal; red with the second query's cache off, then reverted) |
| `./dev.sh build` doesn't restore a fresh checkout | dev.sh's msbuild calls (`:205`, `:215`) run `-t:Build` without `-restore`. The tester's "exits 0" was its own pipe (`./dev.sh build 2>&1 \| tail -30` reports tail's exit); dev.sh exited 2 with NETSDK1004 | fixed (coder, 181468c11: a never-restored or changed project is restored first, serially; a failed restore exits 2) |
