# codeanalyzer v1 — kind-is-type (stages 0–3 + 767 + 789/790)

**Scope:** the landed stages 0–3 of decision 774 (*a kind is a type*), plus decisions 767 (a goal call's name never climbs) and 789/790 (a setting reads itself back from its record; no old-shape reader). Diff base **runtime2 `cf66e2d01`**. Stage 4+ (json navigator, born-unread, http/foreach/action-row producers, builder stage 5) is out of scope until the architect's go.

**Method:** every core type read in full (not only the diff); callers walked upstream; a compiler-backed hide sweep (`dotnet build PLang`); targeted greps for the model's own OBP gates (`is kind` forks, `kind<T>` uniformity, app-wide by-name kind callers, reflection sites). `Tools/ObpScan`'s Roslyn `MSBuildWorkspace` loader fails to load `PLang.csproj` here (the NuGet prune/vuln warnings abort the project-model load — a known tooling limitation, not a branch issue; `dotnet build` itself is clean), so the mechanized member sweep was done by full-read + grep + the real compiler instead.

**Carry discipline:** `Documentation/Runtime2/obp-cleanup.md` #1 parks `type/list/this.cs`'s registry verb surface (`BuildTypeEntries`, the name-resolution thicket, the `Get`-twins) for its own branch; its guardrail is only "don't grow it." Verified this branch adds no new name-resolution door and no new collection-proxy verb to that registry — the kind doors it adds are the branch's own work, not a sixth name→CLR door. Don't-raise items from the brief (confirmed present and as-described, not re-raised): `type.Construct` beside `Make`, bool's closed pair on a kind (782), the record reader's json round-trip in `Step.Set` (798), `template` on its base (782).

---

## Verdict: CLEAN — PASS

Stages 0–3, 767 and 789/790 are OBP-sound and merge-safe. The locked-core kind model (`kind<T> : kind : type`) is well-shaped: behaviour lives on the element, the one parent-fallback seam is `kind<T>`, and the app-wide kind search was removed without a replacement leaking in. The branch **compiles clean** (0 errors; PLNG001/002 at 0) and introduces **no new member hides**. No finding blocks the merge. Three non-blocking cleanups (one dead const, one stale doc, one reflection-per-call) are for the coder on a later pass; none is a surface change.

**Next bot: tester.**

---

## The model's own OBP gates — each verified

The plan (`test/plan/kind-is-type/start.md` "OBP validation") names the checks a kind-is-type review must make. Each, with what I found:

### 1. `kind<T> : kind : type` — what's different about a kind lives on the kind class; no `is kind` check outside it ✓
- `type/kind/this.Generic.cs`: `kind<T>` sets `Family` to T's entry once (`:21–22`), so a kind answers its own, else its parent's — the **one** seam. A concrete kind overrides only what is its own (`subclass`, `format`, `number/kind/int`).
- **No `is kind` behavioural fork outside `type/kind/`.** The only two `is …kind.@this` hits outside the folder — `size/this.cs:77`, `duration/this.cs:76` — are **non-null binds** on the indexer result (`list["size"].kind[asked.Name] is kind.@this standard`): the value re-expresses itself in the standard it was asked for, read from its **own** type's kind registry. Value-owns-its-op, not behaviour-by-subtype.

### 2. A kind falls back to its parent in one place; the parent never asks its kind back ✓
- The fallback is `kind<T>` (`Family = NameOf(T)`) and the type's fact hooks (`type/this.cs:667–694`): `Property`/`Values`/`Offers`/`IsClosed` read `OwnX ?? kind.OwnX ?? Family._field` — reading the **stored field** of the family entry, never the virtual (`:670–673` documents why: a kind's `Family` is its parent entry, so the field terminates; the member could loop). The parent never calls back into its kind.

