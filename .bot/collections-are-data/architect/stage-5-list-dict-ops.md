# Stage 5 — list/dict ops as exposure — `where` on `dict`+`list`

**Leaf-trace row:** H (`list` action handlers). **Phase B — sequenced after Stage 4 (reuses its relocated compare).**

**You own the final shape.** Anchors for the design — change what reads wrong, keep the dispositions.

## The state today

`module/list/` has 18 actions (`add any contains count first flatten get group indexof join last range remove reverse set sort split unique`), **no `where`**. They disagree with each other:
- `sort.cs:19` sorts raw values with `Comparer<object>.Default` — can't key by a field.
- `group.cs:18-27` keys via `Data` (`item.GetChild(key)`) but stores raw `item.Value` — the opposite shape, same module.

## Do

- The `list`/`dict` value types own the operations; the element `Data` owns per-element key/compare. Action handlers collapse to **thin dispatch** — declare the PLang-visible params and forward, no algorithm.
- Add `where` as a **`dict`+`list`** capability:
  - `dict.where` is the leaf — subject is the dict itself, predicate keeps or drops it; bare field names scope against it (`age` → `%self.age%`).
  - `list.where` delegates to `dict.where` per element — subject is each element, predicate filters.
  - `5 where age > 20` stays meaningless (the apex has no fields to scope into) — correct.
- `sort`/`group` stay `list`-only (ordering needs ≥2; grouping one thing is a bucket of one). `group` buckets are themselves `Data<List<data>>`.

```text
- sort %people% by "age" desc   / list.sort keys each element via %item.age% (Data), typed compare
- where %people% age > 18       / list.where evaluates the predicate per element Data
- group %people% by "city"      / buckets are Data<List<data>>
```

## Acceptance

- `where %users% age > 20` filters a list; `where %user% age > 20` keeps/drops one dict.
- `where age > 20` reads identically on one item and a thousand.
- `sort`/`group`/`unique` go through the one compare path (Stage 4).
- handlers hold no element-handling logic — dispatch only.

## Green

Both suites pass. Depends on Stage 4 (typed compare) being in.
