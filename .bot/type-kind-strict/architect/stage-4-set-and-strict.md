# Stage 4: `variable.set` takes `type`, and strict validation

**Goal:** Change `variable.set.Type` from a `string?` to a `type`, read `name`/`kind`/`strict` off it in the handler, and enforce `strict` in the build-validation seam — error for verifiable mismatches, warn (or nothing) otherwise, `%var%` deferred to runtime.
**Scope:** Included — `app/modules/variable/set.cs` (`Type` param + `Run` + `ValidateBuild`), the `IKindValidatable` implementation on `image`, the strict-validation call in `build.validate`/`ValidateBuild`. Excluded — the LLM prompt teaching (stage 5).
**Deliverables:** `variable.set.Type` is a `type`; `Run` reads `Type.Value.Name` for CLR resolution and carries the whole `type` (kind included) onto the minted variable; `ValidateBuild` enforces strict via `IKindValidatable`; `image` implements `IKindValidatable`.
**Dependencies:** Stages 1–3.

## Design

- **`Type` becomes a `type`.** Today it's `data.@this<string>? Type`. After: the parameter carries a `type` value (`{name, kind, strict}`) the LLM constructed. `Run` reads `Type.Value.Name` to resolve the CLR type via `Context.App.Types.Get(name)` (the registry — not a public `ClrType`), converts the value, mints `Data<T>`, and sets `minted.Type = Type.Value` — the **whole** type, so the kind survives (this is the dropped-kind bug from stage 1, now fixed by construction).
- **Strict enforcement lives on the type, called from `ValidateBuild`.** `variable.set` already implements `IBuildValidatable.ValidateBuild` and already skips `%var%`. Extend it: when `Type.Value.Strict` is true and the resolved CLR type implements `IKindValidatable`, call `ValidateKind(value, requiredKind)`. Match → ok; mismatch → return a build error. When the family doesn't implement the marker (e.g. `text`), strict only asserts the kind name is known. Default (not strict) stamps the kind and validates nothing — at most a warning on a surprising kind.
- **`%var%` values defer to runtime** — `ValidateBuild` already returns null for them. The same `IKindValidatable.ValidateKind` call fires at runtime in `Run` (after resolution) when strict, producing a typed error on mismatch.
- **`image` implements `IKindValidatable`** — sniff the bytes (it already uses ImageSharp `Identify` for dimensions; the same path yields the actual format), compare to the required kind. `text` does not implement it.

Per [plan/kind-derivation-and-validation.md](plan/kind-derivation-and-validation.md), this is the table: literal + sniffable + strict → build error on mismatch; literal + unverifiable → name-known check only; `%var%` → runtime; default → stamp + maybe warn. No per-format switch in `variable.set` or `build.validate` — they ask the type.
