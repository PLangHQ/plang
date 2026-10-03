# Separators

A `separator` is what cuts a text into pieces or goes between them — what `split` and `join` use.

## Named separators

Five are named, each standing for its characters:

- `line` — a line break (also `lines`, `newline`).
- `comma` — `,`
- `tab` — a tab.
- `space` — a single space.
- `semicolon` — `;`

```plang
Start
- split %text% into lines, write to %lines%
- join %names% with comma, write to %csv%
```

## Any characters

Anything else is taken as the literal characters to use — `join %parts% with " | "`.

A text that is *exactly* a separator's name means that separator, whether written in the step or held in a variable (`%sep%` holding `comma` splits on `,`). To split or join on the word "comma" itself, write it some other way.
