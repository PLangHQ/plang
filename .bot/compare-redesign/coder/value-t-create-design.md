# The typed ask — T.Create design (Ingi + coder session 2026-06-11)

**Status: settled with Ingi, awaiting architect review before implementation.**
Supersedes/extends parts of `architect/stage-9-slice-2b.md` (the door retype is
kept; the Value<T> rebuild mechanics changed in design review with Ingi).

## Rulings from the session (Ingi)

1. **No statics that receive values from outside the type, no reflection, no
   central ladders.** The earlier `Value<T>` rebuild (carve-outs + WrapConvert +
   ResolveMethodCache reflection) was rejected wholesale.
2. **The TARGET type owns conversion** — "we want number, the number should
   know how to create it." Not the source ("can I be a number?"), not a
   catalog above the types.
3. **`_type` is never null** (renamed from `_instance` — "it's just _type").
   Absence is a typed citizen: `absent` for NotFound/Uninitialized, `null.@this`
   for present-null. All Data null-checks die.
4. **`Ready()` was never a real name** — the type-side door is `Value()`,
   same name both floors. (Ready existed only because public `Value`
   properties blocked the method name; those faces die in 2b anyway.)
5. **Render belongs inside the type's door** — a stamped template isn't
   ready until its holes are filled, so `text.Value()` renders;
   `Data.Value()` has NO Template branch (it can't tell render from parse
   and shouldn't).
6. **Data passes itself at ask time** (`_type.Value<T>(this)`) — no stored
   back-pointer (values are shared; "the" owning Data is ambiguous), no
   context parameter (context rides the Data).
7. **The type builds the returned envelope** via binding mechanics Data
   exposes (`ShallowClone<T>` / `CloneError<T>`) — type decides WHAT rides,
   Data owns HOW a binding is cloned.

## The mechanism — `T.Create(item)`

Explored in order, each rejected for a concrete reason:
- instance virtual `Convert` on the SOURCE — source can't know every target (ruling 2);
- `new T()` blank + instance Convert — `new()` constraint requires public
  parameterless ctors, breaching number's private tower factory, null's
  singleton, path's protected scheme family, and creating constructible
  blank dates/bools;
- prototype registry (each type self-registers one instance at discovery) —
  viable, but loses compile-time enforcement and adds a runtime registry;
- `new T(item)` ctor — no such generic constraint exists in C#; ctors can't
  decline (only throw) and always allocate on pass-through;
- **`T.Create(item)` via `static virtual` interface member — CHOSEN.**
  This is NOT the central-static smell: dispatch is per-type at compile
  time; each type implements ITS OWN Create; adding a type = the new class
  brings its own; nothing central exists. It is `new T(item)` made
  generically callable and able to decline (null = "I can't be made from
  that").

```csharp
public interface ICreate<TSelf> where TSelf : item
{
    static virtual TSelf? Create(item value) => value as TSelf;   // default: pass-through or decline
}

// item — the door, then the TARGET constructs itself
public async ValueTask<Data<T>> Value<T>(Data asking) where T : item, ICreate<T>
{
    var answer = await Value();
    if (answer is T t) return asking.ShallowClone<T>(t);
    return T.Create(answer) is T c
        ? asking.ShallowClone<T>(c)
        : asking.CloneError<T>(this, answer);
}

// Data — the binding asks, nothing else
public ValueTask<Data<T>> Value<T>() where T : item, ICreate<T> => _type.Value<T>(this);
public async ValueTask<item> Value()
{
    var answer = await _type.Value();
    if (_type.Cacheable) _type = answer;
    return answer;
}
```

Chain-facet lookup ("Data<file> slot satisfied after the file parsed to
dict") stops being framework: a type that honors its history does the
Prior-walk inside its OWN Create (file does; most don't care).

Existing per-type `static Convert(object?, kind, ctx)` catalog hooks become
the BODIES of the Create implementations (logic reused; the registry
dispatch retires for this path — registry stays for the `as <type>/<kind>`
reader path until separately judged).

## Generator changes

The two runtime carve-outs were compile-time knowledge being re-discovered
via reflection — they move to emission:
- `Data<Variable>` (IRawNameResolvable) slots: the getter emits
  `Variable.Resolve(rawSlotText, Context)` directly.
- Action-destination slots (StepActions etc.): the getter emits direct
  structure conversion; sub-action %refs% stay raw for their own dispatch.

## Consequences / re-pins

1. `Data<T>` constraint widens to `where T : item, ICreate<T>` — every
   domain type used in a slot adds the interface declaration (default impl
   = zero bodies for pass-through types: goal, GoalCall, test, hash, …).
2. **Name propagation flips to slot-name**: the typed view carries the
   ASKING slot's name (ShallowClone of the asking Data); the unset-%x%
   diagnostic rides Data.Error (the render failure names the missing ref).
   NamePropagationTests re-pin accordingly. [Ingi aware — "I think
   slot-name is the honest answer" stands unchallenged.]
3. `Ready` disappears; the bool/binary/choice public `Value` properties go
   first (already 2b contract).
4. `Data.IsEmpty` is `ValueTask<bool>` forwarding to the instance's own
   IsEmpty (Ingi: "trust the type").
5. Templates on containers: list/dict override `Value()` to render their
   entries (each through its own door); the WalkContainerVars/
   SubstitutePrimitive family is already deleted.

## Architect amendments (2026-06-11, settled with Ingi — supersede the sketch above where they differ)

1. **`Create` returns the envelope: `static virtual Data<TSelf> Create(item value, data asking)`.** A bare `TSelf?` decline says "can't" but not why; the envelope return carries the real reason on `Data.Error` (uniform with the failure convention), and conversion can only happen inside a binding — no orphan typed views minted from free C#; every typed view is born a proper binding.
2. **The chain-facet walk lives in the DEFAULT Create** — `value as TSelf`, else `value.Facet<TSelf>()`, else `CloneError`. The model doc's chain promise ("a `Data<file>` slot is satisfied from the chain") is framework-level, not per-type opt-in; file overrides because it does MORE, not to get the basics.
3. **`item.Value<T>(Data asking)` disappears.** With Create taking the asking Data and returning the envelope, the typed ask is one branchless line on Data: `Value<T>() => T.Create(await Value(), this)`. Create is the single home.
4. **The guard line (binding-mechanics boundary):** `ShallowClone<TSelf>` and `CloneError<TSelf>` are the ONLY members of `asking` a Create implementation may touch. Create holds a Data now — this sentence is what keeps the courier/value layering from eroding one convenience read at a time.

5. **Door errors — the answer to the A-vs-B question (2026-06-11): neither as written; A's core with B's contract.** The type-side door takes the binding — `ValueTask<item> Value(data asking)` — and **every failure is authored by the type that failed** (file tells IO stories, source tells parse stories): the type catches only its OWN known failure modes, calls `asking.Fail(its own error)`, and returns `item.Absent` (never-null ruling). The untyped ask's return stays **item back** — the model pin holds, because the consumer already holds the error carrier: the Data it asked (await → guard → use). The generic `MaterializeFailed` catch and the `Mint()`-for-a-message are DELETED, not relocated — a truly unexpected exception is a bug and keeps propagating; B's typed-exception relay is rejected ("never a throw into a courier"). Consequences: **`Fail(error)` joins the guard list** — the blessed binding surface is exactly three members (`ShallowClone<T>`, `CloneError<T>`, `Fail`); `Data.Value()` keeps only the rebind branch (binding mechanics); and `Create`'s default forwards an already-failed asking's error rather than minting "cannot create T from absent."

6. **Unobserved errors surface — observation is opt-in handling (ruling 8, Ingi 2026-06-11: "love it").** The await-guard-use pattern's trap is that the guard is optional — a forgotten `if (!Path.Success)` swallows the error. Fix: swallowing becomes structurally impossible. (a) `Data.Error` gains an internal *observed* flag, set the moment anyone reads `.Success` or `.Error` (`.Value(fallback)` counts — it is explicit handling). (b) The generator's handler wrapper — the same home as the pre-Run `MissingRequiredParameter` guard — gains the mirror **post-Run epilogue**: any param Data carrying an UNOBSERVED error while the handler returned success → the param's error becomes the action's result, type-authored message intact. Happy-path one-liners are fully safe; the guard line is written only when the handler wants custom handling or a fallback. Same family as the existing generator guards: compile-time slot knowledge emitted per handler, no registry, no reflection.

## Open for architect

- ~~Does the `as <type>/<kind>` explicit-cast path (reader registry) stay on
  its current dispatch, or also fold into Create with a kind parameter?~~
  **RULED (architect + Ingi, 2026-06-11): two doors, one knowledge home.**
  Folding is structurally impossible under no-reflection — `T.Create` is
  compile-time generic dispatch; the reader path starts from a RUNTIME name
  ("image" parsed from the step), and reaching a static virtual member from a
  runtime name requires `MakeGenericMethod`. So: the reader path keeps
  registry dispatch, owned by the TYPE ENTITY (`{name, kind, strict}` —
  "construct a value of me, as kind X, strictly" is the entity's member,
  which also collapses more of variable/set.cs); kind/strict are reader-door
  parameters, never Create's; both doors call the type's SINGLE construction
  body — no second copy of "how a number is made."
- ~~AsCanonical (plain-Data slots): current rebuild keeps the full-match
  live-variable hop at Data level (the canonical IS the live Data — a
  binding-identity question, not a value question). Confirm.~~
  **CONFIRMED (architect + Ingi, 2026-06-11).** The hop happens before any
  value exists — a name→binding lookup ("which Data does %list% refer to?");
  no door opens, no type is in play, nothing for a type to own. Binding is
  Data's one job; this is purely that. Boundaries: full-match only (a
  template is a value born by rendering — normal path; the builder's stamp
  distinguishes), and the hop adds ZERO value logic — after it, everything is
  the live Data's ordinary doors.
- The exit-gate greps in stage-9-slice-2b.md still apply unchanged; the
  worklist items all survive — this design only changes HOW Value<T> is
  rebuilt, not what dies.

## State of the working tree

Mid-2b, deliberately uncommitted since 4f007672e: doors retyped
(`ValueTask<item?>`/`item?` — null-ness tightens further under ruling 3),
assert's second comparison engine deleted (data.Compare + item.Contains +
item.IsEmpty landed), Operator.cs collapsed, cache/wrap + debug/tag door
fixes, five Parse arms → `item.Backing` seam, RawValue deleted, "this"
probe deleted, IsVariable/HasVariableReference stamp-based, AsCanonical
slimmed, Walk* family deleted. ~50 compile errors remain = the site
worklist, paused for this review.
