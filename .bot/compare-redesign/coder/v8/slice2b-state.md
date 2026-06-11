# Slice 2b — T.Create landed structurally; consumer-tail walk in progress

**State: COMPILES CLEAN (PLang + PlangConsole + all test projects, 0 errors).
Uncommitted WIP, not pushed.** Runtime: large failure count = the sanctioned
consumer-tail walk (architect 2b "the compile errors ARE the site worklist",
larger than the ~75 estimate) + one context-propagation ripple (below).

## What landed (the structural core — correct & per the approved design)

- **`Value()` / `Peek()` return the typed instance** (`item`), never C# null —
  per architect 2b first item ("the answer is THE INSTANCE, always"). Door:
  `_type.Value(this)`, rebind iff `Cacheable`, return the instance.
- **`T.Create(item value, data asking)`** — the typed ask is one line:
  `Data.Value<T>() => T.Create(await Value(), this)`. `ICreate<TSelf>` default =
  pass-through → chain `Facet<T>()` → the catalog converter
  (`catalog.TryConvert`, the single construction body, reader-path ruling) →
  decline carries the real reason on the envelope. `ShallowClone<T>` /
  `CloneError<T>` / `Fail` are the blessed binding surface.
- **`_type` is never C# null** — `absent.Slot` default; `Lift(null)` →
  `null.@this.Instance`; NotFound/Uninitialized → `absent.Slot`.
- **Failure: type authors its own error** — file/url/source/text doors catch
  their own modes, call `asking.Fail(error)`, return `Absent`. The generic
  `MaterializeFailed` catch is gone (navigation keeps one narrow catch).
- **Unobserved-error flag** on `Data.Error` (ruling 8) — `Success`/`Error`
  read sets observed; `HasUnobservedError`/`ErrorUnobserved` for relays.
- **ICreate constraint** added to ~33 item types + the `where T` sites
  (serializers, GetOrCreate, RunAction<TResult>, test fixtures).
- **ROOT FIX — Variable born at the entry seam** (Ingi's call, the real root
  of the `%s%`-not-set wave): `type.Judge` now builds a `Variable` for a
  raw-name declared type *before* the `%var%` bail (line ~409 used to drop the
  declared type for any `%…%` string). A `Variable` is a resolved name — never
  text, so never template-stamped, door never renders. `variable` registered
  via `[PlangType]` + context-free `GetPrimitiveOrMime` arm so `Judge` (runs
  pre-Context) can test `IRawNameResolvable`. The generator raw-name carve-out
  I started was REVERTED — the getter just calls `Value<Variable>()` and it
  works because the value is already a Variable.
- `text.Value` is a private field `_value`; `item.ToRaw` internal; the five
  Parse arms route through `item.Backing`; `RawValue`/`AsCanonical` walk/
  `WrapAs`/`AsT_Impl`/`TryStaticResolve`/`ConstructWrap`/reflection cache all
  deleted. `Contains`/`IsEmpty` are item virtuals; `Data.HasValue`/`IsEmpty()`
  are the binding's presence/emptiness asks.

## Remaining runtime failures — roots identified (NOT yet walked)

1. **Consumer tail (the bulk).** ~800 sites do `await Value() as ClrType` /
   `Peek() is byte[]` / `(IIdentity)Peek()` expecting the RAW form; they now
   get the typed instance / `clr` carrier / `null.@this` citizen. Per 2b:
   each → `item.@this.Lower<T>(...)` at a proven .NET edge, or typed flow.
   Categories seen: `clr→IIdentity` casts (35), `is byte[]`/`is string`
   arms, `Value() == null`/`IsNull` checks (null citizen ≠ C# null —
   production + test re-pins), `JsonElement`/`Dictionary` arms in OpenAi/llm.
