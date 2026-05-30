# Coder v2 — plan (tester v1 response)

Address all 7 tester findings; broaden Stage 2 to complete `Data.Context` non-null per architect spec.

## F1 — back-refs non-null + Data.Context flip + Null type sentinel

- Flip `step.Goal`, `channel.Actor`, `channel.Channels`, `module.App`, `goal.App`, `error.App` to non-null `= null!`.
- Stamp Context at SQLite rehydration (Permission.Find: stamp `_actor.Context` on grants from `SettingsStore.GetAll`).
- Route Sqlite RehydrateValue through the entity's own `data.Type.ClrType` resolver (no second fallback chain).
- Route variable/set's ValidateBuild through `value.Type.ClrType`.
- Keep the legitimate fixture-supporting `App?.Type ?? GetTypeNameStatic` chains in `module/this.cs` Describe (test fixtures `new module.@this()` mint without App; that's the documented no-App surface).
- `type.@this.Promote()` throws on unstamped non-primitive reads — silent return was a footgun (would surface as wrong LLM prompts far from the bug). Primitive-fallback path marks `_foldLoaded = true` at construction so primitives stay reachable without Context.
- **Data.Context** flipped non-null (`_context = null!`, public `Context` non-null). Strip dead internal `_context == null` guards where defensive; keep `EnsureSigned`'s throw (real contract enforcement).
- **`type.@this.Null` sentinel** replaces `Data.Type == null` state. Wire converter skips Null emission. Type setter clears `_type` when assigned the Null sentinel so call sites copy unconditionally.

## F2 — real byte-diff golden

Pin SHA256 of `schema.ToJson(indent:false)` + `schema.TypeSchemas` to constants. Either input changing breaks the test. Plus length sanity-check so empty output can't accidentally match.

## F3 — distinguishable registry path

Use `"path"` instead of `"int"` — `path` is in the registry catalog but NOT in `GetPrimitiveOrMime` (asserted via guard). The registry resolves it to `app.type.path.@this`; static fallback returns null.

## F4 — actually read the entity

`set %name% = "alice", type=text/plain` followed by `assert %name!Type% equals "text/plain"`. Forces the entity to flow through `data.Type` and be navigated via the Properties accessor.

## F5 — registry index-miss with typed error

Use a literal channel name (`absent-channel-xyz`) so the registry actually misses, capture `%!error.Key%` in the `on error` clause, and assert `equals 'ChannelNotFound'` — proves the typed error, not "any error."

## F6 — flake fix

`[NotInParallel]` on `Stage0_BuildMethodTests`. The static `InvocationLog` makes a per-instance log infeasible (handlers are constructed per invocation), so serialise the fixture instead.

## F7 — real assertion

Move `ChannelWriteThroughAccessor.test.goal` to a subfolder with a sibling `Capture.goal`. Test sets `%captured%` in Capture, registers channel "log" → Capture, writes through it, and asserts `%captured% equals "hello from channel accessor"`. Pins that the accessor path actually reaches handler code.

## Tests

- C# (TUnit/.NET 10): 3694/3694 passing.
- PLang: 253/253 passing.
