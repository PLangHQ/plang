Min — the lower bound (inclusive): the number after "between" (`random number between 1 and 100` → Min=1). When the step gives a bound you MUST write it; only leave it out when the step gives none (default 0).
Max — the upper bound (inclusive): the number after "and"/"to" (`… between 1 and 100` → Max=100). When the step gives a bound you MUST write it; only leave it out when the step gives none (default 100). `between 1 and 100` gives both → always `Min=1, Max=100`, never an empty `random()`.

- `get random number`, `pick a random number`, `roll …` → math.random. A plain `random number` with no bounds leaves both out (defaults 0–100).