### 3. Names on two levels — a kind is asked of its parent, never of the app; no second lookup by kind name ✓ (the crux)
- `type/list/this.cs` `Kind(string type, string? kind)` (`:47–53`) is the parent-scoped door: `Named[type]?.kind?[kind] ?? …Coin(kind)` — found under its parent, "never of the app (decision 774, ruling 2)."
- The app-wide `Held` (`:58–70`) was **kept by design** for MIME/extension only (ruling 2; decision 782: "the by-name and alias branch goes, MIME and extension stay"). It feeds `Mime`/`Format`/`Extension` (legitimately app-wide) and the single-arg `Kind(string)`.
- **I verified no NEW app-wide by-name kind search was introduced.** Live bare-name callers of the registry's single-arg `Kind(string)` / `HasKind`:
  - `type/item/text/kind/this.cs:25` (`_types.Kind(n)`) — the text format **coiner**, the one bare-name caller the architect explicitly deferred to stage 4 (decision 782: "the `*format` coiner waits for stage 4"). Known, not new.
  - `data/Operator.cs:171` (`types.HasKind(typeName)`) — the `is <bareword>` presence guard (distinguishes "unknown type" from "known, value isn't it"). **Pre-existing at the runtime2 base** (Operator.cs took only a mechanical `error.@this` rename on this branch; `HasKind` is at base `type/list/this.cs:106`). The actual IS-A answer is value-driven (`held.Is(typeName, types)`), not an app-wide resolve. Not introduced here.
  - `type/item/this.cs:472` `IsKind` uses `history.HasKind` — a different `HasKind` that walks the **value's own** type history, not the app. Correct shape.

### 4. A settings row read by its key — one fact names the class; the row doesn't store it twice (789/790) ✓
- `type/reader/Record.cs` + `type/reader/this.cs:103–119`: a type with no reader of its own reads its stored value as a record and `type.Construct(record)` makes it as `declared.ClrType` (789). The class comes from the **key** (`ctx.App.type.list[_type]`), never from a stored kind. Old-shape `{setting, kind:<path>}` rows decline **loudly** (the kind carried so `setting.Create` sees a kinded base and refuses — `Record.cs:33–49`), surfacing `SettingsUnreadable`; the decline rides as a `DeclinedException` that `source.Value` answers softly, never thrown to a courier. The start-up reader loop (`app/this.cs:313–321`) and the per-type setting reader are gone; no reader is kept for the old shape (790). The registry "knows nothing about settings" (`Registry.cs:332–334`).

### 5. `[Kinds]` + the base-class walk + `type.format` ✓ (as refined by 782)
- `type.format` and its filter are gone (demolition done). `[Kinds]`/`FamilyOf` were **kept by design** (decision 782) for the subclass families (input/screen/browser/query) whose kind objects are `kind.subclass<T>`; the walk is cached via `item/this.cs:555 IdentityOf` (`_identities`), so no reflection-per-call.

### 6. 767 — a name with a `..` segment refused before resolve ✓
- `goal/list/this.cs:64–67`: `name.Split('/','\\').Contains("..")` returns `context.Error(… "GoalNameClimbs", 400)` **before** any resolution that would fold the `..` away. An answer, not a throw. Exactly decision 767.

---

## Compiler-backed hide sweep (the 776 ruling)

`dotnet build PLang/PLang.csproj` → **Build succeeded, 0 errors**, PLNG001/002/003-blocking clean. ~20 `CS0108` hide warnings remain, but I verified they are **pre-existing, not introduced by this branch**:
- The kind-model-adjacent ones (`type/this.cs` `Template` `:141`, `Is(@this?)` `:634`, the static `Create` `:273`) **all exist at the runtime2 base** (`git show cf66e2d01:…` — same members at ~`:134/:261/:583`).
- The node/verb-duality hides (`channel/list` `Output`/`Debug`, `table` `Rows`, `archive` `Value`) are in files **unchanged** on this branch.
These are the pre-existing node/verb duality + the static-`ICreate` pattern, not the stage-1 navigable-shadow the 776 ruling fixed (that one — `setting.kind.Property` hiding `type.Property`, which lost prompt C — was resolved; settings moved out of kind entirely in stage 2). **No new hide was introduced**, which is what 776 promised.

