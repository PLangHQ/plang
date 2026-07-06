# Stage 3c — C# actions dispatch through the seam (`app.RunAction`)

Settled with Ingi through the 3c design conversation. This makes settings + `[Default]` actually
apply when an action is invoked from C# (module-to-module), which today they do not.

## The problem
The setting seam is the generated `ICodeGenerated.Resolve(action, context)` — for each param:
**step value → setting → `[Default]`**. It only runs on the `.pr` path
(`entity.RunAsync → Push → Resolve → Execute → Run`). C# callers use:
```csharp
await app.RunAction(new request(context){ Url = …, TimeoutInSec = … }, context);
```
which sets `PreboundHandler` and **skips `Resolve`** (`action/this.cs:300-338` —
"params already set by inline C# composition, so we skip Resolve"). So **settings + `[Default]`
never apply to C#-invoked actions.** Pervasive: OpenAi (`OpenAi.cs:227`), builder (list/read/save),
http (signing.verify), signing (identity/hash), path (ask), schema (verify) — and the 4
`RequestActionTests` (BaseUrl/DefaultHeaders/MaxResponseSize, pre-existing red).

## Why not just call the action's `Run()`
`Run()` is the author's logic (`request.cs:85 => Http.SendAsync(this)`), which assumes params are
**already resolved**. A C# `new request(context, url:…)` has params *set but not resolved* — the
seam hasn't applied settings/`[Default]`/`%var%`. `Resolve` is **async** (setting reads + `.Value()`),
so it can't run in the ctor. And the class is `partial` (one class, no `base`), so a generated
`Run()` would collide with the author's `Run()`. C# has no native "before Run" interception.
→ The invocation is inherently a **separate, generated** step; keep it off the instance.

## The settled shape
```csharp
await app.RunAction(new request(context,
    url:      endpoint,          // required
    method:   HttpMethod.POST,   // optional (has [Default]) — omit → seam → setting → [Default(GET)]
    unsigned: true));
//  context appears ONCE (the ctor — where born-with-context needs it)
//  RunAction reads handler.Context and runs the seam; omitted params fill from setting/[Default]
```
- `app.RunAction` — existing, non-static, **one uniform verb for every action** (a beginner learns
  one thing: `app.RunAction(new SomeAction(context, …))`).
- `Run()` stays the **honest, visible logic** — the instance never pretends to be the entry.
- (Naming: `RunAction` is Verb+Noun — flagged for a later pass, orthogonal to the mechanism.)

## Mechanism 1 — generated raw-args ctor (per action)
The primary ctor is already generated (`partial class request(context __ctx)`). Emit a **second
ctor overload** taking params as raw, typed, named optional args, wrapping each with `__ctx`
(born-with-context). The generator reads three signals per param:

| Param signal | Ctor arg | Omitted → |
|---|---|---|
| non-nullable `Data<T>`, no `[Default]` (`Url`) | **required** — `string url` (no `= null`) | can't — compiler forces it |
| has `[Default]` (`Method`) | **optional** — `HttpMethod? method = null` | seam → setting → `[Default]` |
| nullable `Data<T>?` (`BaseUrl`) | **optional** — `string? baseUrl = null` | seam → setting, else absent |

- **Requiredness lives in the ctor (nullability); defaults live in the seam.** Never bake a
  `[Default]` value into the signature (`method = GET`) — that injects it as the step value and a
  `http.method` setting could never override it.
- plang type → raw CLR type for the signature: `text→string`, `number→long`, `@bool→bool`,
  `choice<E>→E` (enum works — no implicit needed), `dict→Dictionary<string,object?>`, untyped→`object`.
- Body: `if (url is not null) Url = new data.@this<text>("", url, context: __ctx);` per param.

This is why we do **not** add implicit `=` operators to `Data<T>` — the generated ctor makes raw
values work directly (and covers enums, which implicits can't). (Implicit-on-`Data<T>` is also
blocked anyway: `Data`'s value is built at construction *with context* — `this.cs:190-210` — so a
context-less `static` operator can't build a scalar from a raw string.)

## Mechanism 2 — fix `app.RunAction`
- **Run the seam, not the skip:** extract the handler's *set* params (`if X.IsInitialized`) → a
  param bag → build the entity with `Parameters = bag` (NO `PreboundHandler`) → `RunAsync`
  (Push → **Resolve** → Execute → Run). Omitted params fill from setting/`[Default]`.
- **Drop the `context` arg** — read `handler.Context`.

## Removed
- `PreboundHandler` (the property + the skip-`Resolve` branch, `action/this.cs:300-338`).
- `RunAction`'s `context` parameter (now `handler.Context`).
- `new data.@this<T>("", …)` wrapping at every C# call site (via the raw-args ctor).

## Migrate + verify
OpenAi, builder, http, signing, path, schema off `new X(ctx){…} + RunAction(handler, ctx)` →
`app.RunAction(new X(ctx, named args))`. The 4 `RequestActionTests` go green (they can now feed
BaseUrl etc. and the seam resolves).
