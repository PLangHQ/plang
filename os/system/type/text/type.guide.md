# Working with text

`text` is any textual content — a line you typed, a file you read, the body of a response. Its kind (plain, markdown, csv, html, …) comes from the file extension and is only a hint; it doesn't change how you work with the text.

## Reading its members

Every member of `text` is read with a dot, inside `%…%`:

- `%name.length%` — how many characters it holds (a number).
- `%name.toUpper()%`, `%name.toLower()%`, `%name.trim()%` — the text, transformed.

A method is the type's C# method by name: you write it case-insensitively, with parentheses, and you can chain methods. `%title.trim().toUpper()%` trims both ends and upper-cases the result. `length` is the one property — it reads without parentheses (`%name.length%`); everything else is a method and keeps its `()`.

## Arguments

Methods take their arguments positionally — text in quotes, numbers bare:

- `%greeting.replace("world", "there")%` — swap one substring for another.
- `%summary.maxLength(80)%` — cut to at most 80 characters (`"..."` marks the cut).

## Searching lines

`grep` and `grepCount` treat the text as lines:

- `%log.grep("ERROR")%` — the matching lines, each as `line number: line`, returned as a `list<text>`.
- `%log.grep("ERROR", 2)%` — the same, keeping 2 lines of context around each match.
- `%log.grepCount("ERROR")%` — just the count, a number.

Because `grep` returns a `list<text>`, you can loop over it:

```plang
Start
- read 'app.log', write to %log%
- foreach %log.grep("ERROR")%, call ReportLine line=%item%
```
