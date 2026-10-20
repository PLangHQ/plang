# item-doors audit: the auditor (design, correctness, plan against what was built)

Audited: origin/item-doors b91e019b5 against runtime2 at the merge base 1adfb9555. The 84 production commits are grouped by stage. I read the code, not the commit messages, and I checked the architect's log (decisions 1–159) as claims. I did not read the codeanalyzer's report.

## In plain words: is it worth keeping?

**Yes: keep the branch and fix it. Don't start over.** Most of what the plan said to remove is gone, and what replaced it is mostly the right shape:
- `Type` overrides: 48 → 3;
- the sync `Write` and the `IsLeaf` branch;
- `EmitsSchema` and Data's two `if`s on it;
- `FromJson` and its json round trip;
- `_hasWrapped` (list and dict);
- the reflection in `item.Get`;
- `IsDefault`'s decision by names.

The record door, the token reader, the per-class member list and "every Data born with its context" are sound foundations. Starting over from runtime2 would rebuild those same pieces.

**But Ingi's feeling is right about how the branch was steered, and the code shows it.** The pattern in nearly every major finding below is the same: **a probe over the tests was treated as the whole tree, and each gap was ruled at the spot without tracing past the tests.**
- Decision 139 classified the members plang reaches from a probe over the tests. The browser goals run under no test, so they were missed (F5).
- Decision 145 listed the no-type families from a test sweep. Families no test reaches were never listed (F1).
- Decision 79 called `KindOfClass` an OBP violation to shrink. No one noticed that this branch's first commit created it (F2).
- Decision 135 kept the json-bound readers "on purpose" and never recorded what that does to the new reader: any nested value crashes (F7).
- Decision 151 moved the plan's own 1b check ("a write through a read row reaches the list") to the backlog. That check is the silent write loss (F4).

Two of the plan's central shapes were also built differently, without a ruling that says so:
- **Stage 1's first demolition item** (no kind made from a name) became the standard lookup. 103 sites now build a throwaway type from names to look a type up (F2).
- **Stage 2's "one door"** became a second method beside `Output`, called at 64 sites and guarded by a lint (F3).

Ingi's two named areas:
- **Pairs and the door:** the suspicion holds. The door is a workaround shape (F3), and the class-keyed pairs needed an "anti-pair" for kind (F3c).
- **The wire's `Decoded(context)`:** this is a second lazy read beside `source.Value(data)`, with weaker error handling (F6). But it was already on runtime2; this branch only added a third caller. The live problem on the wire is the `Owns`/`Raw` paste at `wire/this.cs:64`, which decision 158 says must go and which is not built.

**Tests (my run at the tip, after `./dev.sh build`):**
- C#: Modules 1295, Types 887, Data 843, Generator 196 and Runtime 1078 green. Wire has 6 red, the known snapshot six.
- plang: 11 failed, 11 not loaded (no `.pr`), 1 needs a rebuild.
- These match the log's "known six" and "277/11".
- Against the base (1adfb9555): no new red. The plang failures went 15 → 11 (the four kind/strict tests went green). None of the critical or major findings below is caught by a test today.

## Verdict per stage

