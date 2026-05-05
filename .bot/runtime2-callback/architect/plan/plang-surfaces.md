# PLang surfaces

The developer-facing PLang surface for callbacks. Three pieces.

## `%!error.callback%`

Synthetic, read-only, lazy. Available inside any error handler scope. The PLang path `%!error.callback%` reads through to `app.Errors.Current.Callback` — the property lives on the current error (`Error.@this` owns it). Reading it materialises an `ErrorCallback` record. See [variable-capture.md](variable-capture.md) for the throw-time semantics and [callback-schema.md](callback-schema.md) for the materialisation steps.

```plang
- insert into users, name=%name%
   on error call goal HandleError

HandleError
- write %!error.callback% to file callbacks/%!error.id%.bin
```

`on error call goal X` is the right shape — it cleanly separates the failure path into its own goal where steps can do whatever they need (write, log, queue, dispatch).

## `- run %callback%`

Consumes a callback. Calls `callback.Run(ctx)`, which verifies the signature, resolves the embedded `Goal` against the current build (mismatch = signed-but-stale → hard error), decrypts the payload, and runs from the captured `(StepIndex, ActionIndex)` with bound state.

```plang
Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

Decryption is internal to `callback.Run` — see [encryption-layering.md](encryption-layering.md).

## Ask-user `vars:` annotation

Step-level on issuing actions only:

```plang
- ask user 'What is your name?', vars: %orderId%, write to %name%
```

`vars:` is **not** a Step-level concept (rejected in design — would imply error-retry needs declared vars too, which it doesn't). It belongs to ask-family actions specifically.

The post-resume contract for ask-user is **lossy**: the developer reloads `%order%` from `%orderId%` on resume; only the names listed in `vars:` carry through the wire envelope.
