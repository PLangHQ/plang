# Stage 2 — `TryConvert` removal is entangled with the `Json.cs` sweep (coder → architect)

Branch: `navigation-driven-record-builder` (wire-source-split now merged). Discovery pass on "what's left in Stage 2–3" after the merge.

## First, the good news — more is already done than the plan markers implied

- **`catalog.@this` is GONE** — no `type/catalog/` dir, no class, only stale comments. Stage 3's headline deletion is done (the `type/list/view` catalog *view* still stands — plan retires it at Stage 4).
- **The convert hub is GONE** — `convert.OfStatic`/`Of`/`Invoke`/`Discover` deleted (one comment remnant).
- `type.Build`, `data.FromRaw`, the compare pass — done and confirmed.

## The finding: `TryConvert` and "the 13 `Json.cs` converters die" are ONE piece, not two

The plan lists them as separate Stage-2 deletions. Tracing `TryConvert` (`type/list/Conversion.cs:131`, ~400 lines) shows they interlock. `TryConvert` is the **CLR-boundary converter** ("any value → an arbitrary CLR type"). Its stages and their intended destinations:

| stage (`Conversion.cs`) | what it does | destination |
|---|---|---|
| `:134` null / `:138` IsAssignableFrom | trivial | stays (or inlines) |
| `:205-237` `entity.Create` | construction | **already IS `Create`** — done |
| `:168-183` `itemValue.Clr(target)` | a plang value lowers itself | stays → `item.Clr` |
| `:247+` `FromWire(string,kind?)` | snapshot/crypto wire rebuild | plan Stays-list ("likely stays") |
| **`:264-305` `JsonSerializer.Deserialize(str, target, GoalReadOptions)`** | **string→record via STJ** | **← a `Json.cs`-family user** |
| `:307+` list-wrap (scalar→list, CLR `IList`) | list construction | `list.Create` |
| `Convert.ChangeType` (primitive) | primitive lower | stays → `item.Clr` |

**The knot:** `TryConvert`'s string→record path is `JsonSerializer.Deserialize(…, GoalReadOptions)` — an STJ deserialize firing the type-entity + host converters. That's the *same* work as "the 13 `Json.cs` converters die / read through the `*`-kind `Read`." So `TryConvert`'s STJ-deserialize is one of the converter-firing sites; you can't cleanly kill one without the other.

Corroborating site — **`dict.Clr` (`dict/this.cs:365`)** still does `JsonSerializer.SerializeToUtf8Bytes(this)` for a CLR-record target, and its own comment says *"dict's attribute strip waits on `Create` owning record construction (that todo)."* So `dict/Json.cs` is **blocked on record-construction** — the record builder this branch was named for.

## Caller status

- **`type/this.cs:304`** already routes through `Create` FIRST (`Create(lowered, carrier)` at `:300`); `TryConvert` is only its no-family **CLR-mate fallback** (a CLR record/enum/`List<T>` no plang family owns).
- **`setting.Set` (`setting/this.cs:102`)** goes straight to `TryConvert` — the CLI `--flag` convert-walk binding a raw settings value to `prop.PropertyType`. Does NOT try `Create` first.

## What I need ruled

1. **Scope `TryConvert` removal *with* the `Json.cs` sweep as one coordinated Stage-2 piece?** They share the STJ-deserialize site; splitting them half-migrates the CLR boundary.
2. **Where does string→record go?** The `*`-kind `Read` (format-agnostic, the Stage-1 machinery) is the obvious home — a string→record is bytes → `json.Reader` → `Read(target)`. Confirm that's the reroute for `TryConvert`'s `:264` STJ-deserialize and `setting.Set`'s complex-prop binding, vs a slimmer converter keeping a narrowed STJ.
3. **`FromWire` + list-wrap** — stay inside a slimmed `TryConvert` (a real CLR-boundary "convert to CLR type X" door for settings/params), or relocate (`FromWire` to its type, list-wrap to `list.Create`) and dissolve `TryConvert` entirely?
4. **`dict/Json.cs` (and any converter blocked on record-construction)** — mark `[Obsolete]` and leave for the record-builder branch, or is record-construction-via-`Create` in scope here?

## My lean

`TryConvert` shouldn't fully dissolve — it's a legitimate **CLR-edge** door ("bind this value to CLR type X" for settings/params). But its **construction + STJ-deserialize** middle should go: `setting.Set` and the `type.this` fallback try `Create` first; the string→record case reroutes to the `*`-kind `Read`; what remains is the thin `item.Clr` / primitive-`ChangeType` lower + `FromWire` + `list.Create`. The 13 converters then die per-site EXCEPT `dict` (blocked on record-construction — mark it, defer to that branch).

Order I'd take on your ruling: `setting.Set` → `Create`-first (contained, safe), then the string→record reroute to `*`-kind `Read` (kills the STJ-deserialize + the entangled converters), then the per-converter strip.
