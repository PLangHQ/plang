# Stage 1b shape — the IWriter door for a text with values inside it (decision 800)

Coder: tester (plang-f2). Approved by Ingi (800): one door; text/json write one string as today
(the byte-equal guard); the llm writer cuts each value. A locked serializer addition → surface
re-pin under 800.

## The problem (recap)

A partial template `"rank these %orders%"` renders through a text writer that `text` makes for
ITSELF (`type/item/text/this.cs:160-167`, `Rendered`), so the handed writer only ever receives the
finished string — the llm writer never sees `%orders%` to cut it. Streaming the parts straight into
a json/plang/wire writer is wrong: a list's tokens where ONE string must stand.

## The door (name is the architect's to set)

A bracket on `IWriter` that delimits a text built from parts that together are ONE string. Between
it, the caller emits each literal via `String` and each embedded value via the value's own
`Output(this, …)`; the writer assembles them into a single string.

```csharp
/// A text with values inside it ("rank these %orders%") — its literal spans and its embedded values,
/// in order, that together are ONE string in the output. Between the two, String writes a literal and
/// each embedded value writes itself through its own Output. text/json/plang assemble one string, as
/// today; the llm writer writes each value through its cuts.
void BeginText();
void EndText();
```

(`Content(string, kind)` stays the leaf form — one whole string already in hand; `BeginText/EndText`
brackets a *composed* one. A single parts-list method can't work: embedded values render async, and
`IWriter` is sync — the bracket lets `text` drive each `await value.Output(writer)` itself.)

## text's Output using it (the locked-core body change)

`text.@this.Output` for a partial template changes from "render to a string with my OWN text.Writer,
then hand the string" to driving the HANDED writer:

```csharp
// was: writer.Content(await Rendered(context), Format(context));
writer.BeginText();
int pos = 0;
foreach (var span in /* the same single pass as Rendered */) {
    writer.String(literalBefore);              // a literal span
    await embedded.Start(context).Output(writer, mode, context);  // an embedded value, through ITS door
}
writer.String(trailingLiteral);
writer.EndText();
```

`Rendered()` (the string form) stays for `Clr`/`ToString`/full-match's own uses; only the writer path
moves to the door. The variable-resolve cycle net and the `%!x%`-unset rule carry over unchanged.
`text.Output`'s **public signature is unchanged**; the IWriter **surface gains two members** → the
surface golden re-pins under 800.

## Each writer's answer

- **text.Writer**: `BeginText`/`EndText` are no-ops; `String` appends bare, an embedded value writes
  its text form (a leaf bare, a container as json-text) — exactly today's bytes. **Guard.**
- **json.Writer / plang (wire)**: in the bracket, route everything (literals + embedded values'
  Output) into an inner text form (buffer); at `EndText`, emit that buffer as ONE quoted string.
  Same result as today (today it already received one pre-rendered string), so **byte-equal** —
  `write %greeting% to a json channel` is unchanged.
- **llm.Writer**: in the bracket, each embedded value is written through ITS cut (a long text cut +
  marker now; list/dict cuts at stage 2), literals pass through, assembled into one string. This is
  what makes `"rank these %orders%"` cut like `%orders%` alone.

## Tests (red for their own reason)

Unit-level on text+writer (deterministic, no llm call), mirroring the plan's 1b lines:
- **PartialTemplateCutsTheEmbeddedText**: a text template `"summarize %doc%"`, `%doc%` over the
  length, Output through `llm.Writer` → the literal, then the first `length` chars of doc, then
  `--- N more characters ---`. RED without the door (the embedded value renders through text.Writer,
  uncut).
- **PartialTemplateIntoJsonIsByteEqual** (guard): the same template through the json writer → the
  exact bytes it produces today. RED if the door changes the one-string assembly.
- Plus the end-to-end: `ask llm "summarize %doc%"` (over max) through the mock-HTTP body, once the
  door is in — the sent user content is the sentence with the cut doc.

## OBP / surface

| surface | check |
|---|---|
| `BeginText`/`EndText` on IWriter | the one door Ingi approved; a locked serializer addition, re-pinned under 800 |
| text.Output | body change only, public signature unchanged; non-llm writers byte-equal (the guard) |
| no value asks which writer | the value's Output is unchanged; each writer decides assembly |

Need the architect's nod on the door's **name** and this shape before I write it.
