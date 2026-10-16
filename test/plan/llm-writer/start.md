# llm-writer

Traces to: the conversations with Ingi on 2026-10-15 and 2026-10-16, logged in `.bot/kind-is-type/architect/summary.md`:

- 791 and 793, which start the branch;
- 800, the door for a text with values inside it;
- 819, a message's content is a value until it's sent;
- 844, the one byte door;
- 845, how a value reaches an llm;
- 846, stage 3 rewritten.

The chain is idea → conversation → summary → this plan → code (`start.goal` and the tests it runs).

## Why

A value put into an LLM message is written whole. `user: %text%` with a 2 MB text, or `%orders%` with 40,000 rows, can blow the model's context, cost money for nothing, or fail the call. Ingi: "I feel like there should be a llm serializer … text can be anything, so when you do 'user: %text%' to the llm, it can blow up, so the llm module uses llm serializer, and for text it limits it to a length, for dict/lists, it trims".

Stage 3 showed the same root at the core: a writer never hears what it's writing. That leads to three faults:

- **A value picks the writer's door.** Content and source ask `Owns(writer)`, and list keeps a table keyed by the writer's name.
- **The writer's kind is a string** (`Format => "json"`).
- **Nothing says how a png or a pdf should look for an llm.**

Ingi: "we need to solve this part, then the llm writer comes pretty close to free".

## What the developer gets

```plang
Start
- read image.jpg, write to %image%
- llm system: tell me about image, user: compare %image% with %other%, I think %image% is so cool, write to %response%
```

- **A short value is written in place**, as today.
- **A text over the max is cut**, with `--- N more characters ---`.
- **A list or a dict** gives its columns and their types, then up to 100 rows, then `--- N more rows ---`.
- **An image goes as an image the model sees, by name.** The text keeps `%image%` as a reference, as often as it says it, and each image goes once beside the text. Ingi: "then %image% is just refrencing a variable in the llm mind".
- **A long value goes by reference too.** Over the inline limit (Ingi: "if it goes over e.g. 200 chars we refrence it like image"), the text keeps `%description%` and the value goes once beside it, by name.
- **Bytes with no way to show themselves** (an mp3) go as a label, `[audio/mpeg, 3 MB]`. They are never inlined.
- **`%x.trim%`** gives a value trimmed, as the LLM would see it.
- **The limits are llm settings**, so a developer raises them where needed.

## The rulings

1. **A value writes what it is; the serializer decides how it looks** (the standing rule).
2. **The core changes only where Ingi said yes:**
   - the door for a text with values inside it (800);
   - one byte door, `Bytes(byte[] value, kind kind)` (844);
   - a registry of serializers per pair, input kind → output kind (846).

   The llm module creates and registers its own entries (Ingi: "the llm module need to create this add register").
3. **Whole up to the max.** There's no per-query "whole" switch; a developer raises the setting.
4. **A list over 100 rows is cut** with `--- N more rows ---`. A list or a dict gives its column names and types first.
5. **`trim` is one word with one intent: the value trimmed.**
6. **A real summary** (`%doc.summary%`) is a follow-up, not this branch.
7. **A serializer per pair, input kind → output kind** (845: "so it is really, what is the input kind and what is the output kind"). An input kind with no entry of its own falls back to its parent. A pdf with no llm entry is written by the text writer under the llm writer. Whether pdf gets its own llm entry is parked.
8. **No `if` on what a value is** (845: "I dont like if statements … I also had spider sense about Owns method"). `Owns` goes, the writer's string `Format` goes, and so does list's map keyed by it ("something big wrong here").
9. **The llm view happens where the message is made**, in `data<LlmMessage>.Value()` (845: "this is where the magic/trimming/image2base64 should happen"). The message knows its content goes to an llm. OpenAi then builds its JSON and doesn't render.
10. **An image, or a long value, is a reference in the text plus the value once beside it, by name** (845). OpenAI's image part has no name field, so OpenAi puts a `%image%:` text part before each image.
11. **This is llm-writer's work, not branch A's** (Ingi: "just with what we are doing now already, this is llm-writer thingy").

