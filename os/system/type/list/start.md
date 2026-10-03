# list
An ordered list of values, each of any type.

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

## count
How many items the list has.

`%orders.count%`

**Returns:** a number.

## first
The first item.

`%orders.first%`

**Returns:** the first item.

## last
The last item.

`%orders.last%`

**Returns:** the last item.

## random
A randomly chosen item.

`%orders.random%`

**Returns:** one item, at random.

## all
Every item. A plain list is itself; a loading list (a goal's items, a config's) reads them all, taking its options through the optional dict.

`%orders.all%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| setting | leave it out for a plain list | dict | no | — | options for a loading list, as a dict |

**Returns:** a list.
