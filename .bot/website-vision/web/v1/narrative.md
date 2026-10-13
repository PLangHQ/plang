# plang.is — One-page narrative (draft v1)

Each screen has one headline, one line of text and one visual. The visitor's next question decides what the next screen shows.

---

### 1 · Hero (what is this?)
**Intent is the new source code.**
Write what you mean in plain language. PLang compiles it into software you can read, own and trust.
*Visual:* a real goal file, centred and large. The puffin stands quietly beside it.
*CTA:* `Install PLang 1.0b` · `Read the docs`

### 2 · Readable (what does it look like?)
**Write what you mean.**
No syntax to learn. A goal is a list of steps, in your words.
*Visual:* one complete goal, 4–6 lines, doing something real:
```
SendWeeklyReport
- select orders from last 7 days, write to %orders%
- [llm] summarize %orders% for the sales team, write to %summary%
- send email to team@acme.com, subject "Weekly sales", body %summary%
```
⚠️ This example is illustrative. It must build on 1.0b before it ships.

### 3 · Compiled (is the AI running my app?)
**AI at build time. Not at runtime.**
Each step is compiled once into a precise action. What runs is deterministic and doesn't need AI.
*Visual:* the one motion on the page. A step line resolves into its compiled action:
`get https://…, write to %response%` → `http.request · variable.set`

### 4 · Transparent (can I trust what it built?)
**Every step, open.**
The compiled output is plain JSON. Read it, diff it, commit it.
*Visual:* a `.pr` excerpt next to the step it came from.

### 5 · Signed I/O (is it safe?)
**Everything in and out is signed.**
Every app has its own identity, and all I/O is signed by default.
*Visual:* a request with its signature, verified ✓.
*Second line (pillar 6):* Apps stay inside their own folder. Access outside it is denied.

### 6 · Complete (what can I build?)
**Everything included.**
HTTP, database, files, LLM, crypto, identity, UI, timers, cache. Built in, not installed.
*Visual:* a quiet grid of module names. No icons, no descriptions.

### 7 · Start (how do I begin?)
**Start writing what you mean.**
PLang 1.0b. Open source. One line on Windows 10/11, with nothing else to install.
*Visual:* one command with a copy button:
```
irm https://plang.is/start.ps1 | iex
```
Below it, four quiet facts (from the os bot, confirmed by Ingi):
- Downloads PLang and checks every download's sha256.
- Opens `.goal` files with PLang, so a double-click runs a goal.
- Runs PlangOS, a small Linux in WSL where only PLang runs.
- Keeps your files on their own disk, so updates never touch them.

macOS/Linux: "coming", stated plainly. No fake command.
Links: GitHub · Docs · Discord.

### Footer
The puffin, small. The early-stage note in one quiet line: "1.0 beta: expect rough edges." Links to terms and privacy.

---

**Decisions in this draft**
- **Signed and Contained share screen 5.** Both answer "is it safe?", and seven screens hold attention better than eight.
- **Pricing leaves the homepage.** It moves to the docs or an install page, once the 1.0b model is known.
- **The puffin appears in three places only:** hero, nav logo and footer.
