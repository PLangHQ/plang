# PLang surfaces

The developer-facing PLang surface for callbacks. Three pieces.

## `%!error.callback%`

Synthetic, read-only, lazy. Available inside any error handler scope. Materialises a `Callback` record on read. See [variable-capture.md](variable-capture.md) for the throw-time semantics and [callback-schema.md](callback-schema.md) for the materialisation steps.

```plang
- insert into users, name=%name%
   on error call goal HandleError

HandleError
- write %!error.callback% to file callbacks/%!error.id%.bin
```

`on error call goal X` is the right shape — it cleanly separates the failure path into its own goal where steps can do whatever they need (write, log, queue, dispatch). Inline `on error` with steps underneath is not how PLang error handlers compose.

## `- run %callback%`

Consumes a callback. Verifies signature (via `signing.verify`), confirms `goal_hash` against current build (mismatch = signed-but-stale → hard error), constructs an App with the callback as entry point, runs.

```plang
Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

For the future ask-user case, `- run %callback%` will also decrypt encrypted variable values internally via `Callback.DecryptInPlace(ctx)` — see [encryption-layering.md](encryption-layering.md).

## Ask-user `vars:` annotation

Step-level on issuing actions only:

```plang
- ask user 'What is your name?', vars: %orderId%, write to %name%
```

`vars:` is **not** a Step-level concept (rejected in design — would imply error-retry needs declared vars too, which it doesn't). It belongs to ask-family actions specifically.

The post-resume contract for ask-user is **lossy**: the developer reloads `%order%` from `%orderId%` on resume; only the names listed in `vars:` carry through the wire envelope.
