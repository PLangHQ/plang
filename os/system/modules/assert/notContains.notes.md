Container — what is searched, the haystack: the value named BEFORE "contains" · say: `assert X does not contain …` → X · builder: `assert %list% does not contain "a"` → Container=%list%
Value — what it must NOT hold, the needle: the value named AFTER "contains" · say: `… does not contain Y` → Y · builder: → Value="a"

- `assert X does not contain Y` is Container=X, Value=Y — X is the holder, Y the thing it must not hold. Never the other way round.
