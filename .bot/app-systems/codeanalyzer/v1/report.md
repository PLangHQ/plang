# codeanalyzer v1 — app-systems pre-merge OBP scan

Scope, method, and carry-discipline in `plan.md`. Range: `0ea5a4b94..app-systems`. Read-only; a fix touching a locked surface is a **proposal**, never an edit.

Each finding carries: smell name · file:line · what's wrong · fix direction · **blocks / carry** · **surface change: yes/no**.

---

## Area 1 — data and the wire (`PLang/app/data/`, `data/Wire.cs`)

**Diff:** +1,843 −3,658 across 34 files. Net deletion — a large simplification. Gone this branch: `Compare.cs`, `IEquatableValue`, `IListLeaf`, `ILoadable`, `IOrderableValue`, `ScalarComparer`, `ShouldExit`, `TString`, `TypedValueNode`, `WireLocal`, `this.Load.cs`, `this.Normalize.cs` (−276), `this.Reconstruct.cs` (−312), and 586 lines off `Wire.cs`. New: `Comparison.cs`, `Operator.cs`, `OutputException.cs`, `reader/`, `schema/` (registry + `ISchemaReader` + `signature`), `this.Output.cs`.

**Tooling:** `git diff` per file + full read of every touched type + `ObpScan app.data`. ObpScan's MSBuild emits NU-prune/vuln load warnings but renders the member tables.

### Verdict: CLEAN (no merge blockers)

The new surface is OBP-exemplary and the weighted smells present are all already carried. Detail below.

### New surface — reviewed, OBP-correct (no finding)

- **`schema/this.cs` — the `@schema` reader registry.** A `Dictionary<string, ISchemaReader>` keyed by `Schema`, each reader owning its behaviour (`data` reads the envelope, `signature` verifies + peels). Selection on the registry, behaviour on the element — the correct shape, not a fork. Unknown schema throws loudly (registry miss). Mirrors `app.type.reader.@this`.
- **`Operator.cs` — the condition engine (relocated into `data`).** One `Registry` dict mapping operator string → async evaluator; negation derived once via `Negated(positive)` (no flag flips a step's logic); equality/ordering routed through the value's own `Compare` → `Comparison`. "Adding an operator = one entry." No per-type switch, no fork. `IsType` (:163-186) follows the ref, narrows, answers from the chain — this is the **correct** side of the carried json-clr-leak (the bug lives in the json kind, not here).
- **`reader/this.cs` — the `@schema:data` reader.** Streams `{name,type,value,properties}` off the `IReader`; `switch(key)` is legitimate wire-key reading. Lazy `source` value slot, no DOM. Clean.
- **`Comparison.cs` — the sign-free comparison enum + `IncomparableException` + `Invert`.** Enum-not-int is deliberate and documented (a magic `-2` would satisfy `<0`). `IncomparableException` is **used** (thrown/caught across the sync `IComparer` boundary in `type/item/list/this.cs:530/942`), not dead. ObpScan's "MISPLACED" on `Invert` is a false positive (extension on the enum it extends).
- **`this.Output.cs` — "Data writes itself".** Single async pass, each item emits its own form; bounded self-ref recursion guard throws a typed error (not a stack overflow). View-mode read (`mode == View.Store`), not a value type-switch. Clean.

### Weighted smells present — all CARRIED (cite, not re-raised)

1. **exception-as-answer — the set-child bridge.** `this.Navigation.cs:51-63`: `item.Set` throws `NotSupportedException`, `data.Set` catches it to answer `CannotSetChild`. → **carried:** next-branch.md "The set-child bridge (59's remainder)" — removing it changes item's locked surface (Ingi's). **Carry. Surface change: yes.**
2. **reader throws its refusal.** `schema/signature.cs:83,89` throw `DeclinedException`; `reader/this.cs:64,82,102` throw `JsonException`/`UntypedValueException`/`PrFormatOutdatedException` for a stale/invalid `.pr`. → **carried:** next-branch.md "A serializer Reader throws its refusal (714)" — `IReader` can't answer an error; changing it is a serializer-surface change (Ingi's). **Carry. Surface change: yes.**
3. **clr leak on structured json.** `Operator.IsType` encodes the ruled promise (read→narrow→answer) that `type/item/kind/json/this.cs` violates by holding parsed json as `clr(JsonElement)`. → **carried:** next-branch.md "The {item, json} door (717)". **Carry. Surface change: yes** (the json kind parsing into native dict/list).