## Stages

Each stage ends with its tests built and run, and each test goes red without its change. Each stage's shape comes to the architect before it's built.

**Done:**

| stage | commit |
|---|---|
| 0, the trace | done |
| 1, the llm writer, `%!llm.setting.length%` = 100,000 | 68d879ba4 |
| 1b, `BeginText`/`EndText` (800) | 0315b6ade |
| 2 part 1, lists and dicts as a table, `%!llm.setting.rows%` = 100 | cd5e2ea39 |
| 2 part 2, a message's content is a plang value, rendered once after materialize (819) | 5ac1bd8a8 |

The old stage 3 (7ce25c4b1) is reverted first and kept as a reference. It rendered twice, type-checked the image in OpenAi, and missed `what is %photo%`.

### 3a. The writer hears what it writes (844)

```
- a json file's content written to json   → copied as is (no Owns: json's entry for json says so)
- an image written to json                → base64, as today
- an image written to text                → [N bytes], as today
```

- **The byte door.** `Bytes(byte[] value, kind kind)` replaces `Bytes(byte[])` (`type/format/IWriter.cs:51`) and `Raw(byte[])` (`:64`).
- **The four byte values make one call each:**
  - image (`image/this.cs:115`)
  - binary (`binary/this.cs:211`)
  - content (`content/this.cs:138–140`)
  - source (`source.cs:363–369`)
- **The writer carries its output kind, an object, not a string.** This replaces `IWriter.Format` (`IWriter.cs:26`) and `IReader.Format` (`IReader.cs:49`).
- **The image holds its registered kind,** so its mime is there. Today it's born bare (`image/this.cs:189`).

### 3b. The pair registry, and llm as a kind of text

```
- the llm module registers: text → cut, list and dict → the table, image → by name
- json writes %doc% (a json value)     → json's entry for json: copied as is
- formal writes a list of arguments    → formal's entry for list (list's map gone)
```

