# coder v14 — stage 12a: the exception pass

Contract (start.md 12a): every `throw` in the code this branch touched is either plang itself broken, or becomes
an error in the result.

## The inventory
234 `throw`s (plus one rethrow) on lines this branch added in `PLang/` (base 14ab50d93), classified by reading
each in place (three read-only passes, every "B" re-read before it is changed):
- **A — plang broken (kept):** ~65. C# invariants: constructor guards, unreachable defaults, a Data stored as a
  value, a context-less ask, wrong-door writer calls.
- **B-caught, key kept:** most of the rest. An `AppException` keeps its key through the action catch
  (`goal/step/action/this.cs:321`) and the goal load (`goal/this.cs:375/380`, `Error.FromException`).
- **B-caught, key lost:** the program's mistake reaches the result, but as `ServiceError` / `Exception` /
  `RenderError` / `CannotSetChild` / `JsonSerializeError` instead of its own key.
- **B — escapes or loses its reason:** a small set (below).

What 12a means in practice: the action catch already turns every exception into a result. So a "B" is a
program-input throw whose **key** doesn't reach the result, or one that isn't caught at all.

## Slices
1. **Catches keep the key they caught.**
   - `goal/step/this.cs:134`: prefer `AppException.Key` over the type name.
   - Fluid `:127`: keep an `AppException`'s key, not "RenderError".
   - The json/plang encode catches: keep an `OutputException`/`NormalizeException` key.
   - `setting/this.cs:79`: keep `Apply`'s key.
   - `goal/this.cs:380`: keep a load key (`UntypedValue`, a `JsonException` → `PrInvalid`).
2. **A program's mistake is an `AppException` with its key.** Today these are plain `InvalidOperationException`s:
   - callback / snapshot restore (`Callback*`, `ProviderRestore`);
   - http SSE overflow, download too large, transfer too slow;
   - http request signing;
   - `OpenAi` image unreadable (carries `content.Error`);
   - `output.ask` with no input channel, and `Channel["x"]` not found;
   - `dict.@schema` write (`NotSupported`, which becomes `CannotSetChild`);
   - VarResolveCycle status 500 → 400.
3. **Answer instead of throw, where the owner already answers Data.**
   - The type's Make/Takes arm keeps a decline's reason (logged). The goal channel `Create` fails its data
     with `GoalChannelIncomplete` (logged).
   - `type.Create`'s declined conversion carries the carrier's key.
   - `path.Value` catches as `path.Create` does.
   - `directory.Contains` catches like `Value`.
   - json reader shape errors on the clr(json) → record paths.
   - number lowering: fractional → int, NaN/Infinity, biginteger of a double.
   - `debug.Activate` (grep regex, uncaught at the CLI).
   - Registry guards for a `code.load` DLL, checked inside `Add(Assembly)` (already Data).
   - `actor.setting` read of a value `Apply` refuses; goal list `Walk` setting; `step.Item.Set`; `ResumeFromWire`.
4. **Report, don't touch:** dead code found on the way (`number.Resolve`, `RegisterStartupParameters`,
   `Comparison.AsSign`, stream `:165`, `sqlite.CreateAsync`), and the `?` rows that need a caller to exist
   first.

Each slice has its own pins: a test per changed throw asserting the result's key. Gate: name-diff.
