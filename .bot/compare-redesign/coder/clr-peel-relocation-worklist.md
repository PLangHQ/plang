# Ask the value, don't reach past it — worklist (OBP)

**Author:** architect. **For:** coder, later. **Status:** worklist, no code written. **You own the final shape** — every signature and split below is a suggestion; if the real code wants a different seam, take it and tell me why.

Two parts, one principle: a caller must not reach *past* a plang value to do its work. Part 1 — don't peel a value to CLR to hand it to a plang-owned collaborator. Part 2 — don't fish a parameter out of the raw bag and pattern-match its peeked value; ask the carrier.

---

# Part 1 — `.Clr` peel relocation (the owner decides)

## The rule

A caller must not peel a plang value to CLR in order to hand it to a collaborator that is itself plang-owned. Pass the whole plang value (or the whole `Data`) and let the collaborator decide what it needs. The peel belongs at the collaborator's own BCL boundary, owned by the collaborator — not pre-decided one frame up.

This is an OBP win, not a style change: `RemoveAsync` owns the decision of what to do with a name; `set.cs` shouldn't pre-decide by peeling to `string` and handing it a key. Objects own their responsibility.

The peel never disappears — plang value types bottom out on the BCL (`text` wraps `string`, `dict` wraps `Dictionary<string,object?>`, `number` wraps `int`/`long`). Relocating a peel pushes it **down** to the layer that actually calls the BCL, where it's one line the owner controls, instead of **up** in every caller.

## The test (apply per site)

Look at what the peeled value is handed to:

1. A method on a plang-owned object (registry, entity, internal service) → **RELOCATE**: change that method to take the plang type; move the peel inside it.
2. A BCL / 3rd-party API directly (`HttpClient`, `Encoding`, sqlite, `System.IO`, a CLR `Dictionary` key/compare, `System.Security.Cryptography`) → **KEEP**: this site is the real boundary.
3. A C# string operation the value should own (`string.IsNullOrEmpty`, `.Split`, `.Trim`, `string.Join`) → **MOVE THE OPERATION** onto the value type (`text.IsBlank()`, `text.Split(text)`, `list.Join(text)`); peel inside that method.
4. The conversion machinery itself (`*.Convert.cs`, `Normalize`, serializers, the `Lift`/`.Clr` doors) → **OUT OF SCOPE**: this is the CLR↔plang perimeter you already accept; don't touch it.

## The registry-key lesson (read before "fixing" any dictionary)

When the collaborator is a `ConcurrentDictionary<string, X>` keyed by a name (channel, timer, module, event id), **relocate the peel into the registry, but keep the dictionary keyed by `string`.** Do **not** rekey it to `Dictionary<text.@this, X>` — that needs a custom `IEqualityComparer<text>` whose body peels to `string` anyway, plus a per-key allocation. The dictionary key is a genuine CLR boundary. The win is only that the registry method takes `text` and peels on its own line; the floor stays `string`.

A registry that already peels at its own dict line is **already correct** — e.g. `callstack/call/tag/this.cs:31` does `_entries[key.Clr<string>()]` inside the tag store. Leave those.

## RELOCATE — grouped by the collaborator whose signature changes

The unit of work is the collaborator: change its signature to take the plang type, peel once inside at its BCL boundary, then fix the call sites that fed it raw.

### Module registry — `app/module/this.cs`
Current: `bool Contains(string module)`, `bool Contains(string module, string actionName)`, `bool Remove(string module)`
Proposed: take `text` (peel at the internal dict line).
Call sites:
- `module/module/remove.cs:18` — `app.Module.Contains((await Name.Value())!.Clr<string>()!)`
- `module/module/remove.cs:22` — `app.Module.Remove((await Name.Value())!.Clr<string>()!)`

### Events registry — `app/event/list/this.cs`
Current: `bool Unregister(string id)`
Proposed: `Unregister(text id)`
Call sites:
- `module/event/remove.cs:13` — `Context.Events.Unregister((await EventId.Value())!.Clr<string>()!)`

