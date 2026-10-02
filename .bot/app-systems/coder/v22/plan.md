# v22 — the pick pass: masking (issue 32), the Option question v2, listed.Option (issue 25) — shape

Sources: `.bot/app-systems/builder/v3/issue32-masking-shape.md`, `.bot/app-systems/builder/v3/option-v2-shape.md`
(with the architect's accepted changes), the architect's queue note on `listed.Option`.

## 1. The step's mask — one owner, one map (issue 32)

A new item the step owns: `goal.step.mask.@this`, read as `step.Mask` (born from the step's text; born again when
the text changes — the step holds the one it made with the text it was made from).

```csharp
// call goal Page module=%!app.module.file%   →   call goal Page module=%v1%
public sealed class @this
{
    public string Text { get; }                 // the step's text, each distinct variable a placeholder, first-appearance
                                                // order, repeats reusing their number; every other word untouched
    public IReadOnlyList<variable> Variable { get; }   // what each placeholder stands for: %v1% → %!app.module.file%
    public string Hide(string line);            // a line written in real variables, written in placeholders
    public string Restore(string answer);       // an answer written in placeholders, written in real variables
}
```
- The variables are the parser's (`type/item/variable/parser`), the same reading the pick's structure uses; member
  paths (`%!app.module.condition%`, `%user.name%`) are one variable each.
- The placeholder stem is one the step's text doesn't use: `v` (`%v1%`), else `v_` (`%v_1%`), … — never a collision.
- Templates read `s.Mask.Text` where they read `s.Text` today (decider1, decider2, properties, decider.state — the
  builder's). Nothing structural changes source: the pick's own reads (`Placed`, `First`, `Destination`, …) keep the
  real text — they make the .pr.

**Restore, one door:** `formal.Reader.Read(line, context)` restores through its step's mask first (`_step.Mask.Restore`),
before anything parses or binds — every formal read (the step list's answer read, `build/code/Default.cs:278`, the pick's
`Known`) goes through it, so a `%vN%` never reaches a binding or the .pr. A line with no placeholder (the pick's own
`Known`, written in real variables) is unchanged.

**The starting line agrees:** `pick.list.Formal` (the `=> formal:` line the writer copies) is written through
`_step.Mask.Hide(...)`, so the line the writer copies and the step text it reads use the same placeholders.

## 2. The Option question v2 — the type answers what it offers (decision 496, accepted shape)

One member on the type, no choice-or-not branch in the question:

```csharp
// type.@this — what a value of this type can be, for a step: the kind answers
public IReadOnlyList<string> Offers(goal.step.@this step) => kind.Offers(step);
// kind.@this (base): the step's own variables, as the decider sees them — its mask's placeholders
public virtual IReadOnlyList<string> Offers(goal.step.@this step) => step.Mask.Placeholders;
// a closed set's kind (a choice's): its own values
public override IReadOnlyList<string> Offers(goal.step.@this step) => Values;
```
(Exact placement follows where a choice's values live — `type.Values => kind.Values ?? Family._values`; the override
sits on the kind that holds a closed set. I confirm that at build.)

- `question.Values` becomes the offers the list gives it at birth (`Property.Type.Offers(_step)` + `None`) — the question
  stays "never its words"; `decider2.template`'s `when "Option"` render is unchanged.
- The decider answers a placeholder (`%v2%`); `Call()` writes it (`Item=%v2%`) into the masked starting line; the writer
  copies it; `Restore` puts the real variable back before the line is read.
- The `ask:` note lines for `loop.foreach` Item/Key and `llm.query` Conversation are the builder's.

**Conversation:** `conversation.Create` is born from a response, not only a dict: a value that isn't a
`{continue: …}` dict is the response it continues (`Conversation=%answer%` → `Continue` = that Data). A bare
"continue" stays the decider's `none` (Ingi: null).

## 3. `listed.Option` (issue 25, robustness)

The chosen options of an action are read by **one** member of the pick list, `Chosen(action)` → `["Template=plang", …]`
(`_option` for that action, `none` left out). `Call()` reads it (required properties + `Chosen`), and `Listing()` sets
`listed.Option = Chosen(name)` on each listed action — so a listed, uncertain `file.read` (0.72, Possible) still carries
`Template=plang`, and the two can never drift. The builder renders `listed.Option` on the `=> decider:` line.

## Tests (each fails without its change)
- Mask: distinct variables numbered in first-appearance order, repeats reused, other words untouched (`Page` stays),
  a member path one variable, a step using `%v1%` itself gets another stem; `Restore(Hide(x)) == x`.
- formal.Reader: `goal.call(Name="Page", Parameter=[{module: %v1%}])` on the issue-32 step reads to
  `Parameter` holding `%!app.module.file%`.
- `pick.list.Formal` is masked; `Known` is real.
- Offers: a `choice` property offers its values; a `variable`/`item` property offers the step's placeholders.
- `Chosen` feeds both `Call` and `listed.Option` (an uncertain listed `file.read` carries `Template=plang`).
- `conversation.Create` from a plain response Data → `Continue` is it; a non-dict non-response still refused? (a
  response is any Data — the refusal goes; I'll say so if a value must still be refused).

## Split
Core (mine): `step.Mask`, restore in `formal.Reader`, masked `Formal`, `Offers` on type/kind, question offers, `Chosen` +
`listed.Option`, `conversation.Create`. Builder: the four templates read `s.Mask.Text`, the `ask:` lines, `=> decider:`
renders `listed.Option`, goldens, and the measurements (32a/b, issue 2, the control set).
