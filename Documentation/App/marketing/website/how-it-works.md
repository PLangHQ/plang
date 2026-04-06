# How PLang Works

---

## Headline
**Your intent compiles into running software. Here's exactly how.**

## Subheadline
PLang has two distinct phases: **build** (where AI helps) and **run** (where AI is gone). Understanding this split is the key to understanding why PLang works the way it does.

---

## Phase 1: Write — `.goal` Files

You write `.goal` files. They contain plain English steps describing what your software should do.

```plang
ProcessOrder
- select product from products where id=%request.productId%, write to %product%
- if %product% is empty then
    - write out {error: 'Product not found'}, status code 404
- set %total% = %product.price% * %request.quantity%
- insert into orders, product=%product.name%, quantity=%request.quantity%, total=%total%, write to %orderId%
- send email to %request.customerEmail%, subject: "Order Confirmed", body: "Your order #%orderId% for %product.name% (x%request.quantity%) totaling $%total% has been placed."
- write out {orderId: %orderId%, total: %total%, status: 'confirmed'}
```

### What you're looking at
- That's not a spec. Not documentation. Not a prompt. It's **the program**.
- Each line is a step. Each step maps to a built-in module (database, email, HTTP, math, conditions).
- Variables use `%name%` syntax. Dot notation navigates objects: `%product.price%`.
- `.goal` files are your source code. You version them. You diff them. You review them.

---

## Phase 2: Build — LLM Compilation

Run `plang build`. Here's what happens:

1. PLang reads each step in your `.goal` file
2. For each step, it sends the text to an LLM (once)
3. The LLM identifies which built-in module and action the step maps to
4. It produces a `.pr` file — a JSON instruction set

### Example: What the LLM produces

For the step `send email to %request.customerEmail%, subject: "Order Confirmed", body: "..."`, the build produces:

```json
{
  "action": "email",
  "method": "send",
  "parameters": [
    { "name": "to", "value": "%request.customerEmail%" },
    { "name": "subject", "value": "Order Confirmed" },
    { "name": "body", "value": "Your order #%orderId%..." }
  ]
}
```

### Key facts about the build
- **Incremental**: Only changed steps recompile. Unchanged steps keep their `.pr` files.
- **Inspectable**: Every `.pr` file is human-readable JSON. Open it, read it, verify it.
- **One-time cost**: The LLM runs at build time. A small fee per step (typically $0.002–$0.009 per line). After that, running costs nothing.
- **Deterministic output**: The `.pr` file maps to exact module calls. No interpretation at runtime.

---

## Phase 3: Run — Deterministic Execution

Run `plang`. Here's what happens:

1. PLang loads the `.pr` files
2. Each instruction calls a built-in C# module directly
3. No LLM. No network (unless your app makes HTTP calls). No interpretation.
4. Same input → same output. Every time.

### What's available at runtime (built in)

| Module | What it does |
|--------|-------------|
| **database** | SQLite by default. Insert, select, update, delete. Zero config. |
| **http** | GET, POST, PUT, DELETE. Send and receive. Sign requests automatically. |
| **file** | Read, write, copy, delete, watch for changes. |
| **email** | Send via SMTP. |
| **crypto** | Encrypt, decrypt, hash, sign, verify. |
| **identity** | Ed25519 key pairs. `%Identity%` on every request. No passwords. |
| **schedule** | `every 5 minutes`, `sleep for 2 seconds`. |
| **webserver** | Start an HTTP server. Define endpoints with goal names. |
| **llm** | Call an LLM from your running app (when YOU want AI at runtime). |
| **condition** | If/else logic. |
| **loop** | Iterate collections. |
| **math** | Arithmetic, rounding, random numbers. |
| **list** | Add, remove, sort, filter, join. |
| **output** | Write to console or HTTP response. |
| **convert** | JSON, Base64, type conversions. |
| **error** | Throw and handle errors with retries. |

40+ modules. Zero imports. Zero config. Zero `npm install`.

---

## The Trust Argument

### "How do I know what my app actually does?"

Open the `.build/` folder. Every step has a corresponding `.pr` file. It's JSON. Read it.

```
MyApp/
├── Start.goal              ← You write this
├── .build/
│   └── Start/
│       ├── 00. select product.pr    ← You can read this
│       ├── 01. if product is empty.pr
│       ├── 02. set total.pr
│       ├── 03. insert into orders.pr
│       ├── 04. send email.pr
│       └── 05. write out.pr
```

Every parameter. Every function call. Every return value. All visible. Nothing is hidden behind a black box.

If a step compiled incorrectly, you'll see it in the `.pr` file. Fix the wording in your `.goal` file, rebuild that step, and it's corrected. You're always in control.

---

## The Build-Time / Runtime Split — Why It Matters

| | Build Time | Runtime |
|---|-----------|---------|
| **AI involved?** | Yes — LLM compiles steps | No — deterministic execution |
| **Internet required?** | Yes — LLM API call | No (unless your app calls HTTP) |
| **Cost** | Small per-step fee ($0.002–$0.009) | Zero |
| **When it runs** | Once per changed step | Every time you run the app |
| **Output** | `.pr` files (JSON) | Your app's behavior |

This is the difference between PLang and every AI coding assistant. Copilot, Cursor, and ChatGPT generate code you have to read, understand, and maintain. PLang uses AI as **infrastructure** — a compiler phase — and your natural language stays as the source of truth.

---

## What PLang Is Best For Today

PLang is in active development. Here's where it shines right now:

- **Internal tools** — dashboards, admin panels, data processors
- **Automation** — scheduled tasks, file processing, API integrations
- **Prototypes** — validate ideas before committing engineering resources
- **Side projects** — build real things on weekends without the setup tax
- **Workflows** — multi-step processes involving databases, email, HTTP, files

It's honest to say: PLang is young. The ecosystem is growing. For production systems at scale, evaluate carefully. For everything else — write what you mean and run it.

---

## Next Step

[Get Started →](/get-started) — Install PLang and build your first app in 60 seconds.
