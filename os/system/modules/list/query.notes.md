List — the list itself, never its name (`sort %people% by age` → List=%people%) · say: the list the query reads
Query — a dict of optional parts, each given only when the step's words name it: where (a condition), group (a field name), distinct (true), order (a field, or {field, desc}) · builder: a one-part sentence is a one-part query ("where %users% age > 20" → {where: {field: "age", op: ">", value: 20}})
where — a leaf {field, op, value} (op a condition operator: ==, >, <, contains, …), or several joined as {and: [...]} or {or: [...]}; a value may be a %variable%, read at run
order — a field name sorts ascending; {field, desc: true} sorts descending · builder: desc only when the step says highest/largest/newest/oldest first or descending — plain "order by X" is ascending
Writes back — a query answers a new list and never changes its input, so the step always has a destination: the one it names (`…, write to %kept%`), or, when it names none, the list it read — `sort %people% by age` writes the answer back with variable.set(Name=%people%, Value=%!data%)
Returns — a new list; after a group, a list of {key, items}.
