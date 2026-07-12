# Coder implementation findings — for architect (decision needed)

Branch: `wire-source-split`. Core rewrite (§1–§9) is implemented and compiles; committed as a
WIP checkpoint alongside this note. Verified against a **true full-timeout baseline** (the
`dev.sh full` baseline was truncated by the 15s suite cap — Wire/Data got cut off — so I
re-captured Wire/Data/Types at 240s; the name lists in `coder/baseline/` are the truncated
ones, superseded by `/tmp/basefull_*` this session).

Result so far: **14 of 20 rewrite-introduced regressions root-caused and fixed. 6 remain**,
and 3 of those are a design fork that is the architect's call (below).

## Three root-caused fixes (2 within plan intent, 1 supersedes §1 Write)

1. **Option B missed variable-typed string slots.** `Make.Param("Value","%myVar%","variable")`
   loads through the data reader; the plan's wire arm (`Slice` → capture door) bypasses
   `type.Create`'s variable-resolution branch (`type/this.cs:259`), so `%myVar%` never became a
   `Variable` and `variable.set` failed "not found". **Fix:** the content-door gate now also
   covers variable-typed strings, not just `Template != null`:
   ```csharp
   else if (reader.Peek() == TokenKind.String
            && (typeRef.Template != null
                || ctx.Context.App.Type[typeRef.Name]?.ClrType == typeof(app.variable.@this)))
       value = typeRef.Create(reader.String(), ctx.Context);   // content door
   else
       value = typeRef.Create(Slice(), ctx.Context, Transport); // strict wire
   ```

2. **Merged tail dropped `context`.** Collapsing the born/deferred arms to `new Data(name, value)`
   lost the `context: ctx.Context` the old deferred arm carried. Source/wire materialization
   renders templates + resolves `%refs%` against `data.Context`; a null context left templates
   literal. **Fix:** `new Data(name, value, context: ctx.Context)`. (The goal.call born arm never
   passed context but is unharmed by gaining it.)

3. **Materialize-on-write (§1 Write) is unsound — supersede it.** `Read().Write(w)` threw on a
   `text/plain`-named type (no `(type,kind)` reader) → **truncated documents** (the whole
   compression cluster: `CompressDecompress_*`, `CompressedBytes_OnceGunzipped`, `Cut2_InnerBytes`,
   `Decompress_ArchivedData`). The key realisation: **under option B there are no structured
   content sources.** A native dict/list is held as `dict.@this`/`list.@this`; a still-encoded
   slice is a `wire`. So a *content source*'s `_value` is ALWAYS a string or `byte[]` — there is
   nothing structured to render at write, and materialize-on-write only ever adds a failure mode
   (a mime-named type with no reader). The plan's own §1 rationale ("a `{number}` literal writes
   42 inline") is already satisfied by **wires** (a number token rides as a wire, writes verbatim).
   **Fix — Write is just:**
   ```csharp
   public override void Write(IWriter w)
   {
       if (_value is byte[] b) { w.Bytes(b); return; }
       w.String(_value.ToString() ?? "");   // content is content: text/template/path/scalar, quoted
   }
   ```
   This needs the architect's ratification — it deletes the materialize-and-delegate design and
   the "authored container literal writes as a list" property (which is moot: such literals are
   wires now). **Recommend accepting** — it's simpler, and the tests prove materialize-on-write
   corrupts documents.

## The 6 remaining — 3 are the string→wire fork, 3 are issue-1-pending

### Fork (3 tests): text-literal string slots ride as wires, and their raw form is quoted JSON
`StartGoal_Programmatic` outputs `"Plang"` **with quotes**; `ResolveValue_StringInterpolation`
and `AsT_DictWithNestedVars` leave `%user%`/`%prompt%` literal. Root: a plain `{text}` literal
`"Plang"` (Template==null, not variable) routes to a **wire**, whose `Raw` is the quoted document
slice `"Plang"`. Display/output consumers that read the raw (the plan's flagged "Peek consumer
audit") get the quotes / unresolved template — this is exactly the coder v1-review "casualty",
now concrete.

This is the **same string→content vs string→wire fork from v1**, and it needs a ruling:

| Option | Fixes leak? | Strictness (`"23"`/{number}) | Cost |
|---|---|---|---|
| **A — text-faced → content** (text/datetime/guid/path string tokens → content door; number/bool/structured stay strict wires) | yes | preserved (a string under {number} is still a wire → fails at the number pull) | a carve-out that reintroduces some "which types read a string" knowledge option B tried to shed |
| **B — all strings → content** (coder v1) | yes | **lost** — `"23"`/{number} parses to 23 (may break a strictness fixture) | simplest; string byte-identity gone (was never real — `RawValue` decodes) |
| **C — keep option B, fix consumers** | via consumer audit | preserved | leak surface may be wide; every raw-reading display/output consumer must materialize first |

**Coder recommendation: Option A.** It keeps Ingi's strictness ruling for number/bool/structured
(a genuinely mismatched token still fails at first touch) while ending the quote-leak for values
whose canonical JSON form *is* a string. The carve-out is small and honest: "a string token is
content only when the declared type's value is itself a string." Detectable without invoking the
reader (the type's json face is a string for text/datetime/guid/path; a number/bool/object/array
face makes an incoming string a mismatch → strict wire).

### Issue-1-pending (3 tests): `DictOfTypedEntries_StoreRoundTrip`, `PlanDict_StoreRoundTrip`
(nested typed dict → null), `Cut2_ConfigJson` (object/json nav → null). I have **not** landed
`object/serializer/json` → `ITypeReader` + `TypeOf` (issue 1/2) yet; these likely clear once it
lands. Note the tangle issue-1 under-specified: `object/serializer/` already has a `Reader.cs`
(ITypeReader at `Kind="*"`, via `parser.ReadSlot`), and the static `json.Read` has 3 real callers
(`item/kind/json/this.cs:69`, `item/serializer/json.cs:31`, plus a doc ref). So it is an ADD of a
`(object,json)` typed reader that parses the json-string, not a straight static→instance swap.

## Ask

1. **Ratify the Write simplification** (fix 3) — accept, or is there a case for structured content
   sources I'm missing?
2. **Rule the string fork** — A (recommended), B, or C.
3. **Issue-1 ordering** — land it next (likely clears the other 3), or settle the fork first (it
   may change nested-string-in-dict behaviour too)?

Everything is committed on the branch tip so the code state is inspectable.

— coder
