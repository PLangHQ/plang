# Runtime Flow — Ask Marker, error.handle, Variable-as-Carrier

## What this covers

The end-to-end shape of how a permission request flows through the runtime: how an action signals that it needs user consent, how the engine recognizes that signal, how the user-facing prompt happens, how the signed grant gets stored, how the action retries. Also covers where the granted permission lives between calls — on the variable's `Properties`, lazy-resolved from the store.

This doc is shared infrastructure for *any* permission kind. Filesystem is the first instantiation; HTTP and Payment use the same machinery on follow-up branches. It also covers free-text user input (`ask user "..."`) — both share the same Ask mechanism.

## Asks are a kind of error

When an action can't complete because the user has to answer something — either a permission question or a plain text prompt — the action returns `Data.Fail` whose Error implements the `Ask` marker. There is no new terminal state on Data; Ok and Fail are still the two outcomes.

The semantic reading is honest:

- "You wanted to read the file, but it failed because the user needs to consent to that read."
- "You wanted to ask the user a question, but it failed because the user hasn't answered yet."

Each is a real failure of *this* call. The reason is what tells the engine "you may be able to fix this by asking" rather than "this is a hard error."

### Marker shape

```text
Ask                          ← marker interface (or base record)
 ├─ PermissionAsk            ← carries a Permission record (or list of them) describing what consent is needed
 │   ├─ FilePermissionAsk    ← FS-shaped permission ask
 │   └─ ... (HttpPermissionAsk, PaymentPermissionAsk on follow-up branches)
 └─ UserInputAsk             ← carries a prompt for free-text input from output.ask
```

(Final naming/structure decided by coder during stage 2 implementation.)

## Engine routing — inside `error.handle`

PLang already has `error.handle` as the error-handling mechanism (both built-in behaviour and the per-step `on error, call ...` modifier). Ask handling is *part of* that mechanism, not a parallel router. The error path stays one path.

```
Action returns Data.Fail(error)
  ↓
Engine: error.handle runs
  ↓
  Built-in: error implements Ask?
    │
    ├── NO  → fall through to user-configured handler (on error, call ...) if any,
    │         else propagate normally up the goal hierarchy.
    │
    └── YES → built-in consent / input flow:
              ↓
              Pick template per concrete Ask type
                 (e.g. FilePermissionAsk → os/system/permission/file.template)
              ↓
              Render, write to actor's output channel
              ↓
              Wait for user response (y / n / a, or free text)
              ↓
              For permission asks with "yes": sign the Data<Permission>, store it
                 - 'y' → no expiry on signature → in-memory store
                 - 'a' → long expiry on signature → sqlite persistence
              For free-text asks: capture the answer
              ↓
              Outcome:
                 - handled     = consent granted / answer captured
                 - not handled = denial / failure to ask
                                 → fall through to user-configured error.handle
                                   (or propagate if none)
  ↓
Engine (back in dispatch loop) branches on outcome:
   - handled         → re-run the same action with the same params
   - not handled     → leave Data.Fail to whatever error.handle resolved
                       (user's handler ran, or it propagates)
  ↓
Re-run path: action runs fresh. Store now contains the new grants
            (or the answer is now available).
            Action completes (returns Ok or Fail as normal).
```

Four things to notice:

- **No new Data state.** Ok and Fail are still the only terminal outcomes. Ask is a *kind* of Fail; error.handle recognises it and runs different default behaviour.
- **No parallel router.** All errors go through error.handle. Ask handling is one branch inside it; non-Ask errors take the other.
- **Action handlers are oblivious.** They emit `Data.Fail` with an Ask-marked error; error.handle does everything else.
- **User can override.** If a step has `on error, call MyHandler`, that runs *after* the built-in consent flow (only on the "not handled" path) — or, if the user really wants, they could write a handler that fires even for Ask-marked errors. The built-in is the default; user code can layer on top.

## Templates per kind

Under `os/system/permission/`:

```
os/system/permission/
  file.template      ← rendered by router when error is FilePermissionAsk
  http.template      ← future
  payment.template   ← future
```

Templates are PLang-native (`.goal` or `.template` files) that take the Ask's payload as input and produce consent text. The PLang developer or system administrator can customize them per app or globally.

For multi-permission asks (batched move, copy): the template renders all needed permissions together as one consent prompt. Single consent covers all. If finer-grained control is needed, the calling code splits operations.

Free-text `UserInputAsk` doesn't need a per-kind template under permission/ — the existing `output.ask` infrastructure already knows how to render those.

## Two lifetime modes — single field encodes both

For permission asks specifically: the user's choice maps to the signed grant's expiry field.

| Choice | Signature expiry | Storage location |
|--------|------------------|------------------|
| y (this session) | null (no expiry set) | in-memory list on `Actor.@this.Permission` (per actor) |
| a (always) | long expiry (e.g. 1 year) | `App.SettingsStore` — persisted in the `permission` table |
| n (deny) | — | none. Router reports not-handled; engine propagates original `Data.Fail`. |

