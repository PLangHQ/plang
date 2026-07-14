# W8 — delete KindHooks + the four `Build` statics: one construction door answers the kind

Settled with Ingi 2026-07-14. Appends to [`stj-collapse-work.md`](stj-collapse-work.md) as its own package — independent of W6/W7, land in any order. `file:line` verified against HEAD at trace time (dcb26e55a); re-verify as you go. **You own the mechanics.**

## Why

`X.Build(raw)` ("read the kind without constructing the value") is a hand-synced duplicate of each family's own construction: `number/this.Build.cs` re-implements `number.Parse`'s literal rules and has already DRIFTED — a dotted literal past decimal range: `Parse` → double (`this.Parse.cs:47-52`), `Build` → null (`this.Build.cs:65-71`). path/image/code are the same pattern (their scheme/extension/language knowledge already lives inside their own construction). The built value carries its kind, so the hook is a second answer to a question `Create` already answers — the one-door rule's exact target. It survived only because its sole caller (`KindHooks`) was parked in the zero-logic Stage-3 tail, and the "relocate during the kind redesign" reason expired when the redesign landed. This is also the LAST convention-discovered-static registry (`convert.*`, `kind.Of`, `FromWire` all died by the same law).

## The change

Both call sites build through the family's eager door and read the kind off the built value:

```csharp
// build/code/Default.cs:930  and  variable/set.cs:228-230
// was: context.App.Type.KindHooks.Of(clrType, raw)
var built = context.App.Type[typeName].Create(raw, carrier);   // the entity courier — same door type/this.cs:300 uses
var kind  = built?.Type.Kind?.Name;                            // decline (null) → don't stamp; kind decided at runtime
```

Mechanics you carry:

- A hook returned `null` to mean "no kind"; the courier can also land an ERROR on the carrier. Both are decline-to-stamp at these sites — never fail the build over a kind probe.
- Both sites' existing `%var%`-skip stays (unchanged).
- The sites currently hold a CLR type (`KindHooks.Of(underlying, …)`); the new call asks the entity. `App.Type[clrType]` exists too — pick whichever each site honestly holds.
- **Verify eager-build side effects**: the hooks deliberately avoided construction. path is the one to check — its `Create` resolves against context (fine at build time in principle, but pin it). If a family's eager build turns out to do real I/O at the builder site, surface it before landing.

## Delete

- `type/kind/Hooks.cs` (registry + reflection `Discover`)
- `App.Type.KindHooks` (`type/list/this.cs:58`) + doc refs (`type/this.cs:535`)
- `number/this.Build.cs`, `path/this.Build.cs`, `image/this.Build.cs`, `code/this.Build.cs` (code's `DetectLanguage` stays — its `Create` uses it)

## Pin

Builder stamps unchanged for the representative literals: `"42"`→int, `"3.14"`→decimal, `"1e3"`→double, `"photo.jpg"`→jpg, `"data:image/gif;base64,…"`→gif, `"https://x"`→http, bare path→file, `%var%`→no stamp. One deliberate behavior FIX rides along — name it in the commit: dotted-literal-past-decimal-range now stamps `double` (the drift case; was: no stamp).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| kind read off the built value | one construction door; no derive-beside-the-build | ✓ |
| the four `Build` statics deleted | knowledge stays where it already lives (each family's own construction) | ✓ |
| `KindHooks` registry deleted | last convention-discovered-static registry gone; nothing replaces it | ✓ |
