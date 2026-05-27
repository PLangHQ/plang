# Stage 3: `As<T>` as Tree-Walker

> **Note for coder:** every code snippet, type signature, method name, and file path in this file is a **suggestion** that captures architect intent — not a contract. You own the implementation. Reshape, rename, restructure, or replace anything below as the real constraints demand. Push back on the design itself if you find it wrong.

**Goal:** Rewrite `Data.As<T>()` so it reconstructs T by walking the normalized tree (the output shape from Stage 2's `Normalize`), instead of delegating to `JsonSerializer.Deserialize<T>` or other reflection-via-STJ paths. Per-type round-trip hooks let domain types like `path` reconstitute themselves cleanly. Property-lookup cache mirrors the one Normalize uses on the way out.

**Scope:**
- `Data.As<T>()` (PLang/app/data/this.cs — find the existing method, it's the entry point everything funnels through).
- Property-lookup cache keyed by `Type` (probably `ConcurrentDictionary<Type, …>`).
- Per-type reconstruction hooks for types that can't be naively populated from a property bag — currently only `path` (via `path.Resolve(Relative, context)`), but the mechanism should generalize.
- Path's `JsonConverter.Read` migrated to the new pathway (or the converter deleted entirely if its Read side is no longer wired anywhere after Stage 2 — depends on what survived).

**Dependencies:** Stage 2 (`Normalize` + `IWriter` + `JsonWriter` exist; path on the wire is now `{ Scheme, Relative }`).

**Out of scope:**
- A second non-reflection format adapter (deferred — `IWriter` accepts one in shape, but no protobuf/MsgPack on this branch).
- `Normalize` itself (Stage 2 — this stage is the *reverse* direction).
- Any new domain type (`As<T>` works for the existing inventory in `plan/wire-out-attributes.md`).

**Deliverables:**

1. **`As<T>()` walks the tree, doesn't reflect-via-STJ.** Cases:
   - T is a primitive — return the unboxed value from Data.Value (with safe conversion).
   - T is `List<X>` — walk Data.Value as `List<Data>` or `List<X>`, build the result.
   - T is `Dictionary<string, X>` — walk `List<Data>`, name → key, value → `As<X>`.
   - T is a `Data` — return as-is or unwrap (existing semantics).
   - T is a record / class with `[Out]` properties — instantiate (parameterless ctor or record positional ctor), populate each `[Out]` property from the named child in the normalized tree.
   - T implements a per-type reconstruction hook (see below) — delegate to that.
2. **Per-type reconstruction hook.** Some types can't be populated by setting public properties — `path` is the canonical case (it's abstract, has no parameterless ctor, needs the Context to wire FileSystem etc.). The hook is "if T has a `static T FromData(Data, Context)` or `static T Resolve(string, Context)`-shaped method, call it instead of the generic property-bag reconstruction." Coder's call on the exact signature / discovery mechanism (marker interface? attribute? naming convention?). For `path` specifically, the existing `path.Resolve(string, Context)` is the obvious target — the As<path> hook reads the `Relative` field from the normalized tree and calls Resolve.
3. **Property-lookup cache.** Same pattern as Stage 2's Normalize cache. `ConcurrentDictionary<Type, PropertyInfo[]>` (or similar) — populated lazily, never invalidated. Reflection fires once per T per process.
4. **`path.JsonConverter.Read` migrated or deleted.** After Stage 2 removed `Write`, the converter is half-gutted; this stage decides what happens to `Read`. Either: (a) delete the converter entirely and route inbound paths through `As<path>` like every other type; or (b) keep `Read` as a thin call into the new As<path> hook if some inbound JSON code path doesn't go through Data deserialization. (a) is cleaner if it works.

## Design

The walker is recursive — each child Data is `As<ChildType>`-walked depending on the parent property's declared type. This is where the property-lookup cache earns its keep: walking a `Data { value: List<Data> }` into a `User { Name; Email; Roles: List<Role> }` requires knowing User's property table once, then dispatching per child.

**Suggested core shape** (coder owns the actual implementation):

```csharp
public T? As<T>(Context? ctx = null) {
    var normalized = Normalize();  // idempotent — Stage 2's Normalize is safe to re-call
    return (T?)Reconstruct(normalized, typeof(T), ctx);
}

private static object? Reconstruct(object? value, Type targetType, Context? ctx) {
    if (value is null) return null;
    if (targetType.IsPrimitive || targetType == typeof(string) || …) return Convert(value, targetType);
    if (HasReconstructionHook(targetType, out var hook)) return hook(value, ctx);
    // Default: property-bag reconstruction
    var instance = Activator.CreateInstance(targetType);
    foreach (var prop in OutPropertiesOf(targetType)) {
        var child = FindNamedChild(value, prop.Name);
        if (child != null) prop.SetValue(instance, Reconstruct(child, prop.PropertyType, ctx));
    }
    return instance;
}
```

**On `path.Resolve`:** today `path.Resolve` takes a raw string and produces a scheme-correct subclass. The As<path> hook is essentially "read Relative from the normalized tree, hand it to `path.Resolve(relative, ctx)`". This means the hook needs a Context — the existing `path.JsonConverter` already grappled with this (it carries the Context as ctor argument). `As<T>(ctx)` accepting an explicit Context is the obvious answer.

**On record types with positional constructors** (Identity might be one, depending on its current shape): the generic property-bag reconstruction needs to know whether to call a parameterless ctor + property setters, or to gather values and call the positional ctor. Record reflection has `EqualityContract` and primary-ctor metadata you can lean on. Coder's call on how to dispatch.

**On the inverse of `[Out]` filtering:** when reconstructing, you only get back the properties that were marked `[Out]` on the way out. Derived properties (path.Extension etc.) recompute on first access via their existing lazy getters. Local properties (path.Source, path.Content) just stay null — that's correct, they're local state that shouldn't survive a round trip.

**On debug-mode round-trip:** if Stage 2 landed the debug bypass, debug-mode walks emit *all* properties — including derived ones. Reconstructing from a debug-mode tree should still work, but derived properties may now exist on the wire and conflict with the lazy getters. Resolution: trust the wire if it's present, fall back to lazy compute if not. Coder's call on whether this is worth supporting or whether debug-mode is one-way (serialize-only, never deserialize).