### Minor findings (cleanup — do not block the merge)

- **F1.1 — dead const (deletion test).** `this.Output.cs:21` `private const int MaxNormalizeDepth = 128;` is referenced nowhere (grep: 1 hit, the declaration). It mirrors a cap for the `this.Normalize.cs` tree that was **deleted this branch**; the live Output path uses `MaxResolveDepth` (:27). Delete lines 16-21 — nothing breaks. **Carry (trivial; leave for coder). Surface change: no.**
- **F1.2 — borderline: `IsDataMarked` placement.** `this.cs:56` is a `static bool IsDataMarked(JsonElement)` on `data.@this`, called only by the json leaf serializer (`item/serializer/json.cs:94,187`). It's a legitimate wire-shape probe (does this JSON carry a `@schema` marker), and a leaf serializer is allowed to read JSON — but it reads as a stray helper that could live on the json serializer beside its one caller. **Carry (note, not a fix). Surface change: no.**
- **F1.3 — borderline: `JsonString.ToJson` parse-ladder.** `JsonString.cs:30-91` drives control flow through 9 `catch (JsonException){}` arms (try strict → fix controls → quote barewords → slice prose → repair truncation → strip commas), returning a clean `Error` only when all fail. This is exception-as-control-flow, but it is a **relocated** pre-existing utility (`App.Utils.Json`, stage 27), not new on this branch, and `JsonNode.Parse` offers no `TryParse`. Not exception-as-answer (each arm is "try next strategy," not a yes/no on a semantic result). **Carry (no action; noted for completeness). Surface change: no.**

### Noise explicitly cleared (so a re-scan doesn't re-chase it)

- ObpScan name-flags on `QuoteBarewordKeys` / `AppendUnmatchedCloses` / `FixJsonStringValues` / `StripTrailingCommas` (JsonString private helpers), `CompareElements/Objects/Arrays` (this.Diff.cs recursive JsonElement walkers — mechanical, localized, diff-util by design), `ReadCore`, `SetValueDirect`: accurate descriptive names / inherent length, not smells.
- `ValidateKind` / `RequireStrictKind` / `CheckStrictKind` (IKindValidatable, IStrictKindEnforcer): **unchanged pre-branch** — not in the `0ea5a4b94..app-systems` diff. Out of scope.
- `Properties.EnsureSupportedValue` (:142-162) primitive `is` list: a wire-boundary accept/throw gate (not a behavioural fork), honest about its best-effort limits inline.
- `code/Default.cs:61` `catch (ArgumentException)`: translates `Regex`'s unavoidable compile-throw into a typed `InvalidPattern` error — a boundary translation, not a swallowed yes/no.

---

## Area 2 — channel and stream (`PLang/app/channel/`)

**Diff:** +529 −2,284 across 24 files. The entire `channel/serializer/` subtree (Json, Text, filters, writers, the plang serializer — ~1,700 lines) was **deleted**, folded into the type serializer (area 5). Grown: `type/stream/this.cs` (+191), `type/goal/this.cs` (+123), `type/test/this.cs` (+47 new). Rewritten: `channel/this.cs`, `channel/list/this.cs`.

**Tooling:** `git diff` per file + full read of `channel/this.cs`, `list/this.cs`, `type/{stream,goal,file}/this.cs` + grep for the weighted smells + `ObpScan`.

### Verdict: CLEAN (no merge blockers)

The dominant feature here — a repeated `catch (Exception ex) when (ex is not fatal)` across every channel — reads like a generic-catch smell but is the opposite: a **deliberate, consistent I/O-boundary** that converts a .NET transport throw into a typed `Error` result while *preserving* the specific ones.

