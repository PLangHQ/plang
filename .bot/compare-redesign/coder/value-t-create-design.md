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

## Open for architect

- Does the `as <type>/<kind>` explicit-cast path (reader registry) stay on
  its current dispatch, or also fold into Create with a kind parameter?
- AsCanonical (plain-Data slots): current rebuild keeps the full-match
  live-variable hop at Data level (the canonical IS the live Data — a
  binding-identity question, not a value question). Confirm.
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
