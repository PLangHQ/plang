# Demolition audit — clr + kinds

What becomes dead or redundant once the `clr` + `Type[t].Kind[k]` change lands, so nothing hangs around confusing the tree. Grounded against HEAD source (file:line). Confidence tags: **[dead]** delete outright, **[replace]** rewrite in place, **[candidate]** collapses but verify during impl, **[stays]** do not touch, **[deferred]** dies in a later branch — not now.

Companion to `plan.md` / `code-draft.md`.

---

## Dies with v1

### [dead] Delete outright

1. **`clr.Navigate` — the `Value is Data` branch.** `type/clr/this.cs:79-80`:
   ```csharp
   if (Value is global::app.data.@this innerData)
       return await innerData.GetChild(key);
   ```
   Dead: the `clr` ctor (`type/clr/this.cs:26`) throws if a `Data` is passed as the value, so a `clr` can never wrap a `Data`. This branch is unreachable today — delete it independently of everything else.

2. **`clr.Navigate` — the reflection body.** `type/clr/this.cs:82-103` (the `GetProperty` bottom-up loop + `TargetInvocationException` handling). Relocates verbatim into the `*` (reflection) kind. `clr.Navigate` becomes pure delegation to `Type[type].Kind[kind].Navigate(...)`. Nothing reflective remains on `clr`.

3. **`OpenAi` — the per-format result ternary.** `module/llm/code/OpenAi.cs:463`:
   ```csharp
   object? resultValue = effectiveFormat == "json" ? TryParseJson(extracted) : (object?)extracted;
   ```
   Replaced by `context.Ok(extracted, kind: format)`. The `format == "json" ? …` fork dies (see §producer).

### [replace] Rewrite in place

4. **The reader's narrow-to-dict.** `type/object/serializer/json.cs:33`:
   ```csharp
   return new global::app.type.item.serializer.json(ctx.Context).Parse(parsed);   // → dict
   ```
   → `return new global::app.type.clr.@this(parsed, ctx.Context);` (kind derives json). The `Parse` *method* stays (many other callers, §stays); only this call site changes.

5. **The deferred-read token-shape guess.** `data/reader/this.cs:79-80`:
   ```csharp
   deferredFormat = reader.Peek() == TokenKind.String ? Text.Mime : "application/plang";
   ```
   Route by the declared type/kind instead — once §5 keeps the value's real type, this coarse guess is wrong for `%plan%`. **Sensitive:** this is the same line the `variable-as-value` fix touched. Verify the full-match `%ref%` → `variable` path (born in `type.Build`, `type/this.cs:265` — a *different* branch) is untouched.

6. **The apex mask at `variable.set`.** `module/variable/set.cs` Type clause. This is a *guard addition*, not a deletion: declaring `object`/`item` (the apex) must stop demoting a value's richer intrinsic type. Listed here so the change is tracked, but no code is removed — a condition is added.

### [candidate] Collapses — verify, likely a big cut

7. **`OpenAi.RestoreFromCache` — the manual value reconstruction.** `module/llm/code/OpenAi.cs:989-1067`. Today it hand-rebuilds the cached value across three shapes — native `dict` (≈1000), `Clr { Value: JsonElement }` enumerate (1009-1027), `Clr { Value: Dictionary }` (1028-1036) — *because* a `JsonElement` doesn't survive cache serialization (the store comment at 468-477 says so). Under `context.Ok(raw, kind)`, the cache stores the **raw response + kind** and restore is `Kind[kind].Load(RawResponse)` — the single parse owner. The three manual branches and the `["Value"] = result.Peek()` store (478) collapse to a raw-reload. ~40 lines. This is the biggest confusion-remover in the change.

8. **`OpenAi.ParseResultValue`.** `module/llm/code/OpenAi.cs:821-835`. The authoritative cache re-parse of `RawResponse`. Folds into `Kind["json"].Load` (same single parse owner) — don't keep two json re-parse paths.

9. **`OpenAi.TryParseJson` — the method stays, one call dies.** `module/llm/code/OpenAi.cs:837`. Keep it for the validation-retry ("is this valid json", calls at 393/399). Only the result-construction call (463, item #3) goes. Do **not** delete the method.

### [candidate] Build-internal — scope-check with coder

10. **`build/code/Default.cs` — the dual-path step readers.** `GetString(object step, string key)` (845-852) and `SetValue` branch on `step is IDictionary` **or** `step is JsonElement`. If the build path navigates steps as `Data` (`%step.key%`) rather than reaching into a raw `JsonElement`/`dict`, these dual-path helpers simplify to one. Verify whether build still handles raw `JsonElement` steps before cutting — may be out of this branch's scope.

---

## [stays] Do NOT delete — the trap list

- **`item.serializer.json.Parse`** — the universal DOM narrower. Callers: `Data` ctor (`data/this.cs:206,315`), `type.Create` (`type/this.cs:483,585`), the `dict`/`list`/`object` `Reader.cs`, `ui/code/Fluid.cs`. Only the *reader* call site (item #4) is replaced.
- **`TryParseJson`** — validation-retry (item #9).
- **`Segment.Index.ResolveKey`** (`variable/path/Segment.cs:61`) — the one bracket-variable resolver. The kind's walk calls it; do not write a second.
- **`app.variable.path.Parse`** — the one plang-path tokenizer.
- **`catalog/Conversion.cs` JsonElement arms** (e.g. 339, 467) — the `as dict` / convert path. Legit; becomes the `dict`-from-json converter later.
- **`data/this.Diff.cs`** JsonElement handling — snapshot/diff comparison. Legit.
- **`GoalCall.cs:75`** JsonElement — `goal.call` parsing. Legit.
- **`clr.Output` / `clr.Write`** — serialization. Unchanged.
- **The double-wrap guard** — `clr/this.cs:26`, `type/this.cs:445`. Keeper (and it's what makes item #1 dead).
- **Native `dict`/`list` construction** for plang-authored values (`%x% = {a:1}`) — stays native.

---

## [deferred] Dies in a later branch — leave it now

These are **not** touched by v1. Deleting them now, before their own branch, breaks the tree.

- **`identifiers → text` branch:** the `string Kind`/`Name` fields on `type.@this`, the wire-serializer `writer.String(Name)` paths (`type/this.cs:43-44`), the primitive alias/canonical tables. Change *then*.
- **`Peek → item.@this` branch:** `source.Peek()`'s raw-CLR return (`item/source.cs:90`) is a deliberate contract (a source's sync face is its unparsed raw). Change *then*, as a `source` pass.
- **Convert consolidation:** once `Type[t].Kind[k].Convert` lands with the first real converter, any ad-hoc convert path can fold onto it — later, not v1.

---

## The one-line summary

The confusing hangers this change clears are: the unreachable `Value is Data` branch (#1), reflection living on `clr` instead of a kind (#2), and — the big one — OpenAi's cache reconstruction gymnastics (#7/#8) that only exist because a raw `JsonElement` couldn't round-trip. Everything else is a call-site swap or stays. Nothing in the [stays]/[deferred] lists should move in this branch.