### Reviewed, correct (no finding)

- **The channel I/O boundary** (`this.cs:140-148,165-170,189-194`; `stream:135,197`; `goal:151`). Each wraps the leaf transport call and excludes exactly the right set: `AppException` (a program's own error — `%var%` not set) passes through with its key and travels to the action; `NullReferenceException`/`OOM`/`StackOverflow` rethrow; `stream:197` also excludes `OperationCanceledException` so a cancel isn't reported as an `AskError`. Transport failures become `ServiceError{Exception=ex}`. This is "convert at the boundary, don't mask the specific" — not the masking smell.
- **`list/this.cs`** — the user-facing `Remove` returns proper error results (`ChannelNotFound` 404, `ChannelInvariantViolation` 400). The throwing `this[string]` indexer (`:141-143`) is the documented hard-miss accessor, paired with the nullable `Get(name)` — the standard nullable-get / throwing-indexer pair, not a fork. `this[System.Type]` selects a channel by its concrete class (infrastructure lookup — "what a channel IS beside its name"), acceptable.
- **`type/goal/this.cs`** — the `_executing` AsyncLocal reentrancy guard (a goal body writing to its own channel can't loop) matches the `Available` contract; `%message%`/`%where%` bindings are built coherently. Clean.
- **`ResolveEncoding` / `WriteText` / `Read`** throws (`this.cs:216,239`) are wiring/boundary translations (no context, or `Encoding.GetEncoding`'s unavoidable `ArgumentException` → a named `InvalidOperationException`), same class as `code/Default.cs`'s Regex translation.

### Minor findings (do not block the merge)

- **F2.1 — borderline clr read at a leaf.** `type/stream/this.cs:160` `question?.Clr<string>()` pulls a raw CLR string from the prompt value only to run `IsNullOrEmpty` (is there a prompt to write). It's a leaf and the value doesn't travel, but the idiomatic form is the value's own emptiness/truthiness, not a `.Clr<string>()` decompose. **Carry (note). Surface change: no.**
- **F2.2 — borderline redundant inner catch (stored-twice shape).** The base `WriteAsync`/`ReadAsync`/`AskAsync` (`this.cs:132-197`) already own the transport-throw→Error boundary around the abstract `Write`/`Read`/`Ask`. `stream`'s concrete `Read`/`Ask` (`:135,197`) and `goal`'s `Start` wrap *again* with the same conversion, so the base's catch is effectively dead for those kinds. Defensible (the concrete gives a kind-specific message/key), but it is the same boundary written twice. Low confidence. **Carry (note). Surface change: no.**
- **F2.3 — possible legacy surface.** `type/stream/this.cs:247-258 ReadAllBytesAsync`, commented "Convenience surface kept for v1 callers," throws `InvalidOperationException` on `!CanRead` (`:250`) — internal, caught by the base. If the v1 callers are gone it's a deletion candidate; flagged for the architect to confirm, not asserted. **Carry (note). Surface change: no.**

## Area 3 — the core value types (`type/item/{text,number,bool,list,dict,path,datetime,duration}/`)

**Diff:** +7,294 across 87 files, almost all additions — the native value-model itself. path +2,498, list +1,389, number +1,322, text +859, dict +531.

**Tooling:** grep for the weighted smells across all 8 type trees + full read of the seams most at risk of a fork (number Ladder, path scheme dispatch, list `Clr(target)`, each type's `Create` path) + the catch census + `ObpScan`.

### Verdict: CLEAN (no merge blockers)

Every seam that could have been a per-type fork is a single-owner or registry design instead:

- **`number/this.Ladder.cs` — the one owner of cross-kind promotion.** Keyed by kind NAME (the kind's identity); "int overflows to long" lives here, not on int or long. `Mint`/`CategoryOf`/`Narrow` switch on the kind token because this IS the one place that knows the order. Not a behaviour fork — the single documented home for a cross-type relation (the Comparison-boundary pattern).
- **`path/scheme/this.cs` — scheme dispatch by registry+factory.** A `scheme.@this` IS a `type.kind` holding a factory; file/http/https registered per-App, `code.load` adds more. `path.Resolve` (`path/this.cs:113-121`) parses the scheme, looks up `kind[name]`, mints via the factory. No `if scheme=="file"` fork. Unknown scheme → `SchemeNotRegistered` (AppException carried with a FixSuggestion), converted to a Data error at the `Create` boundary (`path/this.cs:94-103`).
- **`list/this.cs Clr(target)` (`:653-699`) — the single CLR-interop door.** Branches by the *target's* CLR shape (array / `List<T>` / `IReadOnlyList<T>` ctor / record), each row lowering ITSELF via its own `Clr`. The reflection (`MakeGenericType`, `Activator`, `GetConstructor`) is inherent interop — you cannot build a `List<T>` for a runtime `T` otherwise. This is NOT the carried member-read reflection (reading goal/step/app members); it's value→CLR conversion at the leaf. Acceptable.
- **The catch census is clean:** `number/this.Arithmetic.cs:46-50` translate each specific .NET arithmetic throw (`DivideByZero`, `Overflow`, …) into a typed-key AppException (operators can't return an error); `path/file/this.Operations.cs` and `path/http/this.cs` filter on specific `IOException`/`IsNetworkError`; `list/this.Generic.cs:102` is the **carried** reader-throws catch-site (next-branch "714").

### Findings

- **F3.1 — stored twice (systemic cleanup).** The parse-vs-convert failure classification
  `(((value as item.@this)?.Clr<object>() ?? value) is string s) ? <parse-failed> : <convert-failed>`
  is written **9 times**, once in each value type's `Create(value, declared, data)`: `bool:79`, `duration:80`, `datetime`, `date`, `time`, `size`, `image`, `guid`, `list:110`. Same rule (was I handed a string to parse, or the wrong type?) in nine files. Fix direction *(corrected per architect)*: not a helper on `item.@this` (that would itself be an addition to item's locked surface) — the **source answers its own failure face**: a text says "cannot parse 'x' as T", anything else says "cannot convert Y to T", and T supplies only its hint. That removes the nine copies without adding a member. **Carry (cleanup). Surface change: no** (the classifying behaviour moves onto the value the convert already has; no new member on item). *Plang that reaches it:* `- set %flag% as bool = "banana"` (any failed coercion of a string literal to a scalar type lands one of the nine messages).
- **F3.2 — borderline (same class as F1.3).** `dict/this.cs:84` `catch (JsonException) { return null; }` answers "is this text a dict?" by swallowing a parse throw. STJ has no non-throwing parse and the exception means exactly what the null means ("not JSON" = "not a dict"), so it is control-flow-by-exception, not a masked error. Noted, not a blocker. Separately, `dict/this.cs:89` narrows a `clr(JsonElement)` source into a dict — a **consumer accommodating the carried json-clr-leak** (next-branch "the {item, json} door, 717"), not a new finding. **Carry (note). Surface change: no.**

## Area 4 — the type system (`type/this.cs`, `type/list/`, `type/kind/`, `type/property/`, `type/item/this.cs`)

**Diff:** `type/this.cs` +692 −311, `type/item/this.cs` +941 (item's apex), `type/kind/` +503, `type/list/` +620 −377, `type/property/` +348. `type/convert/this.cs` −121 (deleted — the `OwnedClr` ownership moved onto each type's `this.Owns.cs`, seen in area 3).

**Tooling:** grep for forks/is-X/reflection + the cross-file fork census (registry-constructs, signature) + full read of the flagged sites + `ObpScan`.

### Verdict: CLEAN (no merge blockers)

The spine's reflection is all the **carried** member-read/catalog machinery, and the one genuinely new shape issue works today. Nothing here blocks the merge.

### Carried (cite, not re-raised)

- **Member-read reflection — 673.** `type/this.cs:514,518,540,734-805` (type descriptor built from a CLR class: `Resolve`/`Internal`/`Create`/`Default` statics, property/method walk), `type/item/this.cs:261-290` incl. `:272-276` (`item.Get` default — `AsTask`/`Result` unwrap), `type/list/Registry.cs:149,154,335`, `type/property/list/this.cs:69-77`, `type/property/this.cs:43-47`. All are "what members does this type have?" → **carried:** next-branch "Avoid reflection; one door to a type's members (673)."
- **Reader throws its refusal — 714.** `type/this.cs:321` catches `DeclinedException` → error — **carried** (next-branch names this exact site).
- **clr skip in the catalog.** `type/property/list/this.cs:86` `property.Type.Name is "clr"` continue — skipping clr carriers from the readable catalog → **carried** clr/kind-is-type family (711).

### Findings

- **F4.1 — fork by name/type: `IsDefault` (`type/property/this.cs:118-138`). NEW (not on any carry list).** The "is this written value the property's default?" test switches four ways in one body: `Default is bool` (:131), `Type.Name == "variable"` (:132, `%`-trim), number double-parse (:134), else raw string compare (:137). Four per-type equality rules keyed by name/CLR-type — a type-switch doing equality the value should own (`Operator.Equal` already routes equality through the value's own `Compare`). **Risk:** a new type whose written form needs normalization silently falls to the raw string arm and misreports "not default" (same-shape-second-site / silent divergence). **Fix direction:** the value answers whether it equals the property's default (through its own equality/`Compare`), so a new type needs no new arm here. **Carry** (frozen-branch cleanup — it works today, not a correctness regression). **Surface change: yes** (the value answering default-equality adds a member on item/property — Ingi's call). *Plang reaching it:* build-time omission of a param equal to its default (e.g. a step whose value equals the declared default) — this is the comparison that decides whether the param is persisted.
- **F4.2 — generic-display by name list: `type/this.cs:547`.** `ToString()` renders `name<kind>` only for `Name == "list" || "dict" || "choice"`, else bare `Name` — whether a type shows its kind in angle brackets is a hardcoded name set, not a property the type answers. → **carried:** next-branch "a kind is a type (711)" — the generic-vs-sub-kind distinction ("a generic's element `list<path>` already is one"; the written `.pr` form `{name, kind}`) is exactly what 711 resolves. Concrete site noted; not re-raised. **Surface change: yes** (711 is Ingi's).

### Noise cleared

- `type/this.cs:218 IsNull => Name == "null"`: the null sentinel's identity predicate, not a behaviour fork.
- `type/list/Registry.cs:64` (registry mints `text.@this` of registered names): a single listing mint, not a domain-value factory on the wrong owner.

## Area 5 — the serializer (`type/format/`, `type/serializer/`, `type/item/wire/`)

**Diff:** +671, all new — the interface spine (`IReader` +111, `IWriter` +130, `IOutput`, `format/value/reader.cs` +80), `serializer/Reader.cs` +55, `item/wire/` (+258: the `wire.@this` carrier, `template.cs`, the `plang` wire kind).

**Tooling:** grep for value-type forks / clr / late-stamp + full read of `wire/kind/plang/this.cs`, `format/value/reader.cs`, `wire/this.cs`, `serializer/Reader.cs` + liveness check on the `Normalize` mechanism.

### Verdict: CLEAN (no merge blockers)

The CLAUDE.md invariants hold, verified in code:
- **One wire serializer.** `wire/kind/plang/this.cs` is the single `application/plang` (+`+json`) Encode/Decode. No parallel "Envelope"/"Wrapper" type.
- **Each value writes itself.** `Encode` (`:50-55`) delegates to `data.Output(writer, …, layer:true)` / `sig.Output(…)` — no central writer switching on value type. The value slot is each item's own `Output`.
- **Sign-if-missing inside the boundary.** `Encode:38-49` signs a Data crossing without a signature (asker's actor), owners don't call `EnsureSigned()` at egress — exactly as CLAUDE.md "Sign-if-missing — the converter does it" states. Out refuses unsigned content on Decode (`:95-97`).
- `format/value/reader.cs` is a scalar-only `IReader`; its `Peek()` switch classifies its own single value's token, and the structural pulls throw a "wrong reader, use a parser" guard (by design) — not a fork.

### Carried (cite, not re-raised) — reader throws its refusal (714)

`serializer/Reader.cs:50` (`FormatException` → `DeclinedException` "TemplateInvalid") and `wire/kind/plang/this.cs:101` (catches `DeclinedException` → error) — both **named on next-branch's 714 list**. The other catches here (`plang/this.cs:60-65,102-106`, `format/value/reader.cs:43`) are the wire encode/decode boundary converting STJ/`NotSupported`/`FormatException` throws into typed Data errors — the same boundary pattern cleared in areas 1-2.

### Finding

- **F5.1 — stale doc narrating a deleted mechanism.** `format/IWriter.cs:7,11,126` still describes the serialize pipeline as "`Normalize()` walks the in-memory value… Dispatched by `Normalize`." **The `Normalize` tree was deleted this branch** (`data/this.Normalize.cs` −276, area 1); the live mechanism is each value writing itself via `item.Output`. A grep confirms no live `Normalize` dispatcher remains (only an unrelated `tag` trim and error strings). The interface contract now misdescribes the architecture — a reader looks for a `Normalize` dispatcher that isn't there. Sibling instance: `type/item/kind/json/writer.cs:200`'s error string "Normalize is missing a case for this type." Fix: update the `IWriter` doc (and the json writer's message) to describe the self-writing `Output` path. **Carry (doc cleanup; a reviewer bot leaves the write to docs/coder). Surface change: no.**

### F5.2 — fork + stored twice: the sync `Value(object)` write door beside self-writing `Output` (added after architect review; my Area-5 grep was scoped to the three brief paths and missed `type/item/kind/json/`)

**Correction to the verdict above:** "each value writes itself, no switch on value type" holds for `Encode`/`Output`, but a **second write door** sits beside it and does type-switch. The serializer as a whole is **not** fork-free.

- **Fork (two paths for one operation).** Writing a value to the wire has two doors: the async `item.Output(writer, mode, context)` (each value writes itself — the live path `Encode` and `WriteReflected` use) and the sync `IWriter.Value(object)` + `item.Write(IWriter)` (`type/item/this.cs:838`, 61 overrides). The code itself names the second one legacy: `item/this.cs:917` routes a Data through `Output` "NOT through writer.Value (sync, peels to **the OLD Peek().Write path**)." Still reached with plang values at `type/kind/this.cs:340`, `type/item/this.cs:937`, `type/item/signature/this.cs:221`, `type/item/kind/dict/this.cs:45`.
- **Stored twice.** `json/writer.cs Value(object)` (`:131-203`) has explicit `dict` (`:154`) and `list` (`:171`) arms that write the object/array wire shape — while `dict`/`list` **also** write that shape themselves (`dict/this.cs:175`, `list/this.cs:299 Output`). One wire shape, two writers that can drift.
- **Witness tell.** `Value(object)`'s own comment (`:181-187`) states "the writer never type-switches per type (OBP Rule 9)" — directly above the `dict`/`list` arms that do exactly that.
- **Fix direction:** one write door. Everything routes through the value's own self-write (`Output`); `Value(object)`'s `dict`/`list`/`item` arms collapse into "the value writes itself," and the sync `Write`/`Value(object)` door retires once its four callers move to `Output`.
- **Blocks the merge: no.** Both paths agree today (tests green — an agreeing fork is rot, not a bug). **Surface change: yes** (`IWriter.Value` and `item.Write` are locked core). This is the strongest shape finding of the run and is **not** on next-branch or obp-cleanup — it belongs on next-branch as a new systemic locked-core fork (I flag it; the write is architect/docs').

---

*Areas 6-8 pending (call/variable/events, module registry/app, builder pipeline).*
