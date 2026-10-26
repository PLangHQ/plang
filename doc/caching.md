# Caching

For someone writing a program. The question this page answers: how do I keep the answer of a step so the next call does not redo the work?

Put `cache for <duration>` on the step:

```plang
Start
- save "first" to file 'cached.txt'
- call ReadCached
- save "second" to file 'cached.txt'
- call ReadCached

ReadCached
- read file 'cached.txt', write to %value%
    cache for 5 minutes
- write out "read: %value%"
```

Output:

```
read: first
read: first
```

The file changed between the calls, and the second read still answered `first`. The cached answer stands in for the read, and the read did not run. (`test/module/on/cache` passes.)

## A key and sliding

- `cache for 5 minutes, key 'myKeyTest'` names the entry. Left out, the step itself is the key. The repo's `test/module/on/cache/basic` uses it.
- `cache 1 hour sliding` keeps the entry another hour on each hit. From `os/system/modules/on/cache.examples.md`; not run here.

## What a cache hit means

A cached answer is `%!data%` as if the step had run. A recovery is never cached (`test/module/on/cache/RecoveryIsNeverCached.test.goal`, passes).

Not tried: when an entry expires, and a cache across runs.

The action: `os/system/modules/on/cache.description.md`.
