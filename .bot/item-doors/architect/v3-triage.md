# item-doors — the architect's triage of codeanalyzer v3 (decision 279)

Source: `.bot/item-doors/codeanalyzer/v3/report.md` (46f7965b5). Section numbers below are the report's.

## A. Fix before merge (the coder, one cause per commit, red-first or under green tests; no locked addition)

1. **§1 "kept or used?" asked at four holders** (`data/this.Output.cs:55`, `type/property/this.cs:224`, `wire/kind/plang/writer.cs:59`, `View.cs:136`). The view is always asked; a value with no variable is written plainly by the view. `Variables` keeps its one guard. (v2 R2, left "at the root" in 185.)
2. **§5 `actor/setting`'s row key in a Data's `Name`** (`"<actor>!<path>"` built at `:190,205`, parsed back at `:254,268–269`, `Name.ToLowerInvariant()` three times). The actor answers its row key, or the store takes the actor as its partition. `_classes`/`_addresses` mirroring each other: one.
3. **§7 the command-line walk twice** (`Executor.cs:61–79`, `terminal/type/program/this.cs:175–180`): one door that takes parsed flags.
4. **§7 the settings-or-fallback Data built twice** (`goal/step/action/this.Item.cs:38–43`, `module/this.cs:203–209`): one door.
5. **§7 the open-generic interface probe copied ~11 times**: one place on the type side.
6. **§8 `call/list/this.cs:177` positional nulls through a private overload building three kinds of frame**: each kind builds itself. `goal/step/list/this.cs:176–178` `goal != null ? Push(…) : null` three times.
7. **§6 type checks in code item-doors touched**: `test/list/this.cs:43` ReferenceEquals (the actor answers); `item/source.cs:145,155,224` ReferenceEquals (Read answers it parsed nothing); `source.cs:231–247` the literal-name special case and the throw caught by its own catch.
8. **§4 sentinels in code item-doors added or touched**: `View.cs:220` `Error is { Key: "VariableNotFound" }` (the error answers); `debug/this.cs` `"?"` and empty text as "not given" (the setting's `Goal` answers `Matches(name)`; goal and step answer a label); `debug/this.cs:199 Watch(… string change …)`.
9. **§10/§11 names in code item-doors added or touched** (role/origin words, verb+noun): `SpeaksPlang`, `FormatValue`, `WriteFiltered`, `WriteLlmBlock`, `Fresh`, `Mark` (two meanings), `ValidateKind`; locals `held/kept/saved/own/made/built/found/declared/written` where item-doors wrote them. One commit per file family.
10. **§11 debug's eight static methods fetching `context.App?.Debug`**: instance methods. `Write(object?)`'s local `envelope`.
11. **§11 history/task comments** ("stage 6: was …", "decision 818", "ruling 6"): docs say what is.
12. **§2's `catch … when (ex is JsonException or NotSupportedException)`** (`wire/kind/plang/this.cs:62,106`): the error rule (`catch (AppException)`), if the throw site can carry its Error.
13. **From C, the real point under 270c** (Ingi: "yes"): an unnamed duration or size is marked by an empty kind, which its write checks. Instead, an unnamed duration or size is its own kind, whose write asks the asker's standard, so no write checks for an empty kind. 270c's behaviour is unchanged.
14. **From C, names** (Ingi: "yes"): `carried` (a participle as a class name) is named for what it is; View's private `Settled` beside `Data.Settle` becomes one name for one idea.
15. **From C, the call setting asked at every push** (270b): the ask sits once where pushes share a door (already asked of the coder in 270b's amendment). Confirm it, or say which sites are truly their own.

## B. Locked: a follow-up branch after item-doors (Ingi: "yes", decision 280). Not merge blockers.

0. **A5 moved here (decision 281):** the open-generic interface probe copied at ~11 sites (Registry, type, type.list, list, reflection, node, property, actor/setting). One owner needs a locked `type.list` member or a static extension on `System.Type`, so it waits for the follow-up branch.
0e. **The builder's walks run the structure (293/293a, moved here by decision 294):** each node runs its own part down the structure as read (step → code → action → property.list → property → its code by its declared type), with no outside walker, no flattening member and no type switch on values. Today's `action.Action` (`goal/step/action/this.Build.cs:9–22`) and its 12 walk sites (Build, action.Validate, Callee, clause.Refuse, step.Validate's Walk/Values/Texts and skip, pick.list's Every and :264) stay until then. Open shape: (A) every node owns every walk (~50 methods), or (B) each node answers its `action.list` and each action owns the work. Ingi leaned to `.list` naming. Add members to `property`/`property.list` (Ingi's yes in 293a). Six walks no test catches today (Freeze, Reduce, IsDynamic, clause.Refuse, step.Validate's walkers, pick.list's): write their tests first.
0d. **Names on a pinned surface or shared with one (decisions 285–286):** call.list `Push`/`Isolate`'s `held` parameter; DynamicData's ctor `found`/`declared`; duration/size `Refuse(… string written …)`; `built` on the source, json source, kind.Make, wire and wire.template ctors (with `IsBuilt` and `ReadContext.IsBuilt`); property.list `Reduce(declared, …)`; `Own(string name)` (golden 2928). The `Held` family, decided at once with the pinned `data.Held()`: type `Held(raw)`, type.list `Held(key)`, kind.empty `Held`, variable `Held(context)`, goal/step.Validate local `Held`s, pick.list `Held(actions)`, screen element.list `Held(selector)`, OpenAi `Tool.Held`, Fluid `Item.Held`, the test extension `Held(name)`. (Archive's `held` is a plang type's name and stays; screen Window's `Held` means buttons held down.)
0c. **From A9 (decision 283):** `ValidateKind` (verb+noun, returning an `(ok, actual)` tuple) is the member of `app/data/IKindValidatable.cs` (locked; implemented by image, called by variable.set).
0b. **From A7 (decision 282):** `item/source.cs:145,155,224` `ReferenceEquals(born, this)`. The serializer's readers would answer "parsed nothing" (a change to their answer contract, which is locked). `item/source.cs:231` `Type.Name is "dict" or "list"`: the type would answer that its values are containers (locked).

1. **§2 the view compared by constant** (`view == View.Store` in the plang format and the signature reader; "from our store" held as view, Origin and `Window == null`): the view answers `Window`, `Origin`, `RequiresSignature` (internal members on View, the serializer's).
2. **§3/§4 `type.Make` answering value-or-error**: ~29 throwaway `new data("", …)` carriers read only for `.Error`.
3. **§5 the type answering its settings address and paths** (`Activator.CreateInstance(...).Path` per class at `actor/setting/this.cs:375,388`).
4. **§6 a typed `Bind<T>` on the event door** (debug's nine `item is goal` filters after `Own()`).
5. **§11 `Data` born with its context** (`Data.Context` nullable, re-checked ~14 times).

## C. Declined: contradicts a decision

- §4/§7 "size/duration born in a standard": 270c (Ingi): a value with no standard named is born with none, and the asker's standard decides its look.
- §8 "call settings read synchronously from the built cache": 270a/270b (Ingi): settings are async, and Push takes the setting.
- §10 `Settle`: Ingi's own name (`data.Settle(view)`, 257). `Kept`/`carried`: 237/241.
- §9 item's json and plang `Write` overloads: decided in 207 (the paste by double dispatch, Ingi's "ok").

## D. Backlog after merge (pre-existing, not introduced by item-doors)

**A real bug first (from A11's dropped TODO, decision 287):** a sub-goal's parent-scope variable doesn't survive an `on.error` retry. A parent-scope `%messages%` reads null on the retry, and null answers ValidationError (`module/llm/code/OpenAi.cs`, the comment now states it). It needs a red test and a trace in the follow-up.


§6's `number/this.Arithmetic.cs:43`, `dict/this.cs:306`, `type/this.cs:194`, `Registry.cs` type switches, `Hop.cs` string switch, `property` `switch (Member)`; §7's size/duration R5b/c/d and the CLR unwrap copies, `dict/key` and `modifier/list` (one base), the plang writer as a forwarder; §11's born-without-a-type door, `formal/reader.cs` JSON by concatenation, the dummy-bool overloads, `Apply(object node)` doing two jobs. Each goes to `.bot/item-doors/architect/backlog.md` with its file:line, for a follow-up branch.
