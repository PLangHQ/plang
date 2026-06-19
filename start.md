# PLang

PLang is a programming language you write in plain English.

You describe what you want to happen, step by step. PLang figures out how to do it.

---

## A program is a list of steps

Each step is one instruction. You write it like a sentence.

```plang
Start
- read file.md, write to %content%
- write out %content.h1.list%
- save file.md as file.html
```

That's a complete program. It reads a markdown file, prints all the h1 headings, and converts it to HTML.

---

## PLang understands what things are

When you read a `.md` file, PLang knows it's markdown. So `%content%` isn't just raw text — it has structure you can use directly.

```plang
- read file.md, write to %content%
- write out %content.h1.list%
- write out %content.h2.list.orderby(title desc)%
- write out %content.chapters%
```

- `%content.h1.list%` — all h1 headings in the file
- `%content.h2.list.orderby(title desc)%` — all h2 headings, sorted by title descending
- `%content.chapters%` — the file broken into chapters

You didn't write any parsing code. PLang knows the shape of a markdown file and gives you its parts.

---

## Converting is just renaming the extension

```plang
- save file.md as file.html
- save file.md as file.pdf
```

PLang sees the target extension and converts. `.html`, `.pdf`, `.csv`, `.json` — the step reads the same either way.

---

## Variables hold values between steps

`%name%` is a variable. You write to it in one step and read from it in the next.

```plang
- read file.md, write to %content%
- write out %content%
```

Variables carry type information with them. A variable that holds a markdown file knows it's markdown. One that holds a number knows it's a number.

---

## What's next

- [Steps](doc/app/goal/step/) — how steps work
- [Actions](doc/app/goal/step/action/) — what PLang does when a step runs
