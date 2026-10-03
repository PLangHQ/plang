# Lists

A `list` is an ordered series of values, each of any type. You loop over one with `foreach`:

```plang
Start
- foreach %orders%, call Process order=%item%
```

## Reading items

- `%orders.count%` — how many items, a number.
- `%orders.first%`, `%orders.last%` — the ends; `%orders.random%` — any one.
- `%orders[0]%`, `%orders[2]%` — by position (0-based).
- `%orders.all%` — every item; for a loading list (a goal's items, a config's) this is what reads them all.
