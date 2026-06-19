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

<details style="margin:20px 0;background:#FFFFFF;border:1px solid #E4E7E4;border-radius:10px;padding:0;overflow:hidden;">
<summary style="font-family:'IBM Plex Mono',monospace;font-size:13px;color:#2C6E8C;padding:14px 20px;cursor:pointer;list-style:none;display:flex;align-items:center;gap:8px;">
  <span style="font-size:11px;">▶</span> What is file.md?
</summary>
<div style="padding:20px 24px 24px;border-top:1px solid #E4E7E4;">
  <p style="font-size:16px;line-height:1.65;color:#525C64;margin:0 0 18px;">A <code style="font-family:'IBM Plex Mono',monospace;font-size:0.9em;background:#E9F0F3;color:#2C6E8C;padding:2px 6px;border-radius:4px;border:1px solid #D9E6EB;">.md</code> file is a plain text file. You write it in any text editor — Notepad, TextEdit, VS Code, anything. The <code style="font-family:'IBM Plex Mono',monospace;font-size:0.9em;background:#E9F0F3;color:#2C6E8C;padding:2px 6px;border-radius:4px;border:1px solid #D9E6EB;">#</code> marks headings, and <code style="font-family:'IBM Plex Mono',monospace;font-size:0.9em;background:#E9F0F3;color:#2C6E8C;padding:2px 6px;border-radius:4px;border:1px solid #D9E6EB;">-</code> marks list items.</p>
  <p style="font-size:14px;font-family:'IBM Plex Mono',monospace;color:#97A0A7;margin:0 0 8px;letter-spacing:0.04em;text-transform:uppercase;">file.md — copy this</p>
  <div style="position:relative;">
    <pre style="font-family:'IBM Plex Mono',monospace;font-size:14px;line-height:1.8;background:#F6F8FA;border:1px solid #E4E7E4;border-radius:8px;padding:18px 20px;margin:0;overflow-x:auto;color:#3A434C;"><code id="filemd-example"># My notes

## Shopping list
- Apples
- Bread
- Milk

## Ideas
- Learn PLang
- Build something</code></pre>
    <button onclick="navigator.clipboard.writeText(document.getElementById('filemd-example').innerText).then(()=>{this.textContent='Copied';setTimeout(()=>this.textContent='Copy',1500)})" style="position:absolute;top:10px;right:10px;font-family:'IBM Plex Mono',monospace;font-size:12px;background:#fff;border:1px solid #D0D7DE;border-radius:5px;padding:4px 10px;color:#5C666E;cursor:pointer;">Copy</button>
  </div>
  <p style="font-size:16px;line-height:1.65;color:#525C64;margin:18px 0 0;"><strong style="color:#1A2128;">Where to put it:</strong> Create <code style="font-family:'IBM Plex Mono',monospace;font-size:0.9em;background:#E9F0F3;color:#2C6E8C;padding:2px 6px;border-radius:4px;border:1px solid #D9E6EB;">file.md</code> in the same folder as your PLang program — the same folder where your <code style="font-family:'IBM Plex Mono',monospace;font-size:0.9em;background:#E9F0F3;color:#2C6E8C;padding:2px 6px;border-radius:4px;border:1px solid #D9E6EB;">Start.goal</code> file lives.</p>
</div>
</details>

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

See for yourself — write out everything `%content%` contains:

```plang
- read file.md, write to %content%
- write out %content!%
```

`%content!%` prints all the properties PLang found in the file.

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
