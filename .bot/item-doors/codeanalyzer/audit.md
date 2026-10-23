# item-doors: OBP shape audit (codeanalyzer)

Base `1adfb9555`, tip `b91e019b5`. 84 commits, about 380 production files. Read only.

I did the stage 2/3 work (door, pairs, wire, record door, type list, app) by hand. Three read-only helper agents did the born-whole work, the members/tokens/late-fix work, and the pair-site count. I spot-checked their key claims. Claims I did not check myself are marked *(unverified)*.

## 1. In plain words

**Keep the branch. Do not start over from runtime2.** Fix three places at the root first, and finish one stage that was left half-done.

What is sound and mostly delivered:
- The direction of stage 1: a value is born holding its type.
- `Output` as the one door for writing: the sync `Write` is gone from the items, and `Bytes(bytes, kind)` is the one byte door.
- The record door: `EmitsSchema` is gone and the plang writer exists.
- The token reader, and `FromJson` is deleted.
- Members read and written through the type's own list, with the reflection gone from `item.Get`/`Set`.
- `Replace` is gone and the app's nodes are plain classes.
- The list and dict rows: `_hasWrapped` is gone from the whole tree and a read writes nothing back.

None of this needs reverting.

What is wrong. Ingi's suspicion is right in three places. In each, the problem was patched where it showed up, not where it starts.

1. **Pairs and PLNG006 (stage 2/3): fix at the root.** A registry decides how a value looks in a writer. It can only be correct if every member write in the codebase goes through it, so there are 103 call sites and a build-time analyzer to police them. Only five pairs exist. About 59 of the 103 sites write values that can never have a pair. The pairs duplicate a pattern `IWriter` already has (default method, writer overrides), and two of them cast the writer to its concrete type.
2. **The wire (`Decoded` beside `Value`): fix at the root.** A decode exists in three places, and one of them has no error handling. A verbatim-relay fork (`Owns`) was supposed to go and is still there. The architect's decision 158 already reaches the same conclusion, and it is not built.
3. **Born-whole (stage 1): fix at the root.** The headline promise ("a value always holds the type that made it, never rebuilt from a name") is false today. The base still rebuilds a type from the class, and six plan demolition items are untouched. The coder says this waits on branch 50, but about 25 "until the implicit-conversion branch" comments mark a second path that every consumer has to tolerate.

Smaller findings are in section 4.

**Why the architect's rulings kept producing this.** The pairs chain below shows it. Decision 110 needed a type's identity in every view. Decision 117 made that a registry. Decision 119 added "every member write goes through the door". Decision 129 added a lint for that rule. Each ruling was locally reasonable, and none asked whether the writer already had a way to vary (it does: `IWriter.Variable`, `Given`, `Sensitive`).

## 2. Verdict per stage

| Stage | Verdict | Top findings |
|---|---|---|
| 1 born whole | **fix at the root** | F1 fallback birth alive beside the held type; F2 six demolition items not done; F3 about 295 literal type lookups with no "my type" owner |
| 1b list rows | **keep** | F4: `slot is Data ? Peek()` repeated at 10 sites; `Output` makes a Data for every raw row (plan says write raw as is) |
| 145 families (d0138493c, cf9700e2d, f5b17dd35, 21aabdef7), 149/152 | **keep** | Reflective birth idiom (`Activator.CreateInstance(clr, type)`) copied at 8 or more sites |
| 2 one door (a, b, d, e) | **keep** | `Output` only, `Bytes(bytes, kind)`, the writer's `Kind`. `Owns` is still there (F7) |
| 2 pairs (c1) + PLNG006 | **fix at the root** | F5: 103 call sites, a lint, and a reflection registry for five pairs |
| 2 (c2) the wire | **fix at the root** | F6: three decodes, one without error handling; `Owns` fork alive; decision 158 not built |
| 3 record door | **keep** | `layer` bool threaded through `Data.Output` is older than the branch. JSON-Schema entries not built (the plan said they wait for Ingi) |
| 4 token reader | **keep** | The doc over-claims (a domain item is refused, not walked). The plan's step-row use was dropped *(design change)* |
| 5 members | **fix at the root** | F8: C# lowering fork moved into `property.Write`; literal-key arms (`on`, `setting`) beside the member list; "a key the type lacks is added" not delivered |
| 6 settings | **not built** | Decision 149 left three questions for Ingi. No restructuring commit. Only the born-whole touches |
| 8 nodes / `Replace` | **keep** | Low: a literal-key door in the nodes; a private helper `Spelled` that belongs on `kind` |
| Late fixes | 5e2105bf4: **fix at the root**. bf2648c69, 08ca3c4e2: **keep** | F9: `variable.set` renames at 4 call sites, while every other container renames at the producer |
| Outside the plan | **fix at the root** (app.Output), keep the rest | F10: hand-listed `app.Output` against decision 89's own rule. F11: scope creep (PLNG005 and `[Choice]`, Fluid ordering operators, tester's perf caches in the locked type list) |

