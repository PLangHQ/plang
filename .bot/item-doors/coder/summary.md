# coder — item-doors — summary

## Version
v3 (stage 2: one door for writing; item-doors 107–122, kind-is-type 844)

## What this is
A value writes itself in one place, its `Output`, and writes each member through the writer's output kind:
`writer.Kind.pair.Output(member, writer, mode, context)`. The output kind holds a pair for a value's class when that
value looks different in it (a list in formal, a type in plang). Stage 1 (v2, report `v2/report.md`) made every value
born with its type; stage 2 removes the second way of writing (the sync `Write`) and the writer-side type switches.
Decisions are numbered per branch in `.bot/item-doors/architect/summary.md`.

## What was done
- **(b) the writer's kind**: `IWriter.Kind` (a kind object) replaced `string Format`; formal's kind is
  `[Format("formal")]` on `action.list`.
- **(c1) pairs**: held by the output kind (`app/type/kind/pair/list/this.cs`), keyed by class, found from
  `<type>/format/<kind>` classes; pairs: list/action/action.list in formal, type and kind in plang (119, 121).
- **(a) every Write is Output** (be8191375): 49 sync `Write` overrides folded into `Output`; base `Output` is the
  reflection kind's; type's identity is an internal `Write` (122). The writers' item/data/dict/list `Value` arms went.
  A variable's code pieces and the `.pr` variable list write async through the door, so the formal reader's parse
  chain is async (Read and its three callers). The ~20 item members (a) missed were routed through the door in
  a89b3f12f.
- **(d) the byte door** (ba28490c2): `IWriter.Bytes(byte[], kind)` replaces `Bytes(byte[])` and `Raw(byte[])`;
  image, binary, content and source send their kind; the writer decides (json: a kind it owns verbatim, a text kind
  as text, else base64; text: text or `[N bytes]`; formal: text or base64). Content's and source's `Owns` checks went.
- **(e)** (fc0118d94): json's `Value` keeps C# primitives and byte[]; no production caller handed it a collection.
- Tests: C# green except Wire's six snapshot/resume reds (red before the branch); plang 277 pass, the same 11
  long-standing reds, 12 stale. Surface re-pinned under 107 and 844.

- **Stage 3 begun (129)**: PLNG006 (dab4a8459), a generator error for a value written around its output kind
  (`x.Output(writer, …)` on an item other than this/base, outside the door and a pair's own value); it runs on PLang
  and every test project. The record door (d5f3f500e): `BeginRecord(record, mode, context, layer)`/`EndRecord`, async,
  empty by default; `EmitsSchema` gone; plang writes with its own writer (`wire/kind/plang/writer.cs`), json.Writer is
  item's json only.

- **Stage 4, a plang value read as tokens (fe0b854e7; 133, 135, 136)**: `type/format/item/reader.cs` walks a value
  (leaf / sequence / members); `item.Read` reads every value as tokens by the target type's reader, `item.Set` asks
  `Reader.Reader` (own reader, else `Record`). Eight pure record readers deleted (Record makes them); the list reader's
  elements go through `Reader.Reader` (a `list<message>` holds messages). A step's `code` takes actions only (any
  other value: `NotAnAction`); the json round trip (`FromJson`) is gone. `IReader.Format` removed. The json helper and
  the list's elements stay json-bound on purpose (lazy children, 135).

- **Stage 5, a type's members through its own list (b233e0b66, 963b65b5a, 5a1d7f35c; 138, 139, 143)**: every member
  plang reaches (a %path%, a list query's field, a liquid template in os/ or docs/) carries `[LlmBuilder]`, found by a
  probe in item.Get; the type list keeps one marked-member list per class (`Member(Type)`), which a value reads its
  members from and a type's offer is made of; `property` holds its C# member and reads/writes itself (Write through
  `Reader.Reader`), `IsDefault` asks the value (async). `item.Set` answers a Data (NoAddedMember for a plain value's
  added member — Ingi's open question). Identity's PrivateKey is no longer navigable (a security finding the architect
  logged).

## Next
- The values born with item's no-type constructor (stage 1's leftover): listed by family with a fix each, waiting on
  the architect's order and his call on the app's Type and the "type is its own type" break.
- JSON Schema as pairs (129 #2) waits on Ingi: `pair.list[System.Type]`, `json.Writer(utf8, kind)`,
  `[Format("schema")]` on type. The llm table (#3) waits on the llm-writer merge.
- **(c2) the identity pair** — waits on Ingi's yes to the `Written` virtual on item. With it: wire's two Owns sites
  go, the writers compare their own Kind, `Owns` leaves kind (locked removal, plan-approved; re-pin citing 107).
- The memory as `list<variable>` (103) waits on Ingi.
- `IWriter.BeginRecord/EndRecord` stay (stage 3's record door).

## Code example
```csharp
// a value writes its members through the writer's kind (the door), never member.Output directly
public override async ValueTask Output(IWriter writer, View mode, context? context)
{
    var pair = writer.Kind.pair;
    writer.BeginObject();
    writer.Name("token"); await pair.Output(Token, writer, mode, context);
    writer.EndObject();
}

// bytes say what they are; the writer decides how they look
writer.Bytes(Bytes, Type.kind);
```