| stage | verdict | top findings |
|---|---|---|
| 1, a type is born whole (incl. group 8, the app's nodes) | **fix at the root** | F2: 103 lookups build a type from names (the plan's first demolition item, inverted); `KindOfClass` added by the branch. F1: the rebuild fallback still reached by families with an app in reach. F10: five demolition items kept quietly (name floor, Fluid `"item"`, `type.Get` re-resolve, action-slot name checks, the type ctor). Group 8 itself is clean. |
| born-whole families (145–148) | **keep, finish** | F1: the families no test reaches were never listed (input, screen, window, browser/cursor, clipboard, query and/or, timing, status, limit, http.entity, given). |
| 1b, a list's rows made when read | **fix at the root (pre-merge)** | F4 **critical**: a write through a read row is silently lost unless the raw row is exactly `List<object?>`/`Dictionary<string,object?>`; rows are made by `item.Create`, not the element type's `Make` (plan deviation). |
| 2, one door for writing | **fix at the root** | F3: the door is a second method, at 64 sites, guarded by PLNG006; a silent no-context fork. F6: `Owns`, `Raw` and `Content(kind)` survive (158's c2). Keep: `Kind` for `Format`, the byte door, the `IsLeaf` removal, `_formats` removal, json's `Value`. |
| 3, a type speaks for itself to a writer | **keep** | minor: plang's writer holds its view twice (S3-1); the signature stays a value layer, not on the record as the plan sketched (S3-2); the `IWriter` signature change on the locked surface was approved by the architect, not Ingi (S3-4). JSON Schema and the llm table were not built (deferred, recorded). |
| 4, a plang value read as tokens | **fix at the root** | F7: any nested dict or list read into a record slot, or a stored typed entry inside a dict, crashes with a plain .NET exception (contradicts 133 "refused with a reason"). The deletions and the reader's shape stay. |
| 5, a type's members through its own list | **keep, two fixes pre-merge** | F5: unmarked members that os goals navigate (`%!app.OsAbsolutePath%` in the browser goals) now silently stay as written. F8: `[LlmBuilder]` is now the runtime access gate; init-only members are writable, and `[Sensitive]` isn't checked. Deviation: an added member answers 404 on a plain value, against Ingi's "it should add", approved by the architect only (F12). |
| 6, settings | **not built** | Nothing of #6 is on the branch (149 waits on Ingi). The plan's status line still says "Nothing is built" (stale). |
| test speed (101–120) | **keep** | The caches are per-app and there are no new statics. Minor: PLNG005 covers only generator-built DLLs (F13); the pair registry runs its own assembly scan per kind, against 95/117 (in F3). |
| fixes outside the stages (broken step 93–98, awaits/identity 125–131, debug crash, `app.Output`, failure report, Fluid, strict coercion) | **keep, one fix pre-merge** | F9: a broken step's `Error` is rebuilt from strings, losing each cause's key (breaks 97/98 and CLAUDE.md "never crack an Error"). F11: Fluid hides a failed write as "waits on I/O". Scope creep: 93 (~700 lines of regenerated builder `.pr`) touched the builder's answer path between stages 3 and 4. |

## Findings

Each finding has its why-chain up to the cause, with file:line at HEAD. Severity follows my review rules: a silent failure is critical; a hidden error or a plain .NET exception for a program's condition is major.

### F4: critical, #1b: a write through a read list row is silently lost

- **Effect:** `set %rows[0].name% = "x"` answers Ok and changes nothing when the row is a C# container of any shape but two.
- **Chain:**
  1. `PLang/app/type/item/list/this.cs:140–142`: `Element` makes a fresh value from the slot through `item.Create(slot, context)` on every read, and writes nothing back (the typed list does the same through `T.Create`, `list/this.Generic.cs:91–95`).
  2. `PLang/app/type/item/this.cs:55–58`: `item.Create` aliases only `List<object?>` and `Dictionary<string, object?>`. Every other container is **copied** into a new backing (`:61–66` for `IDictionary`, e.g. `Dictionary<string,string>`; `:73–77` for any `IEnumerable`, e.g. `string[]`, `List<string>`). A `list<T>` row is always a new T.
  3. `PLang/app/data/this.Navigation.cs:52`, `:68`: the write lands in that copy, answers success, and `SetValue` rebinds only the throwaway row Data.
  4. The variable's hop chain never puts a container it walked through back into its parent (`PLang/app/type/item/variable/code/this.cs:70–82`).
  5. **Cause:** the plan's 1b "To check: a write through a row that was read reaches the list through `Set`" (`start.md:84`) was never built. Decision 151 moved it to the backlog on the premise "the row made from a raw dict shares it", which holds for one dict shape. Before 0af5a0aef, `Keep` wrote the made row back, so these writes worked on runtime2.
- **Test gap:** the only test (`SetTypeInferenceTests.cs:125`) uses the one aliased shape.
- **Decision broken:** 24 / plan 1b ("a write puts the value in its slot"). 151's stated premise is false for most shapes.
- **Also not as planned:** a row is made by `item.Create` (150), not "the list's element type's `Make`" (`start.md:57`). 150 chose this to avoid `type.Make` turning a raw string into a lazy source. That is a real reason, but it means "one door for a row" isn't the type's door.
- **Fix:** the hop chain writes each made container back through its owner after the last hop's `Set` (or the read row keeps its made value in its slot, which 24 forbids, so the write-back is the one). Reds: a `Dictionary<string,string>` row, a `string[]` row, a `list<T>` row.
- **Pre-merge.**

### F2: major, stage 1: a type is looked up by building a type from names; the plan's first demolition item is inverted

- **Effect:** about 100 sites build a throwaway type from names just to look a type up.
- **Chain:**
  1. The sites look like `context.App.type.list[new global::app.type.@this("path", "file"), context]`: `path/file/this.cs:75`, `:116`, `duration/this.cs:57`, `:72`, `number/this.cs:187`, `signature.cs:98`, `:101`, `text/serializer/Reader.cs:21`, `:23`, `item/serializer/json.cs:69`, `:166`, `:174`, `:214`, `source.cs:288`, … `new type.@this(` sites in `PLang/app` went 32 → 122.
  2. Each runs the public by-name constructor, `PLang/app/type/this.cs:142–145`, which **makes a kind from the name**: `new global::app.type.kind.@this(kind)`. That kind has no owner and no facts, and is thrown away.
  3. The list's lookup takes a type object as its key: `PLang/app/type/list/this.cs:147–148`, `this[type, context] => Full(type, …)`. The `context` it takes is never used (`Full`, `:171`, has no context). The private `Full` is the real by-names door; it mints yet another ownerless kind for an unknown name (`:176`), as `Kind(string, string?)` does (`:46`).
  4. **Cause:** the list has no public `(name, kind)` lookup. Decisions 10 and 11 ruled the fix: "no bare type object at all … the by-name constructor becomes the list's own; key-builders use a list door". It was never built. So stage 1's first demolition item (`start.md:338`, "type's constructor making a kind from a name") is not just undone: it is the branch's main lookup idiom.
- **Same root, worse:** `KindOfClass` (`type/this.cs:108–119`, reflection plus `Activator` in the type constructor, called at `:767`) **was added by this branch's first commit** (e66c2ae3f, "facts fix"). It is not on runtime2 (`git grep KindOfClass 1adfb9555` finds nothing).
  - Decision 79 later calls it "an OBP violation … stays only for values born without an app", as if inherited. The ruling treated a symptom the branch had just created.
  - The constructor chain makes `_kind` at `:145` and overwrites it at `:768`, `:787`, `:795`.
  - Values still stamp after `new`: `{ kind = kind }` at `number/this.cs:40`, `duration/this.cs:44`, `list/this.cs:179`.
- **Decisions broken:** 10, 11 (key-builders use a list door), 14 (no late stamp), 79 (a kind is born knowing its owner: these are ownerless), 49 ("we shouldn't be creating things twice").
- **Fix:** a `(name, kind, strict, template)` lookup on `type.list`, and make the by-name constructor the list's own (internal). Then `KindOfClass` and the stamps have nothing to serve except the operator fallback that decision 50 deferred.

### F3: major, stage 2: the "one door" is a second method beside `Output`, with a silent fork and a lint to hold it

- **(a) The door is not the method.**
  1. 64 call sites write `writer.Kind.pair.Output(member, writer, mode, context)`, handing the writer twice. `PLang/app/this.cs:54–69` alone has 12 identical lines.
  2. `item.Output` is the method a type overrides **and** the method a caller would call (public virtual, `PLang/app/type/item/this.cs:839–843`). The door is a separate method, `PLang/app/type/kind/pair/list/this.cs:30–37`: "the pair, else `value.Output`". That is a generic fallback beside per-type writes.
  3. Calling the natural method skips the pair, so PLNG006 (dab4a8459) makes calling a public method a build error. A lint forbidding a public method is the sign that the method has the wrong shape.
  4. **Cause:** there is no template method, a non-virtual public `Output` that is the door, with a protected virtual for the value's own write.
  - **Rule broken:** the plan says the pair is "looked up in one place, `data.Output`" (`start.md:93`, `:406`). It is looked up at 64 sites plus inside writers (`formal/writer.cs:104`, `wire/kind/plang/writer.cs:45`, `:69`).
  - PLNG006 enforces a real rule (members go through the pair), but it guards a workaround. With the template method it can be deleted.
  - The fix changes item's base virtual, so it is locked and needs Ingi.
- **(b) A silent no-context fork.**
  1. `pair/list/this.cs:33`: `if (context != null) Find(...)`.
  2. A write with no context before the kind's first write with one finds no pairs, and the value writes itself. A list in formal loses its argument rows; a type in plang loses its identity. No error.
  3. `node/root/this.cs:37` passes a null context through the door.
  4. **Cause:** the registry takes its type list from the caller's context instead of from its own kind (`type.Types`, `type/this.cs:836`).
  - `Find` also runs its own `assembly.GetTypes()` over every assembly, once per output kind (`:47–56`), against 95 ("the assembly read once") and 117 ("discovered the way readers are").
- **(c) Keyed by C# class, it needed an anti-pair.**
  1. `PLang/app/type/kind/format/plang.cs` exists only to call `value.Output` again.
  2. The reason: `kind` derives from `type` in C#, so the class walk (`pair/list/this.cs:34`) gives every kind the type's identity pair (`type/format/plang.cs`).
  3. The key moved type → class (119) after the type key failed, then was patched (121).
  4. **Cause:** "what an input kind looks like in an output kind" (plan #2) is dispatched by C# inheritance, not by plang's type and kind. The pair for `md` in llm (noted in 119) cannot be expressed.
- **Verdict on Ingi's question:** the pairs are a reasonable home for "a type looks different in another kind" (plan #3 asked for it). But the door shape is wrong, and the key is wrong. Fix both before more pairs are added.

### F5: major, stage 5: members os goals navigate were left unmarked; they now silently stay as written

- **Chain:**
  1. `os/system/browser/Window.goal:9` (and `Screen.goal:21`) builds `'--app=file://%!app.OsAbsolutePath%/system/browser/blank.html'`.
  2. `PLang/app/this.cs:138`: `OsAbsolutePath` carries no `[LlmBuilder]` (`AbsolutePath` at `:126` does).
  3. `item.Get` reads only the marked per-class list (`PLang/app/type/item/this.cs:246`, `:271`), so it answers NotFound. On runtime2 the reflection Get read any public member.
  4. An unset `%!x%` in text stays as written (`text/this.cs:173`), so Chromium gets a literal `%!app.OsAbsolutePath%`. No error.
  5. **Cause:** decisions 139 and 141 classified "what plang reaches" from a probe over the C# and plang tests. The browser goals run under no test, and the stays-as-written rule hides a miss. The rule in 139 itself ("a member gets `[LlmBuilder]` when a .goal in os/ … reaches it") was not applied by grep, only by probe.
- **Same:** `%goal.Cache%` (`BuildGoal/Start.goal:7`; `goal/this.cs:291`, unmarked).
- **Decision broken:** 139.
- **Fix (pre-merge):** mark them, then grep every `%…%` and liquid navigation in `os/**` (`.goal`, `.llm`, `.template`, `.html`, `.md`) against the per-class lists, not a probe.

### F6: major, stage 2 and the wire: `Owns`, `Raw` and `Content(kind)` survive; a lazy value has two reads

- **Chain:**
  1. `PLang/app/type/item/wire/this.cs:64`: `if (_reader.Owns(writer)) { writer.Raw((string)Raw); return; }`. A value asks whether its writer writes its stored format. This is the exact thing plan #2 removes.
  2. `Owns` is alive at `kind/this.cs:175`, `item/kind/json/this.cs:24`, `wire/kind/plang/this.cs:126` (each kind type-checks the other's class: `writer.Kind is json` / `is plang`).
  3. The "writer-side" checks delegate back to the kind: `IWriter.cs:65–69` and `json/writer.cs:50`, `if (kind.Owns(this))`. So 126's "content's and source's Owns checks are gone" is true only by moving them into `writer.Content` (`source.cs:376` → `IWriter.cs:67`). `IWriter.Raw(string)` stays (`IWriter.cs:55`, 4 implementations).
  4. **Cause:** `data/this.Output.cs` hands the door the unopened value; "writing is a use" (158 c2) isn't built.
- **The second read.**
  1. `wire.Decoded` (`wire/this.cs:53–54`, `kind.Parse ?? Read`) beside `source.Value(data)` (`source.cs:158–252`).
  2. `Decoded` skips `Value`'s template render, history, caching and its binding-named `MaterializeFailed` answer (`source.cs:242–251`, `:262–270`).
  3. It re-decodes on every `Output` (`:67`), `Clr` (`:75`), and the new `Read` (`:83`, added on this branch).
  4. A bad slice written out escapes as a `JsonException`.
  - The doc at `:51–52` claims it is "the same value Value() materializes to". That's a claim, not enforced.
  - `Decoded` predates the branch (it is on 1adfb9555).
- **Decision broken:** 158 (known and parked). Stage 2's demolition items "Owns and its 5 checks" and "`IWriter.Raw`" (`start.md:351`, `:354`) are not done, though 128 says "stage 2 done but (c2)".
- **Fix at the root, once:** open the value in `data.Output` (writing is a use). Then `Owns`, `Raw`, `Content(kind)` and `Decoded` go together. Signing re-writes the value (`Data.Digest`), so it needs a lossless round trip, not pasted bytes. Prove it with the byte-equal `.pr` and the signing tests before removing the paste.

### F7: major, stage 4: a nested value read into a record slot crashes with a plain .NET exception

- **Chain:**
  1. `property.Write` (`PLang/app/type/property/this.cs:128`) reads the value through `item.Read`, which walks it with the new token reader.
  2. `Record` (`type/reader/Record.cs:25`) hands an object to the dict reader, which reads each member through `json.Entry` (`dict/serializer/Reader.cs:26`).
  3. `json.Entry` reads a nested object or array as `RawEntry(reader.RawValue())` (`item/serializer/json.cs:54`; also `:35`, and the list reader's `list/serializer/Reader.cs:46`).
  4. The token reader refuses `RawValue` with `System.NotSupportedException` (`type/format/item/reader.cs:74`).
  5. `property.Write` catches only `AppException` (`property/this.cs:138`), so a plain .NET exception escapes.
  6. **Cause:** decision 135 kept the container readers json-bound ("laziness is deliberate"), so only a json reader can feed them. It never recorded that the new reader therefore breaks on any nesting. `ItemReaderTests` covers flat dicts only.
- **Second path, the same crash:**
  1. A stored typed entry inside a dict is a lazy wire (`type/this.cs:459`, `:464`).
  2. The walk peeks it as a leaf (`reader.cs:96`, `:41`) and asks its `Backing`, which calls `wire.Clr(Type)`.
  3. That throws `InvalidOperationException` (`wire/this.cs:77`).
  4. **Cause:** decision 133 ruled "an unread wire in a walked dict is refused with a reason". Only a top-level wire got the new `Read` override (`wire/this.cs:81`).
- **Decisions broken:** 133, and CLAUDE.md "a program's error answers".
- **Fix:** the container readers' nested arm reads tokens, not a DOM over bytes. At minimum, `RawValue` refuses with an `AppException` carrying a key. The walk opens a wire through its Data.

### F1: major, stage 1 and families: the rebuild fallback is still reached by values whose maker has an app

- **Chain:**
  1. `PLang/app/type/item/this.cs:382–383`: `Type => _type ?? (FamilyOf(GetType()) … new(GetType()))` rebuilds a bare type, reached through the parameterless constructor `:391`.
  2. Its doc (`:388–390`) says only the root, the type list and implicit-operator births reach it. False: classes with no typed constructor still chain to it.
     - abstract input (mouse/key), screen, browser, channel;
     - window, llm message, event.on, test timing;
     - `clipboard/this.cs:20`, `browser/type/cursor/this.cs:13`, `query/where/condition/and/this.cs:11`, `or/this.cs:12`, `llm/type/conversation/this.cs:25`, status `:30`, llm limit `:29`, http entity `:28`, `given/this.cs:17`.
  3. Two `Type` overrides build a new type on every read (`error/this.cs:28`, `event/this.cs:108`; error is deferred to 50, event is Ingi's).
  4. **Cause:** 145's family list came from a probe over a test sweep, so families no test makes were never listed (146–148 don't name input, screen, window, cursor, clipboard, the query conditions, timing). 106 closed stage 1 on that basis.
- **Decisions broken:** 14, 33 ("every value holds the app's object"), 50 (the fallback was kept only for operator births).
- **Fix:** grep every item subclass for a constructor that doesn't take `type.@this`, not a probe. Make the fallback private to the operator path, marked for 50's branch.

### F9: major, outside the stages (93–98): a broken step's Error is cracked into strings and rebuilt

- **Chain:**
  1. `PLang/app/goal/step/list/this.cs:228`: each persisted step error is `new error(w, "StepRefused", 400)` per string.
  2. The strings come from `Refuse(int, string)` (`:109`). Callers flatten real Errors into them: `cause.Message` (`:192`, `:206`), `number.Message` (`:202`), `declined.Message` (`:211`), each prefixed `step i ("text") —`.
  3. **Cause:** the refusal carrier `SortedDictionary<int, List<string>>` (`:108`) predates the branch. 93 promoted it into an Error that is written into the `.pr` and shown at `goal.Start` (`goal/this.cs:389`, `:411–419`) without changing the carrier.
- **Effect:** each cause loses its own key (`ElseWithoutIf`, the decline keys) and becomes `StepRefused`, and the message repeats the step.
- **Decisions broken:** 97 ("never crack an Error to rebuild it"), 98 ("the step error's message names the step only"), CLAUDE.md "Never crack a caught Error".
- **Fix (small, pre-merge):** `Refuse(int, error.@this)`. Real errors ride whole; a plain reason gets its own key.

### F8: minor (guard pre-merge), stage 5: `[LlmBuilder]` is now the runtime read and write gate

- **Chain:**
  1. `type/list/this.cs:298` filters each class's members by `[LlmBuilder]`.
  2. `item.Get`/`Set` read only that list (`item/this.cs:202`, `:246`).
  3. `property.Write` writes any member with `CanWrite` (`property/this.cs:119`), and an init-only setter counts, so 12 marked `{ get; init; }` members are writable from plang (error `HelpfulLinks`/`Data`/`list`, warning `Key`/`Message`).
  4. `Get` hand-codes `on` and `setting` by name (`item/this.cs:246–252`, `:282–283`) because the one attribute can't say "navigable but untaught".
  5. **Cause:** one attribute now carries three facts: teach it, read it, write it.
- `identity.PrivateKey`: verified not navigable (no mark, `identity.cs:33–34`). But nothing checks `[Sensitive]`, so a future teaching mark re-opens it. `View.cs:51`'s doc still says `%MyIdentity.PrivateKey%` works.
- **Fix:** `Member(clr)` refuses `[Sensitive]` and treats init-only members as own (`OwnMember`).

### F10: major, stage 1: five demolition items kept quietly

All five are still in the code at HEAD. Stage 1 was declared closed (106) with no ruling that keeps them, against the plan's "never keep a member quietly" (`start.md:335`):
- text's name floor, `source.cs:112`: `Type.kind.IsText || Type.Name.ToLowerInvariant() is "text"`;
- Fluid's `format is { Name: "item" }`, `Fluid.cs:194`;
- `type.Get`'s re-resolve, `type/this.cs:893`;
- the action-slot name checks, `goal/step/action/this.Validate.cs:54–55`, `formal/reader.cs:221`, `:349`, `serializer/Reader.cs:153–154`;
- the type constructor's kind from a name (F2).

The cause is in the floor's own comment (`source.cs:110–111`): the registered text type's kind "reads IsText false until" it loads its format. The type is still not born with its facts, so the patches around it are still needed. That is stage 1's purpose, unfinished.

### F11: major (hidden error), outside the stages: Fluid reports a failed write as "waits on I/O"

- **Chain:**
  1. `PLang/app/module/ui/code/Fluid.cs:329–331` (and `:364–365`): `if (!output.IsCompletedSuccessfully) throw new NotSupportedException("… waits on I/O …")`.
  2. A write that already **faulted** with an `AppException` is reported as pending, and its real error is dropped.
  3. A truly pending ValueTask is abandoned while writing into the `MemoryStream` disposed by `using` (`:325`).
  4. The catch (`:199–213`) turns the plain exception into `ServiceError` "RenderError" 500.
  5. **Cause:** liquid's `ToStringValue` has no async form (a real third-party boundary, 130). The check conflates "faulted" with "pending" and throws a plain .NET exception for a program's condition.
- New on this branch (not in 1adfb9555).
- **Fix:** a faulted task re-raises its own error; a pending one is refused with a keyed `AppException`.

### F12: minor, stage 5: the added-member rule is decided by an HTTP status, against Ingi's ruling

- **Chain:**
  1. Plan #4, Ingi: a key the type doesn't have "should add".
  2. Built: a plain value answers `NoAddedMember` 404 (`item.Set`).
  3. `data/this.Navigation.cs:56`: any 404 from a write means "add it to the binding's Properties" (`:59–62`), else `CannotSetChild` (`:64–66`). So a 404 from any other cause (the clr kind) is also read as "add it".
  4. **Cause:** where an added member lives is open (Ingi). 138 ruled an interim answer itself.
- Logged as the architect's, not Ingi's. Flag for him; not pre-merge.

### F13: minor, test speed: PLNG005 covers only generator-built DLLs

- **Chain:**
  1. `Enlist` holds only classes with `[Choice]` (`type/list/Registry.cs:329`), also for DLLs loaded at run (`:182` → `:214`, from `module/code/this.Load.cs:19`).
  2. The check is a generator diagnostic (`Plng005.cs:36–48`), so a module DLL built without the generator gets a `choice<T>` with no set and no reader. Silently.
- 108's "covers user module DLLs too" holds only for generator-built ones.
- **Fix:** `Add(Assembly)` refuses an unmarked `choice<T>` at load.

### Smaller (minor or nit, deferred)

- **S3-1:** plang's writer keeps its view in a field (`writer.cs:77`, for `Sensitive`) and also takes `mode` per call (`:37`, `:54`). Cause: `IWriter.Sensitive(string)` takes no mode (`IWriter.cs:73`).
- **S3-2:** the signature stays a value layer with a type branch in Encode (`wire/kind/plang/this.cs:50`, `data.Peek() is signature`), not on the record as the plan sketched (`start.md:128`). Not ruled.
- **S3-4:** `BeginRecord`/`EndRecord` became `ValueTask …(record, mode, context, layer)` on locked `IWriter`. 129 "takes it as within #3". Under the locked-core rule it is Ingi's.
- **F14:** `kind.Write(item, IWriter)` (56) is untyped: number's kind downcasts (`number/kind/this.cs:29–30`), duration's kind uses `is` (`duration/kind/this.cs:21–22`), and the base throws `NotSupportedException` (`kind/this.cs:317–318`). Better as `kind<T>.Write(T, IWriter)`. `number.Kind`'s cast (`number/this.cs:32–34`) is marked "goes with the kinds item", which landed (925787b46), so the marker is stale.
- **F15:** `size` keeps `_kind` beside `Type.kind` (`size/this.cs:19`, `:53`, `:57`), the flat copy 55–57 removed from duration. The family wasn't completed.
- **F16:** the `type` entry holds the list in `_types` while every type now holds `_list` (`type/this.cs:682`, `:820`, `:832`, `:865`). The doc at `:861–863` ("every other type knows no list") is false. 146's `Type.Types` reverses 33 without a ruling.
- **F17:** three loop guards now (data's resolution guard, text's `Rendering`, app's AsyncLocal `_writing`, `app/this.cs:26`, `:45–50`). 89 asked for "one door for the loop".
- **F18:** the strict imprint is checked in four places (`image/this.cs:165–167`, `module/variable/set.cs:51`, `:217`, `:298–307`), and "this type made strict" is built by hand at three (`image/this.cs:161–162`, `pick/list/this.cs:685`, `:695–696`). 94's watch-list trigger has fired: a type member for it is a locked addition, a proposal to Ingi.
- **F19:** `property.Read` reflects on every read (`property/this.cs:96–106`, `GetParameters`, `GetMethod("AsTask")`, `GetProperty("Result")`). Plan #4: reflection only where property is built (`type/list/this.cs:310–318` already knows the member returns a task).
- **F20:** `AmbiguousMember` is thrown from a Data-returning `Get` (`item/this.cs:273–276`) and recomputed per navigation. It belongs where `Member(clr)` builds the list.
- **F21:** `IsDefault` never matches for a type with no `Order` (`property/this.cs:193` → `item/this.cs:179–180`): path and separator defaults are always written out.
- **F22:** stale docs: `item/this.cs:214–243` (three summaries describe reflection that's gone), `dict/this.cs:111–112` ("same-ref"), `list/this.Generic.cs:4` ("typed view"), the plan's status line "Nothing is built" (`start.md:3`).
- **F23:** the surface golden pins no `app.Attributes.*` class, so a new attribute (locked) can't fail the gate (`ChoiceAttribute` passed unpinned; 101 approved it).

## Decisions whose rule the code breaks

| # | rule | broken at | finding |
|---|---|---|---|
| 10, 11 | no bare type object; key-builders use a list door | `type/this.cs:142–145`, `type/list/this.cs:147`, ~100 call sites | F2 |
| 14 | no late stamp | `number/this.cs:40`, `duration/this.cs:44`, `list/this.cs:179` | F2 |
| 24 / plan 1b | a write puts the value in its slot | `list/this.cs:140–142`, `item/this.cs:61–77` | F4 |
| 33 | every value holds the app's type object | `item/this.cs:383`, the unlisted families | F1 |
| 49 | don't create things twice | `type/this.cs:145` → `:768`; `number.Create` births twice (`number/this.cs:178`, `:186–188`) | F2 |
| 50 | the fallback only for operator births | the families in F1 | F1 |
| 79 | a kind is born knowing its owner | ownerless kinds minted per lookup (`type/this.cs:145`, `list/this.cs:46`, `:176`) | F2 |
| 89 | one door for the loop | three guards | F17 |
| 94 | one door for the strict imprint | four checks | F18 |
| 95, 117 | the assembly read once; pairs discovered as readers are | `pair/list/this.cs:47–56` | F3b |
| 97, 98 | never crack an Error; the step error names the step only | `step/list/this.cs:108–228` | F9 |
| 106 | stage 1 closed | five demolition items kept | F10 |
| 126, 128 | "Owns checks gone", "stage 2 done but (c2)" | `IWriter.cs:67`, `json/writer.cs:50` | F6 |
| 133 | an unread wire in a walk is refused with a reason | `reader.cs:41`/`:96` → `wire/this.cs:77` | F7 |
| 139 | a member is marked when an os/ goal reaches it | `app/this.cs:138` | F5 |
| 151 | a write through a read row reaches the list | `Navigation.cs:52–68` | F4 |
| 158 | a wire written is a use; Owns/Raw go | `wire/this.cs:64` (known, parked) | F6 |
| plan #2/#3 | the pair looked up in one place, `data.Output` | 64 sites | F3a |
| plan #4 (Ingi) | an unknown key is added | `NoAddedMember` 404 | F12 |

## Tests

- **Tip (b91e019b5), after `./dev.sh build`:**
  - C#: Modules 1295 green, Types 887 green, Data 843 green, Generator 196 green, Runtime 1078 green, Wire 6 red of 542 (the snapshot six).
  - plang `cd test && plang --test`: 11 failed (condition operators, file exists, `IsType`, `MissingIsEmpty`, kind-is-type csv/json/separator), 11 with no `.pr`, 1 needing a rebuild.
- **Base (1adfb9555), in its own worktree:**
  - C#: Modules 1287, Types 852, Data 842, Generator 188 and Runtime 1068 green; Wire the same 6 red of 533.
  - plang: 15 failed, 11 with no `.pr`, 1 needing a rebuild.
- **Tip against base:** no new red. The plang failures went 15 → 11 (the four kind/strict tests went green, as stage 1 promised). The only new entries are the plan's own `start` goal stages.
- **Coverage:** no test covers F4, F5, F7 (nested), F9's keys or F11. Each needs a red first when it's fixed.

## What I did not do

- I didn't run a real build (`plang build`) after stage 4, which the plan asks the coder to report (`start.md:389`); I found no record that the coder ran one.
- I didn't verify the byte-equality of every `.pr` (the log reports none moved).
