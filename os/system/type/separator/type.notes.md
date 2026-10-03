A text that is exactly a separator's name (or an alias: `lines`, `newline` for `line`) means that separator, whether it is written in the step or held by a variable: `%sep%` holding `comma` splits on `,`. To split or join on the word itself, write its characters some other way.
A named separator writes its name (`line`), any other its characters — a built program reads back the same.
