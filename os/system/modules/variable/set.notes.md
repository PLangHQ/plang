`set %x% = …` is ONE `variable.set`. Emit the `Type` parameter only when the step gives an explicit `as <name>[/<kind>] [strict]` or `(<kind>)` annotation (see the Type reference block); otherwise omit and let the runtime infer.

`set %x% = … as default` / "default to" / "only if unset" → plain `variable.set` with `{"name":"AsDefault","value":true,"type":"bool"}`. NEVER `code.setDefault` (that picks a signing/crypto provider — unrelated to assigning a `%variable%`).

`<producer>, write to %x%` → the producer + a trailing PEER `variable.set(Name=%x%, Value=%!data%)` (never a modifier). Don't add a separate `Type` — the producer's `Data<T>` (its `→ returns` line) carries it.
