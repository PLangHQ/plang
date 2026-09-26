# Check-in — variable types: one walk, a scratch store, the property's own conversion

Branch `builder-formal`. Traced, nothing built.

## The finding first: a typed EMPTY value, not a typed null

- **A typed null exists.** `new type.item.null.@this("list", "goal")` is null and answers the type
  `list<goal>` (type/item/null/this.cs:31-50).
- **But it can't be the scratch value.** The check is "each property converts its value through its
  own door". That door is the slot type's `Create(raw, data)`, and a null goes through ANY slot as
  null: `number.Create` of a null answers null (number/this.cs:105-117), and a nullable slot takes
  it. So `math.add(A=%goals%)` with `%goals%` a typed null `list<goal>` would never be refused.
- **So the scratch value is the type's EMPTY instance, a real value:** `list<goal>` → an empty list
  (its kind kept), `number` → 0, `text` → "", `bool` → false, `dict` → {}.
  - Then `math.add(A=%goals%)` asks `number.Create(empty list)`, which declines, which is the refusal:
    the runtime's own conversion, no compatibility rule written by hand.
  - A type with no empty instance (`goal`, `item`, a host) stays a typed null. `item` is unknown and
    passes; a typed null passes too, so the check says nothing there, which is the "never guess"
    side. `goal` for a foreach item over `list<goal>` is shown in the prompt, but only checked when
    a later action needs it by type.

## The scratch store

- **A context of its own:** `new actor.context.@this(app, app.System.Actor, variables: null,
  parent: null)` (actor/context/this.cs:137). It makes its own `variable.list` (:142), has no parent
  (so no inheritance from the builder's variables), and is the System actor's (so no user-channel
  I/O).
- **Born per goal,** once per walk, and dropped after. The builder's `%goal%`, `%answer%` etc. are
  never in it.
- **What runs in it:** only `variable.set`'s own Run (into the scratch store) and the foreach item
  binding. Every other action is not run: its result is the empty value of its `Return`
  (action/this.Schema.cs:44).

## Where the walk lives, and its one word

- **On the goal's step list, `goal.Step.Scope()`**, one walk over the steps in order. For each
  step it takes that step's code and leaves the step what it knows: **`step.Variable`**, the
  variables the step uses with their types as known at that step (build-time, not [Store]).
- **One walk, two inputs, the same code shape:**
  - **before the LLM** (right after the second `build.pick`), each step's *known code* is built
    from its picks: a certain pick (≥ 0.9) gives the action and its Return, and the step's words give
    the names:
    - `write to %x%` (pick.list's existing WriteTo, reused) adds `variable.set(Name=%x%, Value=%!data%)`
      after the action;
    - `set %x% = <literal>` gives the literal's own type;
    - `foreach %c%, … item=%i%` binds `%i%` (default `%item%`) to `%c%`'s element.
    An unsure pick gives no action, so nothing is known from it.
  - **after the parse** (in build.match, once a step's line reads), its real code.
- **The walk is the same** for both: an action → `%!data%` = the empty value of its Return;
  `variable.set` runs; `loop.foreach` binds its item; a condition's body is walked without evaluating.
- **The check (after the parse only):** each property of each action converts its value from the
  scratch store through the slot type's own `Create`, as the runtime does. A decline is a refusal in
  the step's one refusal: `step 7: math.add A is %goals% (list<goal>), which isn't a number`. A value
  still unknown (`item`) passes.
- **Owner split:** pick.list builds the *known code* (it holds the picks and WriteTo); the walk and
  the store are the step list's; the check is the property's (its slot type's Create).

## Golden steps with `=> types:`

build (goal Build):
- `[6] - build.goals path=%path%, write to %goals% => decider: build.goals 1.00, variable.set 0.88 (write to) => formal: build.goals(Path=?); variable.set(Name=%goals%, Value=%!data%)`
  After it: `%goals% list<goal>`. It uses only `%path%`, which is unknown (the goal's parameter), so
  there's no `=> types:` on this line.
- `[7] - call EmitBuildEvent kind="goals-found", goals=%goals% => decider: goal.call 0.89 (possible) => types: %goals% list<goal>`
- `[8] - foreach %goals%, call BuildGoal goal=%item% => decider: goal.call 0.99, loop.foreach 0.98 => formal: … => types: %goals% list<goal>, %item% goal`

`build.load, write to %app%` gives nothing: build.load's Run is a bare `Task<Data>` (item). It's
unknown, so nothing is shown.

**A mismatch the check refuses** (after the parse): a step `count %goals%, write to %n%` answered as
`math.add(A=%goals%, B=1)`. `A`'s slot is `number`, and `number.Create(empty list<goal>)` declines:
`step N: math.add's A is %goals% (list<goal>), which isn't a number`.

## Your calls

1. The scratch value is the type's empty instance (typed null only where a type has none). OK?
2. The scratch store is a context of its own (System actor, no parent), one per walk. OK?
3. The walk is `goal.Step.Scope()` with `step.Variable`, and pick.list builds the known code before
   the LLM. OK, or should the walk be the goal's?
4. `set %x% = <literal>` and `foreach … item=%i%` are read from the step's words before the LLM
   (plang markers %…% and a quoted or numeric literal; the words "set"/"foreach" come from the pick,
   not the text). OK?