2. **Context-param ripple (distinct, ~34 "Path.Authorize requires
   Context.Actor").** Collapsing `Value<T>()` removed its `context` param;
   `ShallowClone` now relies solely on the asking `Data._context`. Call sites
   that passed context explicitly without setting it on the Data lose it →
   the resolved path has no Context.Actor. FIX OPTIONS: (a) ensure every
   producer sets `Data.Context` before `Value<T>()`, or (b) re-thread a
   context into the door for the cases that need it. Needs a decision — likely
   (a) since "context rides the Data" is the ruling.
3. **`choice`/number/datetime conversions** now route through the default
   Create → `catalog.TryConvert`; verify each kinded conversion still lands
   (some `CreateDeclined: choice` seen — confirm TryConvert handles
   text→choice<Enum>, else `choice` needs a `Create` override).

## Progress (session 2, after the structural core)

Roots fixed, with measured drops (C# slices):
- **Context invariant** — context RIDES THE VALUE. `Data.Context` getter falls
  back to the value's own (`_type as IContext`); setter propagates only NON-null
  (never clobbers a value's born context); `ShallowClone` sources the answer's
  own context first. Killed the ~34 `Path.Authorize requires Context.Actor`.
  (Field still nullable for genuinely context-less OUTPUT results — `Data.Ok(5)`
  is static; those get stamped at the dispatch-return boundary as today. Full
  non-nullable-TYPE sweep deferred — it needs the static result factories
  decided.)
- **Provider resolution** — `Code.Get<T>()`/`[Code]` getter did `Peek() as T`,
  now get the `clr` carrier → null. Both → `item.@this.Lower<T>(...)` (carrier
  lowers via its own Clr). + 3 direct provider casts (identity `(IKey)`, actor
  `(IIdentity)`/`as Identity`). Killed the 268 NRE + 53 IKey + 35 IIdentity
  waves.

- **Providers are rung-3 — never in Data (Ingi's call).** `Code.Get<T>()` now
  returns `(T? Provider, IError? Error)` — no `Ok((object)provider)` wrap, no
  `clr` carrier, no `Peek`/`Lower` round-trip. Both generator emit paths
  (the `[Code]` getter + the eager dispatch resolve) + `GetOrDefault` + 4 prod
  callers (Navigation grep, identity IKey, debug ILlm, actor IIdentity) +
  ~5 test files converted. (The `Lower<T>` patch from the prior step is gone —
  this is the real root.)
- **`context.Ok(...)` deferred to todo** (`Documentation/v0.2/todos.md`): a
  context-stamping result factory, the path to non-nullable-BY-CONSTRUCTION
  when the static `Data.Ok/...` factories get their pass. Today: context is
  effectively non-null at READ via "rides the value" + entry-point stamping;
  the field stays nullable for write-only results.

Counts: Modules 541→229, Runtime 112→76, Types 66→47, Wire 101→60,
Generator 25→12, Data ~157→144 (segfault-truncates). Compiles clean.
Peak was ~1000 failures across slices; the mechanical-root phase took it to
~568. Remainder is the diverse long tail below.

## Remaining (long tail — diverse, per-area investigation, NOT one-line roots)

- **~44 assertion mismatches** (`Expected to be equal to X`) — born-typed value
  shape vs test expectation; case-by-case re-pin (display compare / Lower).
- **28 JsonElement `Array`-vs-`Object`** — ALL in `llm/code/OpenAi.cs:283`
  (`responseJson.Value.choices.GetArrayLength()`). The HTTP response Data's
  JSON shape changed under born-typed wire — the response body now parses to a
  different element shape. LLM-area investigation (not a cast).
- **9 `NotSupportedException` serializing `Func<data,ValueTask<item>>`** — a
  `computed`/door delegate is reaching a JSON serializer. Find the Data that
  carries a Func onto the wire (likely a `computed` value or a snapshot).
- **~10 residual NRE, `UnknownType ''`, `NoSignature`, null-citizen `== null`
  / IsNull re-pins** — mixed.

These need fresh, area-by-area attention (llm, wire/serialization, assert
re-pins) — the mechanical-root phase is done.

## Resume plan

Walk by category, biggest first, rebuild+retest per category to watch the
count drop (don't blanket-edit): (1) fix the context ripple — it's a real
defect inflating the count; (2) `clr`-cast / `is byte[]` sites → `Lower<T>`;
(3) null-citizen `== null`/`IsNull` → re-pin to the binding's null check;
(4) per-type `Create` overrides only where `TryConvert` can't carry it.

Pre-change green baseline: commit `cd7c78b05` (design doc) on top of the
slices 1–5 that were already green. This WIP sits on the uncommitted tree
above that.