- **The registry is keyed by the pair, input kind → output kind.** It's shaped like the reader registry (`type/reader/this.cs:34`):
  - keyed by the pair;
  - a runtime `Register` for a module;
  - an exact match first, then the input kind's parent.

  The registry is NEW and sits on a locked surface (Ingi's yes, 846). It needs a name that navigates.
- **The one place a value is written asks the registry first.** That place is `data.Output` (`data/this.Output.cs:47`): an embedded variable is written through it (`text/this.cs:252`, `bound.Output(sink, …)`). An entry gets the Data whole, its name included.
- **llm is a kind of text, the llm module's own.** A `kind<text>` class in the module is found the way every kind class is (`kind/this.cs:67–71`). Its `Encode` makes the llm writer. What llm doesn't handle falls to text, under it (`llm/writer.cs:18`, `:64`).
- **Stages 1–2 move into entries.** Their cut and their table become the llm module's entries for text, list and dict.

### 3c. The message renders its own content (rulings 9, 819)

```
- llm user: rank these %orders%   → Message.Value(): the content rendered once, through the llm kind
- OpenAi                          → builds its JSON from the rendered messages; renders nothing
```

- **Why not in the message's `Create`.** `Create` is sync and needs no context (`llm/type/message/this.cs:40–48`). Rendering needs a context and I/O (`%image%` opens its file through its gate, `content/this.cs:143`).
- **Where a template renders today.** A partial template's `Value` renders through a text writer it makes itself (`text/this.cs:156`, `:162–169`, `new text.Writer` at `:165`).
- **What changes:** a template renders through its own kind's `Encode` instead. Plain text still gets the text writer, through text's own `Encode` (`text/this.cs:49`). The message's content is a `text<llm>`.
- **The rendered content** is the text plus its named values: the llm module's own type (NEW), not a property bag on a Data.
- **Goes:** OpenAi's `RenderContent` and the llm writer it makes itself (`OpenAi.cs`, at `new llm.Writer`).

### 3d. Images by name (ruling 10)

```
- user: compare %image% with %other%, %image% is so cool
    → text "compare %image% with %other%, %image% is so cool"; images: image, other (each once)
- OpenAi → the text part; then for each image: a "%image%:" text part, then its image part
- an mp3 in a message → no entry → Bytes(bytes, mp3) → [audio/mpeg, 3 MB] in the text
```

### 3e. Long values by reference

```
- user: this is a %description%, the %description% is too long   (over the inline limit)
    → the text keeps %description% twice; the value goes once beside, by name, cut at the max
- a value under the limit → in place, as today
```

The inline limit is an llm setting. My suggestion for its name is `%!llm.setting.inline%`, default 200; Ingi to confirm both.

### 4. `%x.trim%`

```
- write out %orders.trim%   → the same view the LLM gets: columns, 100 rows, "--- 50 more rows ---"
- set %t% = %text.trim%     → a text's own trim, as today
```

## Demolition

**Dies in 3a:**
- [ ] `IWriter.Bytes(byte[])` (`IWriter.cs:51`) and `IWriter.Raw(byte[])` (`:64`), replaced by the one byte door.
- [ ] `IWriter.Format` (`:26`) and `IReader.Format` (`IReader.cs:49`) as strings.
- [ ] `kind.Owns(IWriter)` (`kind/this.cs:177`), its overrides (`kind/json/this.cs:26`, `wire/kind/plang/this.cs:126`), and its five `if`s: `IWriter.cs:76`, `content/this.cs:138`, `source.cs:363`, `wire/this.cs:61`, `:74`.

**Dies in 3b:**
- [ ] list's `_formats` (`list/this.cs:290–304`); its formal serializer (`list/format/formal.cs`) becomes formal's entry for list.
- [ ] `IOutput`'s "keyed by format" map on the value (`type/format/IOutput.cs`).

**Dies in 3c:**
- [ ] OpenAi's `RenderContent` and its own `new llm.Writer`.
- [ ] The text writer hard-coded in `text.Rendered` (`text/this.cs:165`).

**Dies at the start of stage 3:**
- [ ] The old stage 3: 7ce25c4b1's `is image.@this` in OpenAi, its `content.IsVariable` branch, `ImagePart`'s throw, and the render inside `ToApiMessages`.

**Not this branch:**
- the json writer's `EmitsSchema` bool (813, branch A), unless 3a's trace shows the plang output kind's entry takes it over for free;
- the channel's single `Mime` for both directions (`channel/this.cs:45`, `:54`). Ingi: each direction its own serializer. That belongs with the Service work (`test/plan/service-actor/`).

**Stays:**
- every value's own writing;
- the 800 door;
- the cut and the table (they move into entries);
- `LlmMessage.Images` as the named images;
- debug's own line cut (`%!debug.setting.length.max%`).

The verdicts are the contract; the order inside a stage is the coder's. If a verdict looks wrong against real callers, flag it back.

## What tests can't show (checks the coder runs and reports)

- The decider's and the builder's prompts are byte-equal (the decider pins).
- The llm tests' recorded answers don't move where the values they send are short.
- The locked surfaces (`IWriter`, `IReader`, kind, the registry) are re-pinned under 844 and 846.
- Every stage: the gate by name; PLNG001/002 at 0; the plang suite no worse than runtime2's.

## OBP validation

| new surface | check | result |
|---|---|---|
| `Bytes(byte[], kind)` | one door; a value says what it has, the writer decides | 844, to verify at 3a |
| the writer's output kind | an object, not a string; no value reads it | to verify at 3a |
| the pair registry | one place asks it (`data.Output`); no `is` or `switch` on a value; an entry is looked up, never tested | to verify at 3b |
| the llm kind of text | the llm module's own; falls to text under it | to verify at 3b |
| the rendered content | the llm module's type: the text and its named values; not a property bag | to verify at 3c |
| images and long values by name | one mechanism for both; each value sent once | to verify at 3d, 3e |
| `trim` | one member, each value answering what trimmed means for it | to verify at 4 |

The coder owns the code shape; the sketches here are intent.
