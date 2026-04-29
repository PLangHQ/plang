# Cache Module

Cache an action's result and return it directly on the next call.

`cache` is an **action modifier**: it wraps a single action, checks the cache before running it, stores the successful result after.

## Actions

### wrap (written as `cache for ...`)

```plang
/ Cache the result for 10 minutes
- read 'data.json'
    cache for 10 minutes
    write to %data%

/ Cache for 60 seconds, explicit key
- http get 'https://api.example.com/items'
    cache for 60 seconds key='items-feed'
    write to %items%

/ Sliding expiration — each access refreshes the TTL
- file read 'costly.txt'
    cache for 5 minutes sliding
    write to %content%
```

On a cache **hit**, the wrapped action is skipped entirely and the stored result is returned as if it had just run. On a **miss**, the action runs normally and — only if it succeeded — its result is stored. Failures are never cached.

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| DurationMs | long | yes | — | Cache lifetime in milliseconds |
| Sliding | bool | no | false | If true, each access resets the TTL |
| Key | string | no | auto | Explicit cache key. Defaults to `step:{goalPath}:{stepIndex}` |

### Default cache key

When you omit `key=`, the cache key is derived from the goal path and the step's position in the goal. That means **same step, same cache entry** — ideal for idempotent step-level caching without any bookkeeping.

Use an explicit `key=` when you want two different steps (or two goals) to share the same cache entry, or when the cache value depends on a dynamic input:

```plang
- http get '%url%'
    cache for 60 seconds key='feed-%url%'
    write to %data%
```

## Scope

- **Per-action, not per-step.** Only the action directly preceding the `cache for` clause is cached. Subsequent actions in the same step still run on every execution.
- **Success-only.** A failed action (non-success `Data`) is not stored.
- **Shared by reference.** Cached values are returned as-is. Mutating a cached value changes the cache entry too — treat cached results as read-only.

## Example

```plang
TestCacheOnFileRead
- save 'testdata/cache_test.txt' with content 'cached content'
- read file 'testdata/cache_test.txt'
    cache for 60 seconds
    write to %content1%
- read file 'testdata/cache_test.txt'
    cache for 60 seconds
    write to %content2%
/ Both reads return the same cached value
- assert %content1% equals %content2%
```

See `Tests/Modules/Modifiers/CacheOnFileRead.test.goal` for the runnable test.
