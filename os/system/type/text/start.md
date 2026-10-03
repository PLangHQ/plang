# text
Textual content. Kind is set from the file extension (md, txt, csv, html, ...). Kind is a hint by default; strict is a no-op for text (plain vs markdown is not detectable from content).

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

## length
How many characters the text holds.

`%text.length%`

**Returns:** a number.

## toUpper
The same text with every letter upper-cased.

`%text.toUpper()%`

**Returns:** the upper-cased text.

## toLower
The same text with every letter lower-cased.

`%text.toLower()%`

**Returns:** the lower-cased text.

## trim
The text with whitespace trimmed from both ends.

`%text.trim()%`

**Returns:** the trimmed text.

## replace
The text with every occurrence of one substring swapped for another.

`%text.replace("old", "new")%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| old | the first argument | text | yes | — | the substring to find |
| new | the second argument | text | yes | — | what to put in its place |

**Returns:** the text with every match replaced.

## maxLength
The text cut to at most max characters, with "..." marking the cut.

`%text.maxLength(80)%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| max | the first argument, a number | number | yes | — | the longest the result may be; 0 means no limit |

**Returns:** the text, no longer than max characters (plus "..." when it was cut).

## grep
The lines that match a pattern, each returned as "line number: line", with context lines around each match when asked.

`%text.grep("TODO")%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| pattern | the first argument | text | yes | — | what each line is matched against |
| lines | the second argument, a number (leave it out for none) | number | no | — | how many lines of context to keep around each match; 0 (none) by default |

**Returns:** a list of the matching lines (list<text>).

## grepCount
How many lines match a pattern.

`%text.grepCount("TODO")%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| pattern | the first argument | text | yes | — | what each line is matched against |

**Returns:** a number: the count of matching lines.
