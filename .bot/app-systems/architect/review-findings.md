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