---

## Findings (none blocking; all later-pass cleanup, no surface change)

| ID | smell | file:line | what | fix direction | blocks | surface |
|----|-------|-----------|------|---------------|--------|---------|
| **F1** | dead code (deletion test) | `type/this.cs:150–166` | `public const string TypeDescription` has no reader — only its own definition and a `<see cref>` in a doc comment reference it; type teaching now lives in `/system/type/<name>/type.description.md` (`:711,:718–721`). | delete the const (and the `<see cref>` to it); likely pre-existing, not a kind-is-type regression. | no | no |
| **F2** | history-narrating doc | `type/this.cs:21–26, 144–149` | the class-level doc narrates `type.list.@this.BuildTypeEntries` + `Promote` as the construction path; neither exists in the class anymore (construction now goes through the `(string, Type, list)` ctor `:800`). | rewrite the doc to the current ctor-fold; `BuildTypeEntries`'s eventual removal is parked (obp-cleanup #1). | no | no |
| **F3** | reflection-per-call + second door | `type/kind/subclass/this.cs:31–34` | `Offers(step)` reflects `GetProperty("Offer", Static\|FlattenHierarchy).GetValue(null)` on **every** call — a second hand-rolled reader of a static `Offer` fact that `type/this.cs:810` already reads **once** via `Declared<T>("Offer")` into `_offer`, and against this branch's own bind-once discipline (`Registry.cs:281–301` binds `Encode`/`Decode` once, "no reflection per write"). Build-time path (the decider's offer), so cost is modest. | bind the subclass's `Offer` once when the kind is made (it already holds `_class`), or route the subclass's offer through the type's folded `_offer`. | no | no |

### Observations (cited, deliberately not re-raised)
- `type/kind/this.cs:343–366` `WriteReflected` — a verb+noun name + a type-switch fork standing in for a value's own self-write; carries its own `OBPV` self-admission comment pointing at `todos.md 2026-07-09`. Pre-existing carry.
- `type/kind/this.cs:233` `new virtual bool IsSequence(object host)` — a documented `new` (coincidental collision with item's parameterless `IsSequence` property once `kind : item`; different signature, so no real conflict); slated to move off the base with the host verbs. Acknowledged.
- `type/this.cs:595` `ToString` writes `name<kind>` only for list/dict/choice — the tracked **F4.2**, a stage-6 demolition item. Cited.
- `size/this.cs:81`, `duration/this.cs` — the `(…Clr<object>() ?? value) is string s` parse-vs-convert failure idiom is the app-systems **F3.1** carry (pre-existing from the merged app-systems line), not introduced here.
- `data/Operator.cs:171` `types.HasKind(typeName)` — an app-wide by-name kind presence guard for the `is <bareword>` surface. Pre-existing (base), value-driven resolution; a reasonable candidate for `obp-cleanup.md` once ruling 2 is fully enforced, but out of this branch's diff and not a regression.

---

## Files read in full

`type/kind/{this.cs, this.Generic.cs, empty/this.cs, subclass/this.cs, format/this.cs}`, `type/this.cs`, `type/list/{this.cs, Registry.cs}`, `type/reader/{this.cs, Record.cs}`, `type/item/{ICreate.cs, this.cs (base virtuals, FamilyOf/IdentityOf, NameOf)}`, `goal/list/this.cs` (`Find`), `size/this.cs`, `duration/this.cs`. Breadth-swept the family `kind<T>` conversions (number, duration, size, hash, archive, conversation, wire, path/scheme, list, list/element — uniform; the non-`<T>` ones — `kind/{json,list,reflection,dict}`, `text/kind`, `bool/kind`, `template/kind`, `choice/set` — are the documented host-kind / backlog / facet exceptions).
