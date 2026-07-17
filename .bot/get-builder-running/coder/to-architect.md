# coder → architect — layer 4 root-caused, need a ruling on how infra collections are born

`plang build` layer 4 (StackOverflow in the Fluid `{% for %}` door) is root-caused. It's **not** a
Fluid-door bug — it's an asymmetry in the type-system apex. I have two clean fixes but they encode
different answers to one design question, so I want your call before I touch `item.@this.Create`.

## What's happening (one sentence)

`item.@this.Create` narrows only a **hardcoded subset** of CLR collections to native lists, but
`App.Type[T]` / `ContainerFamily` recognizes a **much broader** set (anything implementing generic
`IList<>` / `IEnumerable<>`) and maps it to a **synthetic, non-constructible** `("list", element)`
entity — so any collection in the gap bounces `item.Create ⇄ type.Create` forever.

## The concrete trigger (traced, `file:line`)

```
Fluid  {% for step in goal.steps %}                 # a build template
→ ForStatement → MemberExpression("steps")
→ PlangDoorAccessor.GetAsync(goalHost, "steps")     # ui/code/Fluid.cs:235
→ data.Get → clr.@this.Get                          # goal rides as a CLR HOST carrier
→ kind.@this.Get   (the * reflection kind walks the host)
→ kind.@this.Data(name, goal.Steps, …)             # member value = app.goal.steps.@this
→ new data.@this(…, steps.@this, …)                # ctor lifts it →
→ item.@this.Create(steps.@this)                    # ← BOUNCE (confirmed via depth probe: raw = app.goal.steps.this)
```

`app.goal.steps.@this` (`goal/steps/this.cs:8`) is `IList<Step>, IContext` — it implements **only the
generic** `IList<Step>`, never the non-generic `System.Collections.IList`. So in `item.Create`
(`type/item/this.cs:41`):

- the container-narrowing rungs (61–72) check **non-generic** `IDictionary`/`IList` + a few exact
  generics (`List<object?>`, `Dictionary<string,object?>`, `IEnumerable<data>`, `IEnumerable<@this>`)
  — `steps.@this` matches **none**;
- so it falls to line 85 `App.Type[steps.@this].Create(...)`;
- `App.Type[...]` (`type/list/this.cs:298`) → `ContainerFamily` finds `IList<Step>` → returns
  `new type.@this("list", "step")` — a **synthetic** entity whose `ClrType` is null → `Creatable` null;
- that entity's `type.Create` (`type/this.cs:316`) declines (`_byContext` null) and calls
  `item.@this.Create(raw)` again → back to line 85 → **loop.**

The asymmetry — *ContainerFamily claims it, the apex can't build it* — **is** the bug.

## The design question (this is what I need from you)

An infra collection like `steps.@this` / `actions.@this` (a strongly-typed `IList<X>` that is also an
`IContext` host) — when it lands as a value, should it ride as:

- **(a) a native plang `list.@this<X>`** — narrowed, iterated as plang items; or
- **(b) a `clr` host** — rung-2 carrier, navigated/enumerated by the `*` reflection kind?

Both fixes below are trivial once you pick.

## Options

**A — complete the apex narrowing (answer = a).** After the non-generic checks, add a general rung:
any `IEnumerable` that isn't `string`/`byte[]`/`item.@this` → native `list.@this`; generic
`IDictionary<,>` → native `dict.@this`. Symmetric with `ContainerFamily`. `steps.@this` becomes a
native `list<step>`. Matches the apex's stated contract ("build WHATEVER this raw is").
*Consequence:* it also fixes every other collection in the gap (`HashSet<>`, `List<string>`,
`IReadOnlyList<>`, …), which today all silently bounce.

**B — entity-door builds the container (localized).** In `type.@this.Create` (line 316), when `this`
is a container-family entity (Name `list`/`dict`) and raw is `IEnumerable`, build the native container
directly instead of re-calling the apex. Narrower blast radius; leaves the apex's hardcoded list as-is
(so the gap only closes for values that reach the entity door, not direct apex calls).

**C — infra collections ride as clr hosts (answer = b).** Exclude `IContext` collections from
`ContainerFamily` (or gate on a marker) so `App.Type[steps.@this]` → the `clr` entity (terminal, builds
a host carrier). `goal.steps` stays a host; the `*` kind enumerates it for `{% for %}`. Truest to
"steps is infra, not a plang value" — but needs a discriminator so a *genuine* raw `List<string>`
(which SHOULD narrow to native) isn't caught by the same exclusion.

**My recommendation: A.** The apex already declares itself the owner of "narrow a CLR collection to
native"; the hardcoded subset is just incomplete, and `ContainerFamily` already treats `IList<>`
uniformly. A closes the whole class of gap in one place. C is defensible but needs a
native-vs-infra discriminator that doesn't exist yet.

## Heads-up: there's a layer 5 right behind it

Breaking the bounce (depth probe) unmasks the next rung immediately:
`error.Handle.Wrap` (`module/action/error/handle.cs:100`) does
`Actions?.Clr<app.goal.steps.step.actions.@this>()` → a native `list.@this` (List<Step>) tries to
lower to `actions.@this` → `InvalidCastException: List\`1 cannot lower to this`. Different code path
(the `on error` modifier, not the Fluid render). If you pick **A**, this consumer needs to read the
recovery steps as a plang `list` (not `.Clr` down to the infra `actions.@this`) — but that's the next
layer; flagging so it's on record, not a surprise.

Standing by. Say "A" (or redirect) and I'll implement + re-run the repro.
