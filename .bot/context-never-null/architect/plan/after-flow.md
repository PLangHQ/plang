# The code flow after Context is non-null

One rule runs through all of it: **context is born at app start, owned by each actor, forwarded into every call — never null.** Every place that used to ask "do I have a context?" stops asking. The branches disappear; the path goes straight.

Code shapes below are illustrative — they show the *shape* of the after-state, not the final spelling. The coder owns names and exact seams.

## 1. App start → actors → contexts (the births)

Both actors are constructed eagerly; each owns a context; the context knows its actor from the first line.

```
// app.@this start
_system = new actor.@this("system", this);   // was: lazy _system, null until first access
_user   = new actor.@this("user", this);

// actor.@this ctor
public @this(string name, app.@this app, ...) {
    ...
    Context = new context.@this(app, owner: this, ...);   // owner passed in
    // (gone: the separate `Context.Actor = this` line a step later)
}

// context.@this ctor
public @this(app.@this app, actor.@this owner, ...) {
    App = app;
    Actor = owner;          // non-null from construction
    ...
}
```

`Context.Actor`, `App._system`, `App._user`, `Variables._context` are all non-null. `GetActor("x")` returns a non-null `actor.@this` or **throws** — there is no null actor to hand back.

## 2. A value is created during execution

A handler produces a result. It is born from the context already in hand, not constructed then stamped.

```
// before — construct, then someone stamps later (or forgets, and Data.Context returns null via the `!`)
var d = new data.@this("", new text("ok"));
...                                  // d.Context is null until something sets it

// after — born from the context
return context.Ok(new text("ok"));   // value carries context.Actor / context from birth
```

`Data.Context` is now a plain non-null property:

```
// before
public actor.context.@this Context { get => _context ?? (_type as IContext)?.Context!; set { _context = value; if (value != null && ...) ... } }

// after
public actor.context.@this Context { get => _context; set { _context = value; if (_type is IContext c) c.Context = value; } }
```

A raw `path` outside any Data wrapper still answers `path.Context` non-null, so `path.Authorize(verb)` works without a wrapper. Sentinels come from the context too: `Data.Null()` → `context.Null()`.

## 3. Write path — value → wire → store

A value is serialized through `application/plang`. Signing fires inside the write; the store binds the serializer with its context.

```
// settings/Sqlite.cs
_serializer = new(context);          // was: new()  (context-less)
...
store.Set(table, key, data);         // data.Context is non-null; serializer.Context is non-null

// channel.serializer.plang Wire.Write
//   - renders the value via data.Normalize(View)
//   - sign-if-missing: hash the BODY (body-only write, no recursion), sign, wrap in a signature layer
```

The hash step canonicalizes the **body only** — it does not re-enter sign-if-missing, so signing does not recurse. The crypto module holds the context it canonicalizes through.

## 4. Read path — store → wire → typed value

This is where the two-narrow fork collapses to one. `_context` is non-null, so `Wire.ReadBody` always routes a typed value through the Typed reader. The `_context != null` gates are gone.

```
// before — three gated branches; null context falls to a typed-entry-blind parse
if (... && _context != null && _context.App.Type.Readers.Typed(name, kind) is {} typed) { born = typed.Read(...); }
else { value = item.serializer.json.Parse(el); }   // raw {type,value} dict on the context-less path

// after — one path; the Typed reader always runs for a declared type
if (_context.App.Type.Readers.Typed(typeRef) is {} typed) { born = typed.Read(...); }   // pass typeRef whole, not .Name + .Kind
```

A nested `{type:{name},value}` entry is now always born to its type, whether it came from a `.pr` load or a settings read. The cache double-wrap that blocked `plang build` does not occur, because there is no second narrow to leave it raw.

## 5. The signature layer — verify with authenticity

`Wire.ReadSignatureLayer` no longer has a "no context → trust" branch. Every signature layer verifies. `View` decides only whether freshness is checked.

```
// before
if (_context == null) {
    if (View != Store) return FromError("can't verify without actor");   // gone
    return layer.Value;                                                  // gone — at-rest trust
}
... run verify, skip freshness when Store ...

// after
layer.Value.Context = _context;
var verify = new signing.verify {
    Data = carrier,
    SkipFreshnessCheck = (View == Store),     // Store skips freshness only
};
// no decomposed identity passed in. verify is IContext, so it navigates its OWN
// context for the authenticity check:
//   assert layer.Identity == action.Context.Actor.Identity   (+ the existing signature/hash checks)
```

The authenticity check lives **inside** verify, which navigates `action.Context.Actor.Identity` itself — nothing decomposes the actor at the call site (OBP Rule #2: don't pass `actor.Identity`, let the callee navigate to it).

`actor.Identity` is the actor's identity — the `identity` keypair object whose identity-*value* is its public key (the type renders to its `PublicKey`, and PLang exposes it as `%Identity%`). The signature's `layer.Identity` is already the public-key text. So verify asserts `layer.Identity == action.Context.Actor.Identity`, reducing to the public-key string on both sides. The actor keeps the whole keypair (it needs the private key to sign); only its public face is the identity. The bootstrap root read is the exception — the actor's identity isn't loaded yet, so it carries `verify.Root = true` (request state, a parameter — Rule #6): run the normal checks but skip the actor-identity match. The keypair self-consistency check (`PublicKey` re-derives from `PrivateKey`) lives in the identity provider's load, not in verify — verify never learns what a keypair is.

## 6. Bootstrap — the one read that authenticates differently

Loading the system keypair can't match against `App.System.Identity` — that read is *how* the key gets into memory. So it sets `verify.Root = true`: verify runs its normal checks (the signature check validates the self-signature) but skips the actor-identity match. The identity provider then checks the loaded keypair is self-consistent (`PublicKey` re-derives from `PrivateKey`) — that check is the provider's, not verify's. Possession authenticates the root. After it, `App.System.Identity` is in memory and steps 3–5 above use it. Bootstrap loads the system identity before any other `application/plang` read.

## 7. Step.Disabled — context passed, not stashed

The shared Step no longer carries a context. The disabled state is reached by passing the running context.

```
// before
step.Context = Context;          // stash on shared step
if (step.Disabled) ...           // reads it back off the bag
// + AnchorScope saves/restores step.Context each dispatch

// after
if (step.Disabled(context)) ...      // query: drop the Is-prefix; step navigates the passed context
// mutate via real-work verbs: step.Disable(context) / step.Enable(context)
// AnchorScope keeps context.Step (for %!step%) but the Step.Context dance is gone
```

## The shape of the whole thing

Context enters once, at app start, and rides every value and every call from there. Reads stop guessing because they always hold the type-reader registry (via context) and the actor (for verify). Writes always sign. The store binds the MIME, the MIME decides verify, the actor proves authenticity. Nothing in the path asks "what if there's no context" — because there never isn't one.