**The two areas Ingi questioned:** the pairs/door/PLNG006 is **fix at the root** (F5). The wire's `Decoded` beside `Value` is **fix at the root** (F6).

## 3. Findings with why-chains

### F5. Pairs, the door and PLNG006 (stage 2 c1, stage 3). Breaks core shape.

Smells: **fork** (a registry that picks the form by the value's CLR class), **stray helper** at the scale of a whole analyzer, and a **cross-file discipline** (the rule "go through the door" lives in 40-odd files, not in the type that owns it).

Chain, symptom to cause:
1. **Symptom.** 103 real call sites of `writer.Kind.pair.Output(…)` (107 grep hits; 4 are comments and the door itself). Examples: `app/this.cs:47-69` (13 in one method), `goal/this.Item.cs:37,140-150`, `type/property/this.cs:219-254`, `type/item/signature/this.cs:175-179`, `type/item/this.cs:880,899`. An analyzer enforces it, `PLang.Generators/Diagnostics/Plng006.cs`, which errors on any `value.Output(writer…)`. The rule is also restated in three doc comments (`format/IOutput.cs:8`, `kind/this.cs:76-79`, `kind/pair/list/this.cs:22-25`).
2. **Counted.** The helper agent split the 103 sites by what is written. About 44 can reach a list, action, action.list, type or kind. About 59 write scalars or fixed leaves (`input/mouse/this.cs:58-69`, `llm/type/limit/this.cs:71-73`, `status/this.cs:100-101`) that can never have a pair. The plan says the registry is "looked up in one place (`data.Output`)" (`start.md:406`). Built: 103 places.
3. **Why every site has to go through the door.** The door picks a pair by walking the value's CLR base chain (`kind/pair/list/this.cs:32-35`). So a pair for `list` also applies to a `step.list` or an `error.list`. A member could be any of those, so a nested write that skips the door can silently skip a pair. Decision 119 therefore made "every member write goes through the door" a rule, and 129 made PLNG006 the enforcement. 129 recorded that the analyzer then flagged only 2 sites (`Fluid.cs:326,336`), so it was added to police a rule that had been created one decision earlier.
4. **Why the registry is found by reflection.** `kind/pair/list/this.cs:41-58` scans every type of every assembly for a class whose name equals the kind's name, whose namespace ends `.format`, and whose owner class is `<namespace minus .format>.this`. That is an address built from strings, which breaks the rule against structure from names. The plan said pairs would be keyed by input kind (107). They are keyed by CLR class (119), because a keyed-by-type design "missed clauses and loops" (the cost of the base-walk).
5. **What the five pairs are.** `list/format/formal.cs` and `action/format/formal.cs` cast the writer to `formal.Writer` (`list/format/formal.cs:15`, `action/format/formal.cs:14`) and call `BeginRows`/`BeginCall`. They are the formal writer's code living in the values' folders, and they name the concrete writer. The stated rule is "neither asks about the other" (`CLAUDE.md`, "A value writes what it is; the writer decides how it looks"). `type/format/plang.cs:12` calls `type.Write(writer)`, the type's own identity write (`type/this.cs:41`), which survived as an `internal` sync `Write` the plan said would go. `kind/format/plang.cs` is a pair that does nothing: it exists to cancel the type pair that a kind inherits by being a type.
6. **The cause.** A type has two forms, its face and its identity. `type.Output` picks between them by view (`type/this.cs`, `if (mode != View.Out) Write(writer) else writer.String(…)`), which `CLAUDE.md` calls the wrong structure for a value. The registry was introduced so that a Data's type slot could ask for the identity form without the type knowing.
7. **A mechanism already exists for this.** `IWriter` has default methods that a writer overrides: `Variable` (`format/IWriter.cs:73`), `Given` (`:78`), `Sensitive` (`:59`). `formal.Writer` overrides `Variable`, `Given` and `Raw`. Decision 117 states the rule "no new thing where one exists" and then builds a registry anyway. The formal primitives (`BeginCall`, `BeginRows`, `BeginActions`, `BeginBody`) are public on `formal.Writer` (`action/formal/writer.cs:187-240`). Adding them to `IWriter` is a locked-core addition and needs Ingi's yes.

**Root-level fix would look like:** the five pairs go. List, action and action.list override `Output` and call writer primitives. The formal writer's rows and calls become `IWriter` defaults that json and plang ignore, as `Variable` and `Given` already are. A type writes its identity in the plang record slot (`wire/kind/plang/writer.cs:45`, one site) and the property row (`property/this.cs:219`, one site). The base walk, the registry and the reflection scan go, then PLNG006, then the 103 sites go back to `member.Output(…)`.

**Cost of the fix:** an `IWriter` addition (locked). Say so to Ingi before building it.

### F6. The wire: `Decoded` beside `Value` (stage 2 c2). Breaks core shape.

Smells: **fork** (two execution paths), **split lifecycle** (one decode in three files).

Chain:
1. **Symptom.** A raw `.pr` value is decoded in three places that do nearly the same thing:
   - `source.Value(data)` (`source.cs:158-252`): the kind's `Load`, else the stored `_format.Parse`, else the type reader (`Read`), inside a try/catch that fails the Data.
   - `source.Parse(ctx)` (`source.cs:279-280`): the kind's `Parse` alone.
   - `wire.Decoded(ctx)` (`wire/this.cs:53-54`): the kind's `Parse`, else `Read`.
2. **Asymmetry.** `Value` catches `JsonException`, `FormatException`, `InvalidOperationException` and `NotSupportedException` and answers a typed `MaterializeFailed` error (`source.cs:242-251`). The wire's callers (`wire/this.cs:67,75,83`) call `Decoded` with no catch, so a bad parse inside `Output`, `Clr` or `Read` is a raw exception. The same pattern shows in `wire/this.cs:65-66`, which throws `InvalidOperationException` for a missing context. `CLAUDE.md` says this should be an Error answer.
3. **The fork.** `wire.Output` (`wire/this.cs:64`) has `if (_reader.Owns(writer)) { writer.Raw(…); return; }`, a lazy value that asks whether its writer writes its stored format, so that it can paste its characters. The plan said `Owns` and its 5 checks go. Four remain (`type/list/this.cs:264` is an unrelated `Owns`; `format/IWriter.cs:67`, `item/kind/json/writer.cs:50`, `wire/this.cs:64` are the three that matter).
4. **How it got here.** Decision 116 found that the five `Owns` sites each relay a private written form, and proposed a new `item.Written` for Ingi. Decision 158 withdrew 116 (the architect's own words: he had not checked it) and replaced it with "writing is a use". That change is **not built** (`158`'s c2, the coder was told to hold). So the branch tip carries the interim the architect has already ruled wrong.
5. **The cause.** `wire` is a subclass of `source` (`wire/this.cs:21`) only because a `.pr` slot decodes lazily and its signature once bound its raw bytes. 158 says signing re-writes the value (`Data.Digest`, `data/this.Output.cs:33-40`), so verbatim relay isn't needed for signatures. *(I did not verify that a re-written value is byte-identical to what a peer signed; 158 asserts it needs a lossless round trip.)*

**Root-level fix would look like:** build 158's c2. A wire is a use: it decodes through the one source door, and the value writes itself. `Decoded`, `Parse`, `Owns`, `IWriter.Raw` and `IWriter.Content` go. The check that costs: every `.pr` read and written again must stay byte-equal.

### F1. The fallback birth path is alive (stage 1). Breaks core shape.

Smells: **fork**, **late stamp** by another name.

Chain:
1. **Symptom.** `item/this.cs:382-383`: `Type => _type ?? (FamilyOf(GetType()) is { } kind ? new(NameOf(kind.Family), …) : new(GetType()))`. `item/this.cs:391`: `protected @this() { }`. Bare types are built in constructors: `number/this.cs:40` (reached by 15 implicit operators), `duration/this.cs:44`, `bool/this.cs:31`, `choice/this.cs:43`, `text/this.cs:345`, plus `error/this.cs:28` and `event/this.cs:108`, which still override `Type` with `new(typeof(…))`.
2. **A second Data path.** `data/this.cs` keeps a private constructor with `_context = null!` for the same reason (diff of `1d3c5007f`).
3. **Consequence.** A `number` made by an operator holds a bare `{number, int}`, not the app's registered one. "Born holding the type that made it" holds only for values made through a context.
4. **The cause is a deferral.** The implicit conversions into plang types are slated for "branch 50" (`start.md:370`). They number 20 now, against 21 at the base. About 25 comments in the diff say "until the implicit-conversion branch". The fix to a promise ("never rebuilt", `start.md:23-24`) was therefore a deferred second path, and each consumer tolerates both.

The plan explicitly defers this part. The audit finding is not that the coder skipped it. It is that stage 1 is not finished and the interim has to be read as a fork by every later stage (for example `data.As<T>` at `data/this.cs:584`).

### F2. Plan demolition items not done (stage 1). Breaks core shape.

Verified on the tip:
- `type/this.cs:145`: `new global::app.type.kind.@this(kind)` still makes a kind from a name.
- `type/item/source.cs:112`: `Type.Kind.IsText || Type.Name.ToLowerInvariant() is "text"` is the text name floor the plan lists.
- `module/ui/code/Fluid.cs:194`: `format is { Name: "item" }` is the render-fix name check the plan lists.
- `goal/step/action/this.Validate.cs:54-55`, `action/serializer/Reader.cs:153-154`, `formal/reader.cs:221,349` are the "is this an action slot?" name checks.
- `type/this.cs:893,907`: `type.Get` re-resolves through the list.
- `image/this.cs:162`, `image/this.Parse.cs:22`: the image builds a type from a name as a lookup key.
- `source.cs:231`: `.ToLowerInvariant()` is dead (the type constructor already lowercases, `type/this.cs:144`).

Chain for `source.cs:112`: `Peek` needs the name floor because "the empty kind reads IsText false" (its own comment, `source.cs:109-111`). That empty kind comes from `type/this.cs:145`, the constructor that makes a formatless kind from a name. The name check patches around the bare kind. The first demolition item is the root, and the other five fall out of it.

### F3. About 295 type lookups with no "my type" owner (stage 1, families). Local, but the volume is the signal.

Added by the diff: 151 `type.list[typeof(X)]`, 41 `type.list["name"]`, 90 `type.list[new type("name", …)]`. The `new type("text")` form builds a throwaway bare type, with the name-made kind of F2, only to find the real one (`type/list/this.cs:171-181`). `list/this.cs:845` repeats `context.App.type.list["list"].Create(…)` eight times in one file. `type.list[typeof(@this)]` appears 55 times in added lines: a value looking up its own class.

Cause: a value already holds its type (`Type`), but a pure `Create(raw)` or an operation that builds a sibling does not ask it. 13 `Create(object? raw)` cores remain (plan: 23, all to take the type). `Type.Make` exists (`type/this.cs`, the `_empty`/lift thunks).

Root-level fix: a creator takes its type from its argument or its receiver, and the literal lookups collapse to that. `this[type, context]` (`type/list/this.cs:147`) and `Kind(type, context)` (`:31`) take a `context` they never read.

### F4. List and dict (1b). Local.

The plan's shape is delivered. Remaining differences from the plan:
- The test `slot is Data d ? d.Peek() : slot` is repeated at 10 sites: `list/this.cs:190,611,964`; `list/this.Generic.cs:30,108`; `dict/this.cs:476`; `signature/this.Wire.cs:55`; `directory/this.cs:89,110`; `goal/child/this.cs:17`. A slot that answers its peeked value would remove all 10.
- `Output` makes a Data for every raw row (`list/this.cs:243-244`, `dict/this.cs:157-161`). The plan says a raw row is written as it is (`start.md:59`).
- The CLR exit copies (`list/this.cs:575-613`, `dict/this.cs:314-334`); the plan says "no copy, no walk" (`start.md:60`). Commit 49fd9084e is titled "a list lowered to C# is a list of its own", so this may have been ruled. *(unverified)*

### F7. Write-side leftovers. Medium.

- `kind.Write(value, writer)` is still a base virtual with 24 overrides (`type/kind/this.cs:317`). This is by decision 56 (a number's `short` writes `5m`). It does not match the plan's "the sync `Write` goes" or 107's "every `Write` becomes its type's `Output`". A stale plan claim, not a defect.
- The sync `Write` survives on `type` as an internal identity write (`type/this.cs:41`) and on `kind`. It feeds F5.
- `Fluid.cs` `ToStringValue` (`module/ui/code/Fluid.cs`, added ~lines 330-340) calls the async `Output` synchronously and throws `NotSupportedException` if it did not complete at once. Decision 57 ("a value becomes text only through Output; no ToString for display") forced a sync question into the async door. The throw is the hazard. *(Not exercised by me.)*
- `kind/this.cs:232` has `public new virtual bool IsSequence(object host)` hiding `item`'s property, with a comment "moves off the base in stage 3". It did not move.
- `kind/this.cs:349-353` carries an "OBPV — carry marked" comment: the author names `WriteReflected` as a verb+noun and a type-switch fork, and left it.

### F8. Members (stage 5). Medium.

- **A C# lowering fork moved, it did not go.** `property/this.cs:131-136`: `Activator.CreateInstance(slot.PropertyType, rows)` and `plang.Clr(slot.PropertyType, context)` run beside the plang read. The plan says `item.Set`'s lowering fallback goes (`start.md:213`). It is now in `Write`. A slot's C# type should be part of its property.
- **Literal-key arms beside the member list.** `item/this.cs:246-283`: `Member ?? On ?? Setting ?? Kept ?? NotFound`. `On` and `Setting` are literal-key checks that are not in `Type.Property`, so the builder is offered a different set of names than the runtime reads. The same shape recurs in the nodes (`node/this.cs:101-111`, `node/current/this.cs:34`, `node/root/this.cs:66`). `On` is the event door (locked) and the plan's sketch does not list it.
- **The headline feature is not delivered.** `item/this.cs:202-211`: a key the type lacks is added only for classes that override `Kept` (app, call, actor, module, goal, step); a plain value answers `NoAddedMember` (404). `set %user.blabla% = 12` is one of the three things the plan says the developer gets. The comment at `:208-209` says "Ingi's open question", a task reference in code.
- `item.Listed` throws `AmbiguousMember` (`item/this.cs:274-276`) while `Get` returns a Data. *(Whether every caller catches it is unverified.)*
- Dead after stage 5: `type/this.cs:650-661` (`init` setter, `_property`, `OwnProperty`). *(Grep found no initializer or override.)*

### F9. Root-cause smell: `variable.set` binds under the variable's name (5e2105bf4)

- **Symptom:** `set %x% = "v"` bound a Data named "Value" (the parameter's name). The fix adds `Binding(name)` at `module/variable/set.cs:37-39` and calls it at three sites (`:32,191,316`), plus a fourth `Value.Copy(name!)` at `:243`.
- **Producer:** `variable/list/this.cs:137` (`Bind`, doc at `:85`) stores a Data as given and treats its name as advisory. The root-write door `code/Variable.cs:54` hands a mis-named Data straight to it. A reference path in the same store already renames (`:107`), and the dict renames in `Put` (`dict/this.cs:108`). This one container does not.
- **Why this is symptom-patching:** tells 2 and 14. A producer contract (the store keeps the name) became a patch in each consumer, and the pair is half-fixed.
- **Root-level fix would look like:** the store's `Bind` (or the root write at `code/Variable.cs:54`) binds under the written name, with "one Data under two keys" as the explicit exception. `Binding` and its four call sites go.
- bf2648c69 (snapshot keyed by the store key) is the same cause, fixed correctly at one site. It would not have been needed with the producer fixed. *(Whether `foreach`, goal.call and http bind mis-named Data is unverified.)*

### F10. `app.Output` is a hand list; decision 89 says it should not be (1731b7cd2). Medium.

- **Symptom:** `app/this.cs:47-69`: 13 `writer.Name(…); await pair.Output(…)` lines, in hand-listed order, plus an `AsyncLocal<bool> _writing` loop guard.
- **Decision 89 says:** "Chosen by tags, not a hand list." `CLAUDE.md` says an item structure is written as its tagged bag by the base `Output`, with no override.
- **Cause:** commit dcd7eb293 ("a debug view of the app no longer walks into the asker's memory") and 1731b7cd2 patched the walk into the asker's memory (app → variable → the asker's memory → app). The producer of the loop is the app holding a memory that holds the app. A re-entrancy flag was added to cut it.
- **Also:** about 15 `app` members gained `[LlmBuilder]` marks in the same range (`AbsolutePath` among them, which `CLAUDE.md` calls C# plumbing; decision 143 notes it for Ingi). So the app's members are now listed twice, by tag and by hand.
- Locked: `app`'s members. 89 records "A locked change on app, Ingi's yes", so this was approved. The finding is that the approved shape was "tags", and the built shape is a hand list.

### F11. Work outside the plan's stages. Local to medium.

- **PLNG005 and `[Choice]`** (`e92111cc4`, decision 101, test speed): a second analyzer and an attribute, added by the tester to make the birth lazy. Not in the plan; Ingi said yes to "the four" in 101.
- **The type list became a cache host:** `_named`, `_classed`, `_full`, `_member` (`type/list/this.cs`) and `_empty` in `type/this.cs`, all from the tester's perf commits (328799fb1, dd899b865, 81159279d). They sit in the locked type system, and the tester's later work moved to `test-speed` (159). `type/this.cs` also gained `_list`, `_types`, lazy `??=` getters (`Shape`, `ConstructorSignature`) and `KindOfClass` (an `Activator.CreateInstance` over `MakeGenericType`), the last marked "goes with the implicit-conversion branch". *(Two indexes, `_named` and `_classed`, partly duplicate `Items()`; stored-twice risk unverified.)*
- **Fluid ordering operators** (`Ordered`, `module/ui/code/Fluid.cs`): a feature, not an item-doors stage. Decision 61 approved it in passing.
- `surface_golden.txt` re-pinned with 543 lines added and 394 removed across the stages. No one can read that diff as a review. Each re-pin cites a decision number; whether each was Ingi's yes is recorded in the log only for some *(unverified for the rest)*.

## 4. Decisions whose rule the code breaks

| Decision | Rule | Where the code breaks it |
|---|---|---|
| 89 | the app's members chosen by tags, not a hand list | `app/this.cs:47-69` |
| 107 / plan OBP table | pairs "looked up in one place (`data.Output`)", keyed by input kind | 103 sites; keyed by CLR class with a base walk (`kind/pair/list/this.cs:32-35`) |
| 117 | "no new thing where one exists" | the registry beside `IWriter`'s default-method pattern (`IWriter.cs:59,73,78`) |
| 107 | `Owns` and its 5 checks go; every `Write` becomes `Output` | 3 `Owns` sites remain, `kind.Write` (24) and `type.Write` remain |
| 158 | "writing is a use": wire reads itself | `wire/this.cs:64` unchanged (c2 held) |
| 33 / 14 / plan 1 | no value is made without its app; a type is never rebuilt from a name | F1, F2 |
| 138 | a key the type lacks is added | F8 (`NoAddedMember` on plain values) |
| 57 | a value becomes text only through `Output`, no `ToString` for display | satisfied, but via a sync-over-async throw in Fluid (F7) |
| 122 / 128 | `type.Write(IWriter)` becomes an internal non-virtual identity | satisfied (`type/this.cs:41`), but it keeps a second sync path alive (F5, step 5) |

## 5. Already tracked

I searched `Documentation/Runtime2/obp-cleanup.md` for the pairs, wire, name-floor, `app.Output`, `Binding` and `NoAddedMember` findings and found no entry for them. One entry is related to F1: "type.list.Property makes a bare type (item-doors, stage 5)" (line 304). I did not read the whole file. F1 to F11 are new except that one.

## 6. Order I would fix in

1. F6, build 158's c2. It is the decision the architect already reached, and it removes `Owns`, `Raw`, `Content` and `Decoded`.
2. F5, replace the pair registry with writer primitives. This needs an `IWriter` addition, so it goes to Ingi first. It removes 103 sites and PLNG006.
3. F2, then F1: the type constructor, then the name checks. The fallback waits for branch 50, but stop adding `until the implicit-conversion branch` births.
4. F9 and F10, the two patches that disagree with a producer or with a decision.
5. F3, F4, F8 and F7 are collapses that follow.
