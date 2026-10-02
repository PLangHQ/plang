Container — what is searched, the haystack: the value named BEFORE "contains" · say: `assert X contains …` → X · builder: `assert %list% contains "a"` → Container=%list%
Value — what it must hold, the needle: the value named AFTER "contains" · say: `… contains Y` → Y · builder: → Value="a"

- `assert X contains Y` is Container=X, Value=Y — X is the holder, Y the thing inside it. Never the other way round.
