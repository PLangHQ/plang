# codeanalyzer v2 — kind-is-type (stages 4–6 + 814 + stage-5 C# + security F1)

**Scope:** everything landed after v1 (`82d8866ae`) up to HEAD `aa353656b`. **Method:** forced build (`rm -f .devsh-stamp*; ./dev.sh build`, exit 0); every touched core type read in full and callers walked; a compiler-backed CS0108 hide sweep; `obp-smells.md` names for anything raised. `Tools/ObpScan`'s MSBuildWorkspace still can't load `PLang.csproj` here (NuGet prune/vuln warnings abort the project model — tooling, not the branch), so the mechanized sweep was full-read + grep + the real compiler.

**Deferred, confirmed present, not re-raised:** F8.1 (the action-slot name checks), the `source.cs:115` text-face floor (both branch A's list), the 6 Wire snapshot/resume tests (fail identically on runtime2), `After_EachRetry_IsAFreshAttempt_WithAFreshDeadline` (timing under load).

---

## Verdict: CLEAN — PASS

The stage-4–6 shape work is OBP-exemplary and merge-safe. **No new findings.** The central "json rests, a use opens" change lands as a single `data.Open()` door with parse-once; the navigator is gone with no value-navigation residue; the new `OwnInternal` hook follows the established `Own*` pattern on the locked surface (decision-approved, loop-free); file.read's Type slot keeps the carrier whole; F4.2 is done; and **my v1 F1–F3 are all fixed.** The branch compiles clean (0 errors, PLNG001/002 at 0) and introduces **no new CS0108 hide in the locked core**.

**Next bot: tester.**

---

## What was verified

### 1. json rests, a use opens — ONE door ✓ (the v2 crux)
- `data.@this.Open()` (`data/this.cs:322–338`) is the **single** parse-once door: `Value()` → if a resting `source`, `rest.Parse(ctx)` once → `parsed.history.Add(rest)` (the source rides as history) → narrow `_item` → return. A bad parse answers `Fail(rest.Unread(ex, this))` with a **narrowly-scoped** catch (`JsonException`/`FormatException`/`InvalidOperationException`/`NotSupportedException`), never a throw to a courier.
- Every use-site calls that door — none re-implements parsing: `EnumerateItems` (`:505`), `Compare` (`:636`), navigation (`this.Navigation.cs:41`), `Value<T>` (`:557`), the `is` operator (`Operator.cs:182`), the identity decode (`this.cs:431`), and `source.Get` (`source.cs:335`). No broken-seal, no stored-twice.
- `source.cs`: the courier door (`source.Value`, `:161–261`) keeps a resting kind unread (`:217 if (loaded.Peek() is source) return this`); `Declared` (`:316–319`) lets the source own its own re-typing (no type entity cracking its raw); `Parse` (`:288–289`) uses the **parent-scoped** `Kind(_type, ctx)`, never the by-name lookup; the catches are narrow. The `_format` field carries the raw's own format across a re-declaration (818/758) — the declaration is still held WHOLE (`:20–22`), not shredded.

### 2. The navigator is gone with no value-nav residue ✓
- `type/item/kind/json/this.cs` is slim: `Load` rests (returns an unread `{item, json}` source carrying `format: this`), `Parse` is json's own parse into plang values (object→dict, array→list, scalar→native) over the type readers — **no `JsonElement` carrier, no navigator** (Descend/Data/Enumerate/IsSequence/Set/Clr/Output are gone). The `Read` format→type bridge (`:109`, still a legitimate `(JsonElement)obj`) is **kept by design and reachable** — `type/clr/this.cs:131` `Kind.Read(Value, reader, kind, context)` delegates the clr carrier's lower here; that `JsonElement` is the carried host-kind machinery (673), not a stage-4 miss.
- No new app-wide by-name kind search: the by-name `Kind(string)` entry is deleted (38077d61c); `source.Parse`, `text.Format`, `size`/`duration` all use the parent-scoped `Kind(type, ctx)`.

### 3. The `OwnInternal` hook (814/831) — the `Own*` pattern, loop-free ✓
- `type/this.cs:736` `Internal { get => OwnInternal ?? kind.OwnInternal ?? Family._internal; }` — the same 777 shape as `Property`/`Values`/`Offers`, terminating on the family's **stored field** (`_internal`), so no type→kind→Family→kind loop. `OwnInternal` (`:652`) defaults null; the host kinds (`kind/{list,reflection,dict}/this.cs:15/15/18`) override it `true`, so a kinded `{item,dict}` answers Internal through its kind while `{item,json}` stays offered. This is a **decision-approved** (831) addition to the locked `type` surface — verified as shape, not raised.

### 4. file.read's Type slot (814/832/837/838) — carrier whole ✓
- `module/file/read.cs`: `Build()` returns the `Type!` carrier **whole, unopened** when typed (`:56–62`), and `Typed()` is one shared check (`:25`). `Start()` passes the raw `byte[]` to `declared.Create(raw, …)` (`:38`) — `Create(object? raw)` **is** the type's birth-door contract (the one runtime crossing), so this is the designed crossing, not an opened-box-to-a-helper. Clean.

### 5. render (7574cd6d3) ✓
- `type/item/text/this.cs`: the text asks its own kind via the parent-scoped `Format(ctx) => Kind(Type, ctx) ?? Type.kind` (`:256–257`), opens its characters through that kind once and caches (`_opened`, `:281–291`) — no `writer.Format` switch in the value, no per-format table. The value writes what it is; the writer decides how.

### 6. Stage 6 — the stopgap and my v1 F1–F3 ✓
- The `%x!type.kind%` clr-carrier stopgap (80e4c9e4e) is gone; **F4.2** is done — `type/this.cs:570–571` `ToString => kind.IsEmpty ? Name : $"{Name}<{kind.Name}>"` for **all** kinded types (the old list/dict/choice name-switch is gone).
- **My v1 findings are all fixed:** F1 the dead `TypeDescription` const — gone; F2 the stale `BuildTypeEntries`/`Promote` class doc — gone; F3 `subclass.Offers` — now binds the static `Offer` **once at construction** (`subclass/this.cs:14,20`) and `Offers` reads the field (`:39`), no per-call reflection.

### 7. Stage-5 C# and security F1 ✓
- The only stage-5 production C# (0fda873ec) is the builder's kind follow-up: `KindsOffered` (`goal/step/pick/list/this.cs:635–641`) offers `picked.kind.list(ctx).Items().Where(k => !k.kind.IsEmpty && !k.Internal)` — each kind answers its own `Internal` (the 814 hook), no name-list; d3138cf16/50e83a808 are teaching (`.md`), out of C# OBP scope.
- Security F1 (`d9bc6aae7`, read for shape only): `goal/serializer/Reader.cs` refuses a `.pr` `file` that is more than a bare name (separator / `.` / `..` / rooted) via `PrFormatOutdatedException` — the reader-throws-its-refusal pattern, caught at the load boundary; reinforces 745/767 at the `.pr`-read layer.

### Compiler-backed hide sweep (the 776 discipline)
`dotnet build PLang -t:Rebuild` → **0 errors**, PLNG001/002 clean. 90 `CS0108` across the whole project — all the pre-existing node/verb duality + static-`ICreate` pattern. In the stage-4–6-touched **core** files (`type/kind/*`, `kind/json`, `source.cs`, `data/this.cs`, `Operator.cs`, `file/read.cs`): **none**. The only core hides are `type/this.cs:140/248/609` — the same `Template`/`Create`/`Is` members that exist at the runtime2 base (verified in v1; lines shifted by edits above them) — and the pre-existing `Registry.NameOf`. **No new hide introduced.**

---

## Findings

**None.** Zero new OBP violations in the v2 scope. The two deferred items (F8.1, the floor) are intentional holds on branch A's list, confirmed present and recorded there — not re-raised.

### Observations (not findings)
- `json/this.cs:109` `Read` keeps a `(JsonElement)obj` cast — carried host-kind machinery (reached via `clr.cs:131`), slated for 673, not a stage-4 miss.
- `OwnInternal` is a decision-approved (831) addition to the locked `type` surface; shape verified against the `Own*` pattern.

## Files read in full / swept
`data/this.cs` (Open/Value/use-sites), `type/item/source.cs`, `type/item/kind/json/this.cs`, `type/this.cs` (OwnInternal/ToString/Internal), `type/kind/subclass/this.cs`, `type/item/text/this.cs` (render), `module/file/read.cs`, `type/clr/this.cs:131`, `goal/step/pick/list/this.cs` (KindsOffered), `goal/serializer/Reader.cs` (security F1); CS0108 swept across the whole PLang project.