The runtime can tell which store to read from by the signature shape — a signed grant with no expiry was a "y" choice. Two stores, one shape, one source of truth per grant.

## Variable-as-carrier — the Path owns the check

Once granted, the permission is stored. But the *check* happens through a specific `%path%` variable. The Path is the owner of its own permission question — the FS layer asks Path, not the store.

`path.CheckPermission(Verb.X)` is a method on Path that returns Data:
- `Data.Ok` if a matching signed grant exists for this Path + Verb.
- `Data.Fail(FilePermissionAsk(...))` if no matching grant.

Inside `CheckPermission`, Path consults its own Properties cache first (`Properties["permission"]`). If absent or invalid, Path walks via its Context to the calling actor's `Permission` view (`path.Context.Actor.Permission`) and queries for a matching grant. The actor's `Permission` is the unified view over in-memory ("y") and persisted ("a") grants for this actor. On hit, Path caches the grant in Properties for subsequent checks.

The FS layer doesn't see `actor.Permission` directly. It asks the Path and propagates whatever Path says.

Why on Path: Path owns everything about itself — its absolute form, its raw form, the calling Goal context, and now its permission status. PLang developers can read `%path.permission%` like any other property; the lookup happens transparently.

Why lazy: most paths never need permission (in-root paths). Eager lookup at variable construction would touch the store on every `set %path% = "foo"`. Path's `CheckPermission` only touches the store when called.

Why store is source of truth: the store is durable, signed, the place where grants actually live. The Properties cache on Path is convenience; it can be invalidated by mutation. Revocation works by removing from the store — the next `CheckPermission` call finds nothing and emits a fresh Ask.

## Re-query retry (no special machinery)

When the engine re-runs the action after the ask router reports handled, the action runs **from scratch**. No carrier object, no continuation token, no state passed between invocations. The engine just calls `action.Run()` again with the original params.

```
1st invocation: fs.ReadText(path) → check → no grant → Data.Fail(FilePermissionAsk(...))
                  → router intercepts → user grants → router stores → handled
2nd invocation: fs.ReadText(path) → check → finds grant in store → Data.Ok(content)
```

The two invocations differ only in store state. Action code is identical on both — it's not aware that it's being retried.

This means: an action that emits an Ask-marked Fail must be **idempotent up to the point of emission**. Any side effect before the permission check would happen twice on retry. The FS operations are fine because they check permission first, before doing anything. Future actions need to follow the same discipline — check permission before any I/O.

If the second `Run` *also* returns an Ask-marked Fail, the engine doesn't loop forever. One re-run attempt; if still asking, propagate as a normal Fail. (Shouldn't happen if the router did its job, but failing safely is right.)

## Multi-path bundling (filesystem-specific shape)

`file.move` and `file.copy` need permissions on two paths with two different verbs. Both Paths are queried; if either fails, the FS method bundles the two FilePermission Asks into one `Data.Fail`. User sees one consent prompt covering source-read and dest-write together. Fail-fast: if either is missing, no partial work happens.

The Path-side method handles single-verb queries. The FS method's only job in the multi-path case is to ask each Path, collect missing Asks, and merge them into a bundled response — that's a small piece of orchestration that genuinely belongs at the FS layer (it knows about *the operation*, which is what binds the two Paths together). If even that orchestration grows, it points to a missing type like `MoveOperation` that owns the pair.

## Three permission scopes — local vs service

The runtime distinguishes:

- **User / system permissions (local)** — checked locally, stored locally, never sent on the wire. FilePermission is always local. HttpPermission (when added) is local. The check happens inside the local layer that knows about the operation.
- **Service permissions (remote)** — needed by a remote PLang server to authorize an operation on its end. Carried alongside the outbound Data when calling a service. PaymentPermission is the canonical example.

This branch implements local only. Service-permission flow (how a signed Data<Permission> travels along with an outbound request to a PLang server, how the server returns Ask-marked failures, etc.) is the http and payment follow-up branches' concern.

## What this means for action handlers and the FS layer

After this branch lands:

- **Action handlers** call `fs.ReadText(path)` (or save/delete/copy/etc.), get back a Data, return it. Never inspect Ask, never branch on permission.
- **FS methods** ask `path.CheckPermission(Verb.X)`, propagate Fail-with-Ask if Path returns one, otherwise do the BCL IO and wrap the result in Data.
- **Path** owns its permission status. Internally consults Properties cache and `path.Context.Actor.Permission`; emits the Ask if no grant matches.

Three delegations, each to whoever owns the concern. No transaction script, no helper soup.

Future kinds follow the same shape: the URL variable owns http permission (`url.CheckPermission(...)`), the payment context owns payment permission, etc. The pattern repeats — domain object owns its own questions.
