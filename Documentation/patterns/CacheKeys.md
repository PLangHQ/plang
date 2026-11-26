# Cache keys

A report query that takes a second should not run on every page load. Caching the step's answer fixes that, but the cache gives back
whatever is stored under the key, so a key that leaves out one input gives one visitor's answer to another question. The pattern:
the key holds every input that changes the answer (filters, dates, the user when the answer is the user's), and the cache has an
expiry.

## 1. The cached step

```plang
/ report/OrderSummary.goal
OrderSummary
- select count(*) as orders, sum(total) as revenue from orders
    where status=%status% and created >= %from%
    return 1 row, write to %summary%
    cache "orderSummary_%status%_%from%" for 5 minutes
- write out "%status%: %summary.orders% orders, %summary.revenue%"
```

- The key is text with the step's inputs in it: `orderSummary_paid_2026-01-01` and `orderSummary_refunded_2026-01-01` are two entries.
- `for 5 minutes` is counted from when it was stored. A new order is not in the answer until the entry expires: after an insert of
  1000, the summary still said `paid: 2 orders, 350` within the 5 minutes. Pick the expiry by how old an answer may be.
- The cache is shared by every request of the app, and every visitor. An answer that belongs to one user needs the user in the key:
  `cache "myOrders_%user.id%_%from%" for 5 minutes`.

## 2. What a missing input does

The same query with `status` left out of the key, called with `paid` and then `refunded`:

```plang
    cache "orderSummary_%from%" for 5 minutes
```

| called with | answer |
|---|---|
| status=paid | `paid: 2 orders, 350` |
| status=refunded | `refunded: 2 orders, 350` (the paid answer) |

Two requests to a route, `/summary?status=paid` and then `/summary?status=refunded`, gave the same: the second visitor got the first
visitor's answer. Nothing fails and nothing is logged. The page shows a wrong number until the entry expires.

## 3. Check it was built

The cache is part of the step's build. Look at the goal's `.pr`:

```bash
jq '.GoalSteps[0].CacheHandler' ".build/report/OrderSummary/00. Goal.pr"
```

```json
{"TimeInMilliseconds": 300000, "CacheKey": "orderSummary_%status%_%from%", "CachingType": 1, "Location": "memory"}
```

`CacheKey` is your key with its variables, or `null` when the step names none, and `TimeInMilliseconds` your expiry. When it reads
`"TimeInMilliseconds": 0`, the step runs uncached on every call: build that goal again.

## Why this way

- **No key at all** (`cache for 5 minutes`) is cached too: the key is the step and the values of every %variable% the step reads,
  hashed, so `where status=%status%` is one entry for `paid` and one for `refunded`, and a setting never sits in a key. Write a key
  when the answer depends on something the step does not read, such as the user behind a query that does not name them.
- **A fixed key** (`cache "orderSummary" for 5 minutes`) is the wrong-answer case above for every input at once.
- **Every input in the key**, including the ones that are almost always the same: the one time a filter is changed is the time the
  wrong answer shows.

See also: [Caching](../CachingHandler.md), [Finding slow goals](./SlowGoals.md), [SQL in files](./SqlInFiles.md).
