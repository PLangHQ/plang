# Plan: `context.Type.Create` — remove the static `FromName`, born type-entities with context

## Decision (Ingi)
- Mint type-entities through **`context.Type.Create(name, kind?, strict?, template?)`**, not a
  static factory. The context you're in is the context the type gets.
- NOT `app.Type.Create` — that would have to pick an ambient/"current" context (an anti-pattern
  Ingi wants gone).

## The enabling insight
A type-entity's `Context` is **App-identity**, not actor-scope: the only use is the schema fold
`Context.App.Type.ComplexSchemas()` (App-keyed, actor-independent). So "which context" never
matters beyond "which App" — `context.Type.Create` stamping the calling context is always correct.

## What ships
1. **`context.Type` accessor** → a context-bound factory (readonly struct holding the context)
   with `Create(name, kind = null, strict = false, template = null)` → borns
   `type.@this` WITH the context. Delegates to the existing `type.@this.Create(..., context)`.
2. **Replace `FromName(name)`** (static, context-less) at every context-bearing site with
   `context.Type.Create(name)`. Sites (all hold `Context`):
   - module list actions: add/remove/set/where/any/sort/reverse/range/group/unique/split/flatten
   - variable/set.cs:200, variable/list/this.cs:91 (`context` in the ctor)
3. **Replace `new type(name, kind)` in value `Mint()`** with `Context.Type.Create(name, kind)`
   — the carriers now hold `Context` (clr/file/url/image/kind already read it in Mint).
4. **Thread context into `Format.TypeFromMime/TypeFromExtension`** (instance methods on
   `format.list`, currently context-less; 13 callers, all but 2 hold `Context`; the 2 —
   getTypes:181, path/this.cs:235 — can reach one). They then `context.Type.Create(...)`.
5. **The STJ wire read** (`type/serializer/Reader.cs:20`) currently
   `Deserialize<type.@this>()` then `entity.Context = ctx.Context` (construct-then-stamp).
   The reader HAS `ctx.Context` → parse {name,kind,strict,template} and `ctx.Context.Type.Create(...)`.
6. Delete the static `FromName`.

## The one context-less mint that stays: `@null.Mint`
`@null` is a **static singleton** (`Instance = new()`), no `IContext`, no context to give. Its
`Mint()` mints an identity type for a typed-null slot (empty `path` slot answers `path`). This is
pure identity — it never hits the schema fold — so a context-less type here is correct.

**Therefore `type.@this.Context` stays NULLABLE.** This flip is NOT "type becomes non-null" — it's
"remove the static `FromName`, born every *runtime* type-entity with context via
`context.Type.Create`, and keep the fold's self-diagnosing throw as the guard for the rare
identity-only mint (`@null`) that legitimately has none."

## Net
- Static `FromName` gone (Ingi's actual concern).
- Every runtime type mint born-with-context.
- `type` is NOT one of the "10 non-null value types" — it's the type-*entity*, and its identity
  mode is legitimately context-free (guarded by the existing fold throw). Documented, not forced.

## Open question for Ingi
This removes the static and maximizes born-with-context, but `type.@this.Context` stays nullable
(because of `@null`'s identity mint). Is that the intended end state, or do you want `@null` itself
reworked so `type` can be strictly non-null too? (That's a separate, larger thread — `@null` is a
shared singleton.)
