# Keep-alive: app-lifetime disposables

`App.KeepAlive` holds objects that must live as long as the app. An object
added here is not disposed by the code that created it. It is disposed when
`App.DisposeAsync` runs. This page is for the C# developer who needs an object
to outlive its creator.

```
app.keepalive.@this    PLang/app/keepalive/this.cs    namespace app.keepalive
App.KeepAlive          PLang/app/this.cs              property, type keepalive.@this
```

> **Scope.** This is the C# page. Nothing in the `.cs` tree outside `App` refers
> to `KeepAlive`, so the collection has no plang action or type in this read.

---

## 1. What it is

A `sealed` class that implements `IAsyncDisposable` and holds a private
`List<object>`. `App` creates one instance in its property initializer, so
each `App` owns its own collection. It has no `list/` folder and no `.current`.
It is a single owner, not a registry.

---

## 2. The surface

| Member | Behaviour |
|---|---|
| `void Add(object instance)` | Appends the instance. No duplicate check. |
| `void Remove(object instance)` | Removes the first matching entry. If it was found, disposes it: an `IAsyncDisposable` is awaited through `GetAwaiter().GetResult()`, otherwise `IDisposable.Dispose()` is called. If it was not found, returns without disposing. |
| `ValueTask DisposeAsync()` | Idempotent through a `_disposed` flag. Walks the items in insertion order, awaiting `IAsyncDisposable` or calling `IDisposable.Dispose()`, then clears the list. |

`Remove` is synchronous because its callers are non-async paths. Its async
disposal runs on the calling thread.

---

## 3. Teardown order

`App.DisposeAsync` (`PLang/app/this.cs`) runs these steps in order:

1. cancel and dispose the shutdown token
2. `actor.list.DisposeAsync()`
3. `Code.DisposeAsync()`
4. `KeepAlive.DisposeAsync()`
5. `store.Dispose()`

Keep-alive items are disposed after actors and code, and before the store
closes. An item that needs the store while it disposes is safe at that point.

---

## 4. Behaviour notes

The collection is a plain backing `List<object>` used from the app's setup and
teardown paths, so it is deliberately minimal:

- **Not thread-safe.** `Add` and `Remove` touch the list without a lock. The
  callers are the single-threaded app lifecycle.
- **Insertion order.** `DisposeAsync` disposes items in the order they were added.
- **One-shot.** `DisposeAsync` is idempotent (a `_disposed` flag), so adding an
  item after teardown has run leaves it undisposed.
- **No duplicate check.** Adding the same instance twice leaves two entries;
  `Remove` drops the first match.

---

## Find ...

| You want | Source |
|---|---|
| the collection class | `PLang/app/keepalive/this.cs` |
| the app property (`KeepAlive`) | `PLang/app/this.cs`, the property near line 263 |
| the teardown that disposes it | `PLang/app/this.cs`, `App.DisposeAsync` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app tree: the root, the `@this` pattern, the collection-node rule |
| `../actor/start.md` | actors, which are disposed before keep-alive items |
| `../channel/start.md` | channels, the I/O streams an actor owns |
