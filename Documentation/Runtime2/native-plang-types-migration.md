# Migration: C# classes use native plang types, not CLR types

**Decision (Ingi, 2026-06-15):** production C# classes should model values in the
**native plang types** (`number`, `text`, `list`, `dict`, `item`, …) rather than CLR
types (`int`, `string`, `decimal`, `List<>`, `JsonElement`, …). We will go through
the classes one by one. **`llm/code/OpenAi.cs` is the pilot/test class.**

## Why
- A value renders/behaves itself. Code that reaches for CLR types re-implements what
  the plang type already owns and reintroduces CLR-shaped bugs (the `JsonElement`
  property-bag leak, the typed-null vs C#-null bug — both below).
- "All I/O goes through the channel": serialize/deserialize via the channel
  serializer for the content-type, never raw `System.Text.Json` on a value.

## Patterns established (use these)

### 1. Navigate a dict by typed path — `dict.Get<T>(path)`
`app/type/dict/this.cs` gained a sync, typed path getter:
```csharp
number n = dict.Get<number>("usage.prompt_tokens");
text? c  = dict.Get<text>("choices[0].message.content");   // dotted + [index]
var arr  = dict.Get<global::app.type.list.@this>("choices[0].message.tool_calls");
```
Returns the value as T (converting via the type system when a segment is still raw),
or null on a missing path. No `await`, no `.Value()`, no `.Clr<>` at the call site.
(C# can't have generic indexers, so it's a method, not `dict<T>[...]`.)

### 2. Typed-null vs C# null — the `.IsNull` idiom  ⚠ widespread bug
A value's null-ness lives on the value: `item.@this.IsNull` (virtual `false`),
`null.@this` overrides `true` (and `type.@this` overrides → `Name == "null"`).

**The bug:** `data.Peek()` can return the **null citizen** (`null.@this`), which is a
real instance — so `Peek() != null` (C# reference check) reads it as *present*. This
caused: the LLM cache false-hit (missing key → null citizen → "cached" → null answer),
and `ResolveConfigAsync` resolving a missing `llm.endpoint` to the literal string
`"null"` → http to `null:443`. Fixed both with `cached.Peek() is { IsNull: false }`.

**DONE (2026-06-15):** `Data.Peek()` is now **non-nullable** — `item.@this Peek() =>
_type ?? @null.@this.Instance`. Absent/no-value reads as the null citizen; absent-ness
is still distinguished via `IsInitialized`. Swept all `Peek() == null`/`!= null` sites
→ `.IsNull` / `!.IsNull` (and `X?.Peek() == null` → `(X?.Peek()?.IsNull ?? true)`).
Result: Modules 62→49, no regressions. (Note: `item.@this.Peek()` is a *different*
`object?`-returning method — untouched.) The `Peek()?.X ?? default` sites still
compile (redundant `?.`); a present-but-null value now stringifies to "null" rather
than hitting the default — harmless in practice (no test moved), tidy opportunistically.

### 3. http I/O through the channel (done, `http/code/Default.cs`)
- Request body: `Serializers.GetOrDefault(contentType).SerializeAsync(ms, action.Body)`
  → `ByteArrayContent` (trim the NDJSON framing newline). No raw STJ, no `.Clr<string>`.
- Request signature temporarily excludes the body (signing scheme being reworked).
- Response: the channel already deserializes to a `dict`; navigate it (Pattern 1).

## OpenAi pilot — status
- **DONE:** response parsing navigates the `dict` (`dict.Get<number/text/list>`), no
  `JsonElement`/`TryGetProperty`/`ParseApiResponse`/`ParseToolCalls`-on-JsonElement.
  `Value<dict>()` instead of `(await Value()) as dict`. Cache + config use `.IsNull`.
  Result: Modules 106→62, Runtime 57→48 (null-citizen fix cascaded to settings/grants).
- **STILL CLR (follow-up):** token counters (`int`), cost math + pricing table
  (`decimal`), message/HTTP-building. `number`/`text` aliases added; the numeric chain
  (counters/cost/pricing → `number`) is the next pass for this class.
- Aliases at file top: `number`/`text`/`item` (`list` clashes with the `app.type.list`
  namespace → fully-qualified at its 2 use sites).

## Next classes
After OpenAi's numeric chain, sweep the `Peek()` non-nullable change (Pattern 2) — it
unblocks the cleanest form everywhere — then take classes one by one off the
`Peek() != null` / CLR-type audit.
