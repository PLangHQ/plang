# Goal

A goal is a named list of steps. It's the basic building block of a PLang program.

You write a goal by giving it a name on the first line, then listing the steps below it.

```plang
Start
- read file.md, write to %content%
- write out %content.h1.list%
- save file.md as file.html
```

`Start` is the name of this goal. When you run your program, PLang looks for a goal called `Start` and begins there.

---

## Goals live in files

Each goal lives in a `.goal` file. The filename is the goal name.

```
Start.goal        ← runs first
ConvertFile.goal  ← called when needed
SendReport.goal   ← called when needed
```

---

## Goals can call each other

A step in one goal can run another goal.

```plang
Start
- read file.md, write to %content%
- call ConvertFile
- call SendReport
```

```plang
ConvertFile
- save file.md as file.html
- save file.md as file.pdf
```

```plang
SendReport
- write out "Done. Files saved."
```

Each goal does one thing. You compose them to build a program.

---

## What's inside a goal

- [Step](../step/) — the individual instructions that make up a goal
