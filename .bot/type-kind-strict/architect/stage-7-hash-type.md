# Stage 7 (rev 2): The `hash` type — module-owned, returned as a value

> **Coder: you own the final shape.** Settled: `hash` is a **crypto-owned** type (lives under the module, not `app/type/`), and `crypto.hash` **returns a `hash.@this` value** (not `byte[]`). That single return-type change is the spine of this rev — it drives the build-time annotation Ingi wants *and* fixes the runtime defects rev 1 shipped. Method names and the exact read-back seam are yours.

**Goal:** A `hash` is a digest that knows its algorithm. `crypto.hash` returns a `hash` value so (1) the **builder annotates the write-to variable as `%x% (hash)`** for later steps, and (2) `crypto.verify` reads the algorithm off the value instead of a loose parameter.
**Scope:** Included — relocate the `hash` type to `app/module/crypto/type/`; change `crypto.hash` to return `Data<hash.@this>`; the wire read-back (string+kind → `hash.@this`); a real verify round-trip test. Excluded — `encrypt`/`decrypt`; the extension-derived producers (stage 6).
**Dependencies:** Stages 1–2. Independent of stage 6.

## Why — build-time type flow is the point

The payoff isn't runtime verification, it's the **builder**. When a developer writes:

```
- hash %ble%, write to %bla%
- verify %bla% against ...
```

the LLM compiling the *second* step is shown "Variables in scope (from prior steps)" — `CompileUser.llm` renders `` `%bla%` (<type>) `` from `%stepVarTypes%`. We want it to read `%bla% (hash)`.

I traced the chain end to end:

`crypto.hash.Run()` return type → `goal.getTypes.DetermineReturnType()` reflects `Run()` (`Task<Data<T>>` → `T` → `GetTypeNameStatic(T)`) → `chainReturnType` → the paired `variable.set Value=%!data%` records `working["bla"]` → `varTypes[stepIndex]` → `%stepVarTypes%` (`BuildStep/Start.goal:19-20`) → `CompileUser.llm:17`.

Today `crypto.hash` returns `Data<byte[]>`, so `DetermineReturnType` yields `"bytes"` and the builder shows `%bla% (bytes)`. To get `%bla% (hash)`, the return type must be `hash`.

## The one change everything hangs on

**`crypto.hash` returns `Data<hash.@this>`** — value `new hash.@this(bytes, algorithm)`, type stays `Create("hash", kind: algorithm)`. This single change delivers four things at once; rev 1 returned `byte[]` and got none of them:

1. **Build annotation** — `DetermineReturnType` → `"hash"` → `%bla% (hash)` in the next step's compile prompt. *(Ingi's ask.)*
2. **Live serializer** — Normalize dispatches by the *value's* CLR type (`this.Normalize.cs:158`). A `byte[]` resolves to `bytes`, so the `hash` serializer rev 1 wrote can never fire. A `hash.@this` value resolves to `hash` → the serializer renders base64.
3. **Verify works in the real flow** — `verify %data% against %h%` binds `%h%` (now a `hash.@this`) into `Hash` (`data.@this<string>`); `As<string>` hits the implicit `operator string` → base64 → `FromBase64` succeeds. With `byte[]`, `As<string>` has no base64 path → `ToString()` → `"System.Byte[]"` → `FromBase64` throws. *(Rev 1's verify test hid this by manually base64-encoding — see below.)*
4. **ClrType matches the value** — `hash` resolves to `hash.@this`; the carried value is `hash.@this`. No name/value mismatch.

### Wire read-back

Once the value is `hash.@this`, the wire form is a base64 string with `type:{hash, kind}`. Reconstruct on read via the type's own `Convert` (it has the entity, so it has `Kind`): `hash.FromBase64(raw, this.Kind)`. Don't route read-back through a `Resolve(string, ctx)` that can't see the algorithm — the kind is on the type, and `Convert` is where the type can read it.

## Relocation — `hash` is crypto-owned

Move `app/type/hash/` → `app/module/crypto/type/hash/` (mirrors `http/type/`, `settings/type/`, etc. — eight modules already do this; `app/type/` is reserved for the **builtin** vocabulary). The name still resolves to `hash` (the `@this`/class-name convention is location-independent).

**Confirm discovery survives the move:** `hash` is a *result* type — the registry should know it because it appears in `crypto.hash`'s return signature (the catalog walk over module signatures, the same way `http.response` is known), not because it sits in `app/type/`. The `HashType_Resolves_ViaRegistry` test (`app.Type["hash"].ClrType == typeof(hash.@this)`) must still pass after the move. If signature-discovery alone doesn't register it, that's the real work of this stage — wire the module-type into the registry walk; do **not** solve it by leaving `hash` in `app/type/`.

## The `hash` type shape — keep rev 1's, it was right

The type itself (rev 1) is good and stays, just relocated: `byte[] Bytes` + `string Algorithm` (the algorithm *is* the kind), `ToBase64`/`FromBase64`/`DigestEquals`, implicit `string`, `Shape="string"`, advertised `Kinds = [keccak256, sha256]`. The verify *split* is correct — the type owns encoding + byte-equality, `crypto.Default` owns the recompute (the algorithm switch). Don't change that.

## Fix the verify test — make it a real round-trip

Rev 1's `CryptoVerify_DefaultsAlgorithmFromHashKind` manually does `Convert.ToBase64String((byte[])digest.Value!)` and manually stamps `{hash, sha256}` — so it never exercises the produced-variable path and passes while the real flow is broken. Replace it with a round-trip that binds the **actual produced hash** as the verify input (goal-level `hash … write to %h%` then `verify … against %h%` → true, or at least bind the `crypto.hash` result Data directly as `Hash`). No manual base64, no manual type stamp.

## Keep `hash` out of the LLM emit kind-table (Ingi, 2026-05-31)

Two tables, and rev 1 conflated them:

- **C# type/kinds registry** (`Schema.Build().Kinds`, `app.Type[...]`) — **keeps `hash`.** This is how C# resolves the type when validating `verify %bla%` and how `getTypes` maps `%bla% → hash`. The `HashType_AdvertisesAlgorithmKinds` / `HashType_Resolves_ViaRegistry` tests assert this side; it stays.
- **LLM emit kind-vocab table** (`CompileUser.llm`, the "kinds you may emit under each name" table) — **must not list `hash`.** The LLM never writes `as hash`; the algorithm is the `crypto.hash` `Algorithm` parameter, not an emitted type-kind. Listing it is pure noise in the prompt.

Rev 1 built the emit table by walking **all** registered types' `Kinds` (`BuildTypeEntries(null)`), which is why `hash` leaked in. Fix: scope the **emit-table render** to the **builtin emit vocabulary** (the `app/type/` types + primitives) instead of every registered type. Combined with the relocation above, `hash` (now under `app/module/crypto/type/`) falls out of the emit table automatically while staying in the C# registry.

**The `image` trap:** the filter is *"is in the builtin emit vocabulary,"* NOT *"is a module/result type."* `image` is both emittable (`as image`, kinds `png`/`jpg`) and produced — it must stay in the emit table. A naive "exclude module-owned types" filter would wrongly drop `image`. Key off the builtin emit set (or an explicit per-type "emittable" signal), not off folder location.

The builder still sees `%bla% (hash)` on the next step — that comes from the variables-in-scope path (`getTypes → %stepVarTypes%`), name-only, independent of the emit table. Name is enough; the builder does not need the kind at build.