### Event subscription/registration — `module/event/on.cs` → its registration API
Current: registration built from four raw pattern strings.
Proposed: the subscription type holds `text` patterns; matching peels at the match boundary (or matches plang-side — coder to look at how patterns are compared at fire time).
Call sites:
- `module/event/on.cs:52` goalNamePattern, `:53` stepPattern, `:54` actionPattern, `:58` channelName

### Code registry — `app/module/code/this.cs`
Current: `Remove(System.Type providerType, string name)`, `SetDefault(System.Type providerType, string name)`
Proposed: take `text name`. (The `typeName` peel that resolves `providerType` is a registry name→Type lookup — could also take `text`.)
Call sites:
- `module/code/setDefault.cs:20` typeName, `:25` name
- `module/code/remove.cs:20` typeName, `:25` name
- `module/code/list.cs:17` typeName

### Channel registry — `app/channel/list/this.cs`
Current: `RemoveAsync(string name)`, `Register(channel)`, `Get(string)`, `this[string]` over `ConcurrentDictionary<string, channel.@this>`.
Proposed: `RemoveAsync(text)` etc., peel at the dict line (keep dict keyed by string — see lesson above). `ResolveDirection` should take `text`. `Mime`/`Encoding` on the channel object feed HTTP headers downstream, so the channel could hold `text` and peel at the header boundary — lower priority.
Call sites:
- `module/channel/set.cs:34` name → RemoveAsync + Register + new goal channel
- `module/channel/set.cs:48` direction → `ResolveDirection`
- `module/channel/set.cs:57` Mime, `:58` Encoding → channel object
- `module/channel/remove.cs:20` name → RemoveAsync

### Settings facade — `app/module/settings/this.cs`
Current: facade `Set(string key, data)` (and Get/Remove) over `Sqlite` (`Sqlite.cs` Get/Set/Remove take `(string table, string key)`).
Proposed: the **facade** takes `text key`. The `Sqlite` layer is the real boundary — keep it `string`, it peels at `AddWithValue` (`Sqlite.cs:233` stays, see KEEP).
Call sites:
- `module/settings/get.cs:17`, `module/settings/set.cs:17`, `module/settings/remove.cs:16` — key

### path.List — `app/type/path/file/this.Operations.cs:168`, `app/type/path/http/this.cs:213`
Current: `List(string pattern, bool recursive)`
Proposed: `List(text pattern, @bool recursive)` — peel at the System.IO glob boundary inside.
Call sites:
- `module/test/discover.cs:52` — `root.List((await Pattern.Value())!.Clr<string>()!, ...)`
- `module/file/list.cs:20` — `(await Path.Value())!.List((await Pattern.Value())!.Clr<string>()!, ...)`

### Identity service — `module/identity/code/Default.cs` (densest cluster, 13 sites)
Current: `ResolveIdentityAsync`, `GenerateIdentity`, `LoadAsync` take `string name`; `identity.Name = string`; several `string.IsNullOrWhiteSpace` checks.
Proposed: the service methods take `text`; the blank-checks become `text.IsBlank()` (see operations bucket); `identity.Name` is the stored identity token — boundary-ish, lower priority.
Call sites: `:29, :43, :48, :52, :77, :97, :116, :146, :149, :155, :161, :167, :186`

### Mock registry — `module/mock/intercept.cs`
Current: registration built from a raw `Pattern` string.
Proposed: registration takes `text pattern`.
Call sites: `:22`, `:65`

### Timer registry — `module/timer/start.cs`
Current: `name` is a timer dict key; `scope` feeds `new TimerEntry(..., string)`.
Proposed: registry takes `text` (peel at dict line, keep string key); `TimerEntry.Scope` could be `text`. Lower priority.
Call sites: `:17` name, `:18` scope

### test.report formatter selection — `module/test/report.cs:31`
Current: `format` string selects a formatter.
Proposed: formatter selection by `text`.

## MOVE THE OPERATION onto the value type

These peel to run a C# string/collection op the value should own. Add the method to `text`/`list` (thin convert→BCL→wrap), peel inside.

