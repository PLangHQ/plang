# Reading a file line by line

For someone writing a program. The question this page answers: how do I do something for each line of a file?

Read the file, split it into lines, loop over the list.

`names.txt`:

```
alpha
beta
gamma
```

`Start.goal`:

```plang
Start
- read names.txt, write to %text%
- split %text% into lines, write to %names%
- foreach %names%, call Say item=%item%

Say
- write out "name: %item%"
```

Output:

```
name: alpha
name: beta
name: gamma
name: 
```

The last line is empty. The file ends with a newline, so splitting keeps an empty piece after it. `split` keeps empty pieces unless it is told `Empty="drop"`.

## Dropping the empty piece

Saying `drop the empty ones` in the sentence (`split %text% into lines, drop the empty ones, write to %names%`) built, but the builder wrote no `Empty` property, and the empty line stayed. That is what was seen. The action written out does it:

```plang
- list.split(Value=%text%, Separator="line", Empty="drop"); variable.set(Name=%names%, Value=%!data%)
```

Output: `name: alpha`, `name: beta`, `name: gamma`.

See also [loops](loops.md) and [how a step becomes an action](how-a-step-becomes-an-action.md). The `split` action: `os/system/modules/list/split.description.md`. The file module page: [file](../os/system/modules/file/start.md).
