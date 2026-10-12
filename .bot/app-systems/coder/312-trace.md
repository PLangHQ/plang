# 312 — a parameter is program text until its action runs (trace and shape, no code)

Decision 683. The leak: a goal written out in the Out view (`write out %!goal%`, a callback sent onwards, a call to
another plang) renders its actions' parameters against the sender's live variables, so `%apikey%` goes out as the key.

## (1) What a program holds as a parameter's value

One door makes it, on load and in-process alike: `type.Read` (`type/this.cs:443–468`).

- **From a .pr** — `goal/step/action/serializer/Reader.cs:143–155`: the row's raw slice, its declared type (a template
  row's type carries `template: plang`) and its `variable` list → `type.Read(ref slot, ctx with { Variable })`.
- **Built in-process** — `goal/step/action/formal/reader.cs:368–381` (`Born`): the literal's variables are found, the
  type is marked a template when it has any, and the value is read by the same `marked.Read(...)` (IsBuilt: true).

What `type.Read` answers for a row holding variables:

| the row's value | held as |
|---|---|
| a quoted string with `%x%` (`Text="%secret%"`, `"Hello %name%"`) | **`item.source`** (raw string + type + its variables) |
| a container with `%x%` inside (`{"to": %x%}`, a list) | **`item.wire.template`** (raw slice + its variables) |
| a variable-name slot (`Name=%x%`) | `variable` (eager — a name, not a value) |
| anything else | a lazy `wire` (no variables, nothing to render) |

So a program row never holds a text-with-`Template` directly: a text template is what a `source` becomes when it is
read (`source.Value` → `text(template, Template, variables)`, the same object `source.Output:362` hands over to).

## (2) Where a parameter becomes a live value

Every run reads a row through **`property.Data(context)`** (`type/property/this.cs:205`): the generated slot's helpers
`__Copy` / `__View` / `__Default` (`PLang.Generators/Emission/Action/this.cs:438–450`) do
`action[name].Data(context)` and then `.As<T>()` — a Data born with the running context, holding the row's value. A
plain-Data slot (output.write's `Data`) hands that Data on unopened; a typed slot opens it through `As<T>` / `Value()`.

`goal.call`'s arguments are already settled in the caller (`module/goal/call.cs:130`, `parameter.Settle()`): a
template renders there, with the caller's variables, and the callee binds the value — never the row. A call to another
plang sends the running frame's values (`parent/this.cs:40–44`), never the goal written out. Callbacks carry state
through the snapshot, not through rendered parameters.

## (3) The shape

**Program text is the row's value, and it writes as written — always, in every view, with no view check.**
- `source.Output` writes its raw form (`Write`), always.
- `wire.template.Output` writes its slice as authored, always.
- `property.Output` writes `Value.Output(writer, mode, …)` as it is: the row decides nothing.

**A running action's parameter is the live value**, made where the run takes the row — `property.Data(context)` —
by the value itself:

```csharp
// item: the value as a running action holds it — itself, by default (a number, a plain text, a wire)
public virtual @this Live(context) => this;
// source (a template): the text it reads into, which renders when written (text/this.cs:243–249)
public override @this Live(context) => Template != null ? new text(template, Template, _variable) : this;   // (a non-text whole %ref% the same way)
// wire.template: its decoded container, whose entries render themselves
public override @this Live(context) => Decoded(context);

// property: the run's own Data over this row — the live value
public Data Data(context) => new(Name, (Value ?? null.Instance).Live(context), context) { Properties = … };
```

So `write out %name%` is unchanged: output.write's Data holds the live text, passed on unopened, and its Output looks
`%name%` up. `write out %!goal%` writes the goal, whose rows hold sources → `%secret%` as written.

**Which checks go:**
- `type/property/this.cs:170–173` (`asWritten`): goes — the row doesn't decide.
- `source.cs:342` `mode == View.Store`: goes — a source is program text in every view.
- `wire/template.cs:27` `mode == View.Store`: goes — same.
- `text/this.cs:238` `mode == View.Store`: **question for Ingi.** After the change a text-with-Template exists only
  live (a running value, or what a run stored: `set %greeting% = "Hello %name%"` keeps the template). Today the Store
  view (a snapshot, the settings store) keeps it authored, so it re-renders after a restore. Dropping the check makes a
  stored live template store its rendering instead. Keep it (a stored value keeps its variables) or drop it (one
  choice, no view checks)?

**What a plang program sees change:** only the leak closed — a goal, a step, an action written out in any view shows
its parameters as written (`%secret%`). Running is unchanged: every run reads rows through `property.Data(context)`,
where they become live. The Debug view already showed rows as written (asWritten); now every view does.

**Name:** `Live` (an adjective: the value as it lives in a run). If Ingi prefers another word, it's one rename.

## (4) The test

- A goal holding `output.ask(Question="%secret%")` (formal-read, as a build makes it) with `%secret%` set: written out
  in the Out view (`write out %!goal%`'s path) and in Debug, its row writes `%secret%`, never the value.
- `set %name% = "Ingi"`, `write out %name%` still writes `Ingi`; `write out "Hello %name%"` still writes `Hello Ingi`.
- A container: `Parameter={to: %x%}` written out keeps `%x%`; run, it renders.
- Mutation: with `property.Data` handing the row's value unchanged (no `Live`), `write out %name%` writes `%name%` —
  the second pin fails; with `source.Output` rendering again, the first does.