- `module/list/split.cs:20` — `(...).Clr<string>()!.Split(...)` → `text.Split(text)` (and/or a `list` op)
- `module/list/join.cs:20` — `string.Join((sep).Clr<string>(), strings)` → `list.Join(text sep)`
- `module/list/where.cs:32` (Field), `module/list/any.cs:22` (Key), `module/list/group.cs:15` (Key) — name a field/key to extract per item → a navigation op taking `text`
- `module/identity/code/Default.cs:43, :146`; `module/channel/set.cs:35`; `channel/type/stream/this.cs:110` — `string.IsNullOrWhiteSpace/IsNullOrEmpty(...)` → `text.IsBlank()` / `Data.IsEmpty()` (already exists — ask the value, don't peel)
- `module/builder/code/Default.cs:1018` — `.Clr<string>()!.AsSpan().Trim()` → `text.Trim()`

## KEEP — genuine BCL / 3rd-party boundary at this site (do NOT churn)

- `this.cs:390`, `module/http/code/Default.cs:313` — `Encoding.UTF8.GetString(byte[])`
- `module/http/code/Default.cs:989, :1031` — `new ByteArrayContent(byte[])` (HttpClient); `:472` http channel payload bytes
- the rest of `module/http/code/Default.cs` (`:74, :75, :77, :79, :156, :158, :197, :199, :201, :508, :588`) — the module calls HttpClient throughout; url/contentType/encoding peels feed HttpClient. The one internal helper is `ResolveUrl` (could take `text`/`url`) — minor, optional.
- `module/settings/Sqlite.cs:233` — sqlite `AddWithValue`
- `type/path/file/this.Operations.cs:237` — `System.IO.File.WriteAllTextAsync` (inside the path verb surface — exempt zone)
- `module/crypto/code/Default.cs:108` — algorithm name → `System.Security.Cryptography`
- `module/llm/code/OpenAi.cs:1055` — serialize to OpenAI (3rd-party)
- `callstack/call/tag/this.cs:31` — already peels at its own dict line (correct shape)
- `Utils/CommandLineParser.cs:140, :141` — CLI JSON → CLR (process perimeter)

## path self-peels — KEEP with explanation (the §4 "smell" that isn't one here)

`type/path/this.cs:103, :136, :142, :155, :179, :180, :182, :276` — `path` peels its own `_location` text to feed `PathHelper` string parsing (`GetExtension`/`GetFileName`/…). This is `path` being its own leaf at the path-string-parsing boundary; `PathHelper` bottoms out at `System.IO.Path` string ops. Owner peels at its boundary — correct. `Absolute`/`Relative`/`Raw`/`Extension`/`FileName` are cached (`??=`). Leave as-is.

## OUT OF SCOPE — value-model internal machinery (the perimeter already accepted)

The conversion doors and serializer/parser — exactly the layer excluded from the direction. No work:
- All `type/*/this.Convert.cs` hooks
- `type/item/this.cs:304, :317, :351`; `type/list/this.cs:480`; `type/dict/this.cs:312`; `type/this.cs:171`; `type/number/this.cs:96`
- `type/catalog/Conversion.cs:155, :172, :255`; `data/this.Normalize.cs:94`; `data/this.cs:1038`; `type/this.cs:134` (ClrType resolution)
- `variable/list/this.cs:646`; `variable/navigator/Object.cs:34`; `variable/this.cs:102`; `tester/this.cs:64`; `config/this.cs:94`; `actor/this.cs:23`
- `test/discover.cs:293`; `builder/code/Default.cs:80`; `llm/query.cs:122`; `error/throw.cs:39`
- All 9 `Lift(` inbound sites (`data/this.cs:281, :400, :801`; `type/this.cs:412, :415`; `type/item/computed.cs:56`; `type/item/serializer/json.cs:79`; `type/item/source.cs:112`; `module/assert/code/Default.cs:161`) — the inbound door is already funnelled and disciplined.

## Sequencing notes

- Action-record parameters are **already** plang (`Data<text> Name`, etc.) — the source generator wires them. This worklist is handler-body + collaborator-signature work, not signature-attribute work.
- Pre-1.0: no backward-compat shims, no feature flags. Change the signature, fix the callers, done.
- Ratchet PLNG003 file-by-file as collaborators flip to plang returns/params; never sit at a long-lived half-migrated state (the raw↔plang ping-pong is worse than either pure state).
- Each collaborator is an independent unit — pick any, do signature + internal peel + its call sites in one commit.

---

# Part 2 — build-time parameter access (same principle: ask the carrier, don't reach past it)

This is the sibling of the `.Clr` peel: there a caller peels a value to hand CLR to a plang owner; here a build hook reaches into the raw parameter bag and pattern-matches the peeked value instead of asking the `Data` that holds it. Same fix shape — stop opening the package, ask it.

## The smell

```csharp
// query.Build() today
var schema = __action?.Parameters?.FirstOrDefault(p =>
    string.Equals(p.Name, "Schema", System.StringComparison.OrdinalIgnoreCase))?.Peek();
if (schema is not (null or global::app.type.@null.@this)
    && !(schema is global::app.type.text.@this st
         && (st.Clr<string>() is "" or null || st.HasHoles)))
    return Task.FromResult(data.@this.Ok("json"));
```

Two faults: (1) it re-fishes a parameter out of the raw `List<Data>` by string name when a typed property exists; (2) it `Peek()`s the value and pattern-matches `is null or @null.@this` + empty + holes, instead of asking the carrier one question.

`Peek()` never returns C# null — it hands back the `@null.@this` singleton (`data/this.cs:416`). So the `null` arm of `is null or @null.@this` only ever fires from the `?.` on a missing param; ask the `Data` and that disjunction can't arise. This is OBP smell #7's cousin (*only leaves touch `Data.Value`*): a build hook is not a leaf, so it must not open the value.

## Two facts that make it removable (both verified)

1. **The typed property is live at Build().** `SetAction` ends with `await __ResolveParameters()` (`PLang.Generators/Emission/Action/this.cs:367`) — the build setup resolves params into the backing fields exactly like Run. So `this.Schema` / `this.Format` are populated; the raw-bag fishing isn't needed for access, it's just old.
2. **The carrier answers every clause.** On `app.data.@this`:
   - `IsEmpty()` (`data/this.cs:622`) = `!IsInitialized || _type.IsEmpty()` — folds absent + null citizen + empty-string (the instance knows its own emptiness). Replaces `is null`, `is @null.@this`, and `"" / null`.
   - `HasVariableReference` (`data/this.cs:150`) = `_type?.Template != null` — "still a builder template with holes." Replaces `HasHoles` / `Contains('%')`.
   - `HasValue` (`data/this.cs:628`) = present and not the null citizen (note: `""` and `false` are present — use `IsEmpty()` when empty-string must count as empty).

## The clean shape

```csharp
public async Task<data.@this> Build()
{
    if (!await Schema.IsEmpty() && !Schema.HasVariableReference)
        return data.@this.Ok("json");

    if (!await Format.IsEmpty() && !Format.HasVariableReference)
        return data.@this.Ok(Format.Peek().ToString());

    return data.@this.Ok();
}
```

No fishing, no `Peek()`-then-type-match, no `is null or @null.@this`.

## Sanctioned build-time vocabulary (use these; don't grow new ones)

- Presence / emptiness → `Data.IsEmpty()` (async, folds empty-string) or `Data.HasValue` (sync, presence only).
- Still-a-template / unresolved holes → `Data.HasVariableReference`.
- The held value, no resolution → `Data.Peek()` (returns the null citizen, never C# null — test `.IsNull` on it only when you already hold the value; prefer asking the `Data`).
- Foreign-action param lookup → `action.GetParameter(name, ctx)` (`action/this.cs:225`) — the canonical lookup, **and it checks `Defaults`**; hand-rolled `Parameters.FirstOrDefault(string.Equals)` misses that fallback.

Banned in build hooks: `__action.Parameters.FirstOrDefault(p => string.Equals(p.Name, …))?.Peek()`, and `value is null or @null.@this` / `is text && Clr<string>() is "" or null` value-type pattern-matching to mean "absent."

## Worklist

### Case A — an action reading its OWN params in `Build()` → use the typed property + carrier surface
- `module/llm/query.cs:118` (Schema), `:125` (Format)
- `module/file/read.cs:120` (Path) — `this.Path` + `HasVariableReference` replaces `raw.Contains('%')`
- Static `ValidateBuild(List<data.@this> parameters)` hooks can't use `this.` (they get the list), but should still ask the carrier (`IsEmpty`/`HasValue`/`HasVariableReference`), not `Peek()`-and-null-soup. `query.cs:16` already half does this — finish the style there and in `variable/set.cs` ValidateBuild.

### Case B — code inspecting a FOREIGN action's params by name → funnel through `GetParameter`
- `module/goal/getTypes.cs:156` (`ParamByName` — reimplements `GetParameter`, misses `Defaults`)
- `module/http/HttpBuildHelpers.cs:16` (`InferTypeFromUrl`)
- (the generator's own emission re-rolls the lookup at `Emission/Action/this.cs:167, :218, :246` — generated code, separate concern; leave unless you're already in that file)

## Caveat to pin with a test (don't assume)

That a `%var%`-valued param leaves `HasVariableReference == true` on the typed property at Build time — i.e. the template stamp survives `AsCanonical` when no binding exists. Tracing says `AsCanonical` hands back the literal/reference param at build, so it should hold, but lock it: `set %schema% = {…}` (literal) ⇒ format inferred `json`; `schema: %dyn%` ⇒ no inference. Same for `file.read` Path.

## Case C — no-carrier slot reads (the same `is null or @null` idiom, but no `Data` to ask)

`GoalCall.FromSlots` (`goal/GoalCall.cs:100`, `:114`) repeats `n is null or app.type.@null.@this ? "" : n.ToString()` — twice in one method (name + prPath). It's NOT fixable by "ask the carrier" because the source is a raw `object?` slot off a deserialized native shape, not a `Data`. The null+@null fold is doing real work: the slot can be C# null OR the `@null.@this` singleton (whose `ToString()` is the literal `"null"`).

The fix is a tiny shared helper so the idiom stops being open-coded:

```csharp
// on app.type.item.@this — "is this object the nothing value, in either form?"
public static bool IsNothing(object? v) => v is null or global::app.type.@null.@this;
```

Then `FromSlots` reads `IsNothing(n) ? "" : n.ToString()`. Same call replaces the `prPath` guard at `:114`. This is the canonical answer anywhere a raw `object?` (not a `Data`) must be tested for absence — it keeps the null/@null knowledge in one place instead of re-spelled per site.

## Low-priority cleanup — `peek-cast-or-default` (style, not a bug)

`(X.Peek() as ConcreteType)?.Method() ?? default` in leaf handlers that already hold a typed `Data<T>` param:
- `module/goal/return.cs:24` — `(Depth.Peek() as number.@this)?.ToInt32() ?? 0`
- `module/timeout/after.cs:22` — `(Ms.Peek() as number.@this)?.ToInt32() ?? 0`
- `module/http/code/Default.cs:230` — `(action.Default.Peek() as @bool.@this)?.Value ?? false`

Read the typed door instead (`await Depth.Value()` / the `Data<number>` accessor). The hard cast + `?? default` silently swallows a type mismatch; since `Data<T>` already pins the type at resolution the cast effectively never fails and the `??` only covers "absent", so this is consistency, not a correctness hole. Sweep only if already in the file.

## Audit scope (so you know the boundary)

This is the whole population — the smell is contained, not systemic. Across `PLang/app/**`: 6 raw-bag fish sites (all in Case A/B), 2 `is null or @null.@this` sites (`query.cs` + `GoalCall`), 3 peek-cast-or-default sites. Courier value-opening (cache `ILoadable`, `Wire` signature, variable-store `DynamicData`) was checked and is OBP-correct — don't "fix" it.

---

# Part 3 — Convert / cast / ToString (same principle: hold plang values, ask them)

Crawl of `Convert.ToXXX` (25), explicit primitive casts (~60), `ChangeType`/`IConvertible` (27), `ToString()` (240). The conversion surface mirrors the `.Clr` surface: the value-model **core is already disciplined**; the wins are in the **courier/container layers that still hold `object?` or raw CLR**. A, B and D below are one disease — a container holding `object?`/raw-CLR forces coercion or reflection at every read; hold plang values and the read is just "ask the value."

## A — `object?` bags that reimplement coercion (highest value, structural)

Two typed getters coerce with `Convert.ChangeType` *because the bag stores raw `object?`*:
- `data/Properties.cs:82` — `Properties.Get<T>`: `if (v is T t) return t; return (T)Convert.ChangeType(v, typeof(T))`
- `config/this.cs:57` — `Config.Get<T>`: same `ChangeType`, plus a hand-rolled enum branch (`Enum.TryParse` / `Enum.ToObject`)

If the bag held plang values (`Data` / `item.@this`), `Get<T>(name)` is one door — `bag[name].As<T>()` / `.Clr<T>()` — culture-correct, and the enum case is what the `choice` type already owns. `Config.Get<T>` is reimplementing `number`/`choice`/`text` conversion by hand. Structural; it ripples to every `Set(string, object?)` site, so do it as its own unit.

## B — `dict.Get("k")?.Peek()?.ToString()` field extraction (fragile, recurring ~8 sites)

`goal/GoalCall.cs:124, :179`; `module/goal/getTypes.cs:168`; `data/this.cs:823`; `module/test/discover.cs:298` — extract a string field by stringifying the peeked value via the universal `ToString()`. This is the failure documented at `GoalCall.cs:114`: a *structured* value's `ToString()` yields `"Dictionary\`2…"` → bogus `/Dictionary…` path. The typed accessor already exists — `dict.Get<T>` (`type/dict/this.cs:176`). Use `nd.Get<text>("name")` (converts/validates) instead of `nd.Get("name")?.Peek()?.ToString()` (blind stringify).

## C — `Convert.ToInt32(plangValue)` (trivial, one site)

`module/list/indexof.cs:19` — `Convert.ToInt32((await key.Value()))`. `key.Value()` is already a `number`; ask it `.ToInt32()`.

## D — reflection-for-Count casts (symptom of raw collections)

`variable/navigator/List.cs:115`, `variable/list/this.cs:434` — `(int)countProp.GetValue(collection)` reflects over a *raw CLR* collection to read `Count`. Vanishes if collections are always plang `list`/`dict` (ask `list.Count`). Don't fix in isolation — fold into the collections-go-plang direction in `clr-plang-boundary.md`; cross-linked here so it isn't lost.

## KEEP — legitimate, do NOT churn

- BCL encoding boundaries: `Convert.ToBase64String` / `ToHexString` / `XmlConvert.ToTimeSpan` (crypto, hashing, ISO8601), `settings/Sqlite.cs:277` `ToInt64(ExecuteScalar())`
- The value owning its own form: `binary`/`archive`/`duration` `ToString()`, `crypto/type/hash.ToBase64()`
- The number tower: `type/number/this.Tower.cs`, `this.cs:171-181`, the number JSON/Default serializers — value-model core managing CLR numerics
- The sanctioned `ChangeType` leaf: `type/catalog/Conversion.cs:486`, `type/number/this.Convert.cs:84`, `type/item/this.cs:343` — already isolated and documented as the one allowed instance
- Leaf serializers' `ToString`

## Grey zone — `IConvertible` render helpers

`module/ui/code/Fluid.cs:169`, `module/builder/code/Default.cs:759`, `builder/type/Render.cs:113` — `if (v is IConvertible conv) return Convert.ToString(conv, InvariantCulture)`. Render boundaries handling raw primitives; legit today, would collapse to `v.ToString()` if fed plang values. (Fluid is a larger conversation — see Part 4.)

---

# Part 4 — Fluid binds a snapshot, not the live variable store

Different principle, same family: **share the memory, don't snapshot it.** `ui.render` should let a template read the live PLang variable store, not a point-in-time copy made at render start.

## What it does today

The whole variable surface enters Fluid through two eager loops at render start (`module/ui/code/Fluid.cs:120-132`):

```csharp
foreach (var kvp in action.Context.Variable.GetAll())          // every variable, eagerly
    fluidContext.SetValue(kvp.Key, FluidValue.Create(await kvp.Value.Value(), options));
```

`GetAll()` returns live `Data` refs, but the loop **resolves every variable up front** (even unused ones) and wraps each via `FluidValue.Create`, splitting into two regimes:

- **Collections** (`dict`/`list`) → `NativeDictView`/`NativeListView` hold a reference to the live `dict.@this`/`list.@this` and read on demand. Genuinely shared, zero copy. Already right.
- **Scalars** (`text`/`number`/…) → no converter matches, so they fall through to Fluid's built-in `FluidValue.Create`. Frozen at bind time (held as an `ObjectValue` over the instance, or snapshotted into a `StringValue`/`NumberValue` copy — confirm which by checking Fluid 2.31's `Create` dispatch).

Net: it is **not** a live window into `Context.Variable`. It's a point-in-time projection of the whole set. So (1) every variable is resolved whether used or not; (2) a variable created/reassigned mid-render (via `callGoal`) is invisible — the set was snapshotted; (3) even a collection *reassignment* is invisible, because `set` rebinds to a new instance and the view still points at the old one (PLang never mutates in place).

## The fix — a live, lazy, async accessor into the store

Fluid is async-aware end to end: register an **async member accessor** (returns `ValueTask`) and the await runs lazily, **at access time** — when the template actually hits the variable. `Render()` already calls `RenderAsync` (`Fluid.cs:138`), so the plumbing is in place. So the accessor resolves through the existing **async** `Context.Variable.Get(name)` directly — no sync workaround, no eager pass, no new store method.

Replace the two eager loops (`Fluid.cs:120-132`) with the variable store as the context model plus one async accessor keyed by name:

```csharp
// (model, name) accessor → resolves dynamically, lazily, per reference; await runs only when {{ name }} renders
options.MemberAccessStrategy.Register<app.variable.list.@this, object?>(
    async (store, name) => (await store.Get(name)).Peek());

var fluidContext = new TemplateContext(action.Context.Variable, options);  // the live store IS the root model
```

`store.Get(name)` is the existing async resolver (`variable/list/this.cs:561`). The returned value rides the existing `ValueConverters`: a `dict`/`list` becomes a live `NativeDictView`/`NativeListView` (nested-live, unchanged), the `@null` citizen → `NilValue`, scalars map as before. `{{ x }}` now reads the live store at the moment it renders — no eager resolution of unused vars, no snapshot, mid-render `set`/`callGoal` changes visible, zero copy.

`(await store.Get(name)).Peek()` hands Fluid the value instance so `NativeCollectionConverter` can re-wrap nested natives live (`Peek` returns the `item.@this`; that's what the views feed on). If a template should render a `file`/`url` variable's **content** rather than its reference form, that's a deliberate `.Value()`/load step in the accessor — a template-semantics choice now, not a sync limitation.

For richer needs (indexers, or `{% for k in vars %}` enumerating the whole store), inherit `ObjectValueBase` and override `GetValueAsync`/`GetIndexAsync`, wired via a `ValueConverter`. Overkill for plain `{{ x }}` / `{{ x.y }}`; reach for it only if a template enumerates all variables.

The explicit `Parameters` override (`:126-132`) stays — `SetValue` those onto the context after construction so per-call params shadow the store.

## What to verify (don't assume)

- The exact Fluid 2.31 signature for the async-accessor overload (`MemberAccessStrategy.Register<T, TMember>(Func<T, string, ValueTask<TMember>>)`) and that a bare top-level identifier (`{{ x }}`) routes through the model's `(model, name)` accessor — that dynamic-name form is exactly what an arbitrary variable name needs.
- That nothing on the path calls the sync `Render` (it must be `RenderAsync`, which it already is) — a sync render would block on the async accessor.

## While you're in this file — the triplicated formatter

`FormatFormalValue` (`Fluid.cs:154`) is byte-identical to `Default.FormatValue` (`builder/code/Default.cs:752`), and `builder/type/Render.cs:113` carries the same `IConvertible` arm — three copies of "render a value in the catalog's formal syntax," kept in sync by a comment. Collapse to one home: ideally `value.Formal()` on each plang type (text quotes-if-spaced, number its literal, dict/list compact JSON, `%var%` bare), which also retires the three Part 3 grey-zone `Convert.ToString` sites; interim, one shared `Formatter.Formal(object?)`. Gated on the values reaching these sites being plang — until then, the single shared helper is the move.
