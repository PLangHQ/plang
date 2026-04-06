# Get Started with PLang

---

## Headline
**From idea to running software. No setup ceremony.**

## Subheadline
Install PLang, write a `.goal` file in plain English, and run it. Your first app takes about 60 seconds.

---

## Step 1: Install PLang

Download the latest release for your platform from [GitHub](https://github.com/PLangHQ/plang/releases):

| Platform | Download |
|----------|----------|
| Windows | `plang-windows.zip` |
| Linux (x64) | `plang-linux-x64.zip` |
| Linux (ARM) | `plang-linux-arm64.zip` |
| macOS (Intel) | `plang-osx-x64.zip` |
| macOS (M-series) | `plang-osx-arm64.zip` |

Unzip and add the folder to your PATH.

**Verify it works:**
```bash
plang --version
```

For detailed platform-specific instructions, see the [full installation guide](https://github.com/PLangHQ/plang/blob/main/Documentation/Install.md).

---

## Step 2: Create Your First App

Create a new folder and a file called `Start.goal`:

```bash
mkdir HelloWorld && cd HelloWorld
```

Write your program in `Start.goal`:

```plang
Start
- write out 'Hello PLang world'
```

That's the whole program. One goal, one step.

---

## Step 3: Build and Run

```bash
plang exec
```

This does two things:
1. **Builds** — sends your step to the LLM, which compiles it into a `.pr` file
2. **Runs** — executes the compiled instruction

Output:
```
Hello PLang world
```

On your first run, you'll be prompted to set up a PLang service account or OpenAI API key for building. A $5 credit goes a long way — building each step typically costs $0.002–$0.009.

---

## Step 4: Build Something Real

Now let's make it interesting. Create a `Start.goal` that actually does something:

```plang
Start
- insert into tasks, description='Buy groceries', done=false, write to %id%
- insert into tasks, description='Read PLang docs', done=false
- select * from tasks, write to %tasks%
- write out %tasks%
```

Build and run:
```bash
plang exec
```

**What just happened:**
- PLang created a SQLite database automatically (no config, no migration, no Docker)
- Inserted two rows into a `tasks` table (the table was created on first insert)
- Queried all tasks and displayed them

You didn't install a database. You didn't write a schema. You didn't configure a connection string. You wrote what you wanted, and it worked.

---

## Step 5: Add More Power

Here's a task manager with an API and email notifications:

```plang
Start
- start webserver

AddTask - POST
- insert into tasks, description=%request.description%, assignee=%request.assignee%, done=false, write to %id%
- send email to %request.assignee%, subject: "New Task", body: "You've been assigned: %request.description%"
- write out {id: %id%, status: 'created'}

ListTasks - GET
- select * from tasks where done=false, write to %tasks%
- write out %tasks%

CompleteTask - POST
- update tasks set done=true where id=%request.id%
- write out {status: 'completed'}
```

```bash
plang exec
```

You now have a running web server on `http://localhost:8080` with three API endpoints, a database, and email notifications. In 15 lines of English.

---

## Project Structure

After building, your project looks like this:

```
HelloWorld/
├── Start.goal              ← Your source code
├── .build/                 ← Compiled .pr files (auto-generated)
│   └── Start/
│       ├── 00. insert into tasks.pr
│       ├── 01. insert into tasks.pr
│       ├── 02. select from tasks.pr
│       └── 03. write out.pr
└── .db/                    ← Database files (auto-created)
    ├── system.sqlite
    └── data.sqlite
```

- **`.goal` files** — your source code. Version these.
- **`.build/` folder** — compiled JSON instructions. Inspect these if you want to verify what the LLM produced.
- **`.db/` folder** — SQLite databases. Created automatically when your app uses database operations.

---

## What to Try Next

**Add error handling:**
```plang
- read config.json into %config%
    on error call HandleMissingConfig, continue to next step
```

**Schedule a recurring task:**
```plang
Start
- every 30 minutes, call !CheckForUpdates

CheckForUpdates
- get https://api.example.com/updates, write to %updates%
- if %updates.count% > 0 then call !ProcessUpdates
```

**Use conditions and loops:**
```plang
ProcessOrders
- select * from orders where status='pending', write to %orders%
- foreach %orders%, call !ProcessOrder item=%order%

ProcessOrder
- set %total% = %order.price% * %order.quantity%
- update orders set total=%total%, status='processed' where id=%order.id%
- write out 'Processed order %order.id%: $%total%'
```

---

## The Development Workflow

1. **Write** `.goal` files in plain English
2. **Build** with `plang build` (or `plang exec` to build + run)
3. **Inspect** `.pr` files in `.build/` if something seems off
4. **Fix** by editing the `.goal` file and rebuilding — only changed steps recompile
5. **Run** with `plang` — no AI, no network, deterministic

---

## Editor Support

Install the PLang extension for Visual Studio Code for syntax highlighting and F5 run support. See the [IDE setup guide](https://github.com/PLangHQ/plang/blob/main/Documentation/IDE.md).

---

## Honest Notes

- **Building costs money** — LLM calls at build time cost $0.002–$0.009 per step. A $5 credit lasts a long time.
- **Runtime is free** — no AI calls, no network, no ongoing cost.
- **PLang is young** — the ecosystem is growing, the community is small, and it's best suited for internal tools, automation, and prototypes today.
- **You can inspect everything** — `.pr` files are JSON. If the LLM compiled something wrong, you'll see it. Fix the wording, rebuild, done.

---

## You just built your first app.

From here, you have a database, HTTP client, email, file system, scheduling, cryptography, and 40+ more modules — all built in, all callable in plain English. No imports. No packages. No config files.

Write what you mean. Build once. Run forever.
