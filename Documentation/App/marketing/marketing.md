# PLang — Signal Maker Ground Truth

**Purpose:** This is the technical source of truth for the Signal Maker. Every marketing claim must trace back to a fact listed here. If it's not in this document, don't say it. If you can't prove it in 10 seconds, don't promise it.

---

## 1. What PLang Actually Is (So You Don't Oversell It)

PLang is a natural language programming language. Programs are `.goal` files containing steps in plain English. At **build time**, an LLM compiles each step into a `.pr` file (JSON instruction) mapped to one of 40+ built-in C# modules. At **runtime**, no LLM is involved — deterministic execution, same input, same output.

The `.goal` files ARE the source code. They're what you version, diff, and read. The compiled `.pr` files are intermediate artifacts — like `.class` files in Java. Nobody maintains them.

**The one-sentence truth:** PLang is a compiler that understands English, not a chatbot that writes code.

---

## 2. The Claims You Can Make (And the Proof Behind Them)

### Claim: "Your intent is the source code"
**Technical backing:** `.goal` files contain natural language steps. These are the files developers write, version, and maintain. The LLM translates them into `.pr` JSON at build time. The natural language IS the program — not a comment, not documentation, not a prompt.
**How to prove it:** Show a `.goal` file. If someone reads it and understands what it does without explanation, the claim is proven.

### Claim: "AI builds it. You run it. No AI bill at runtime"
**Technical backing:** The LLM runs at build time only. `plang build` calls the LLM once per changed step. `plang` runs with zero network calls, zero LLM calls. Incremental builds — unchanged steps aren't recompiled.
**How to prove it:** Build an app, disconnect from internet, run it. It works.
**Why this matters:** This is the sharpest differentiator against Copilot, Cursor, Devin, and every AI coding tool. They generate code you must understand and maintain. PLang uses AI as infrastructure, not as a crutch.

### Claim: "Batteries included. All of them"
**Technical backing:** 40+ built-in modules: database (SQLite default), HTTP, files, crypto, caching, scheduling, LLM integration, email, code execution, UI rendering, and more. No package manager. No dependency resolution. No `node_modules`. No `pip install`.
**How to prove it:** Show a 10-line app that does database + HTTP + file operations + authentication. Count the import statements: zero.

### Claim: "Authentication without passwords"
**Technical backing:** Every PLang app auto-generates Ed25519 key pairs. `%Identity%` is the public key. Every HTTP request is cryptographically signed. Server validates signature — if valid, `%Identity%` is populated. No passwords, no sessions, no tokens, no auth libraries.
**How to prove it:** Show the full auth flow in 5 lines of PLang vs. the equivalent in Express + Passport + bcrypt + JWT + session middleware.

### Claim: "Your app has a database. Already"
**Technical backing:** SQLite is the default. `- insert into users, name=%name%` works on first run with no configuration, no connection string, no Docker container, no migration tool. Multi-datasource support, cross-database joins, per-user databases for GDPR.
**How to prove it:** `plang build && plang` on a fresh project with database operations. No setup step.

### Claim: "Security by design, not by configuration"
**Technical backing:** Automatic SQL injection protection (parameterized queries), automatic CSRF protection (signed requests), no session hijacking (stateless per-request auth), app sandboxing (each app confined to its folder), transparent code (`.pr` files are inspectable JSON).
**How to prove it:** List the OWASP Top 10 and show how many are eliminated by architecture rather than configuration.

---

## 3. The Claims You Cannot Make

These are real limitations. Making claims that contradict them will destroy developer trust instantly.

| Don't say | Because |
|---|---|
| "Works offline" (without qualifier) | Build time requires LLM API access + internet. *Runtime* is offline-capable |
| "Enterprise-grade security" | Keys are currently stored unencrypted. Bio/pin unlock is planned, not shipped |
| "High performance" / "Scales to millions" | C# runtime + SQLite. Fine for most use cases. Not competing with Go/Rust on throughput |
| "Rich ecosystem" / "Thousands of packages" | Young ecosystem, limited community, few third-party modules. Strength is built-in modules, not ecosystem |
| "Zero bugs from AI compilation" | LLM can produce incorrect `.pr` files. The claim is *transparency and inspectability*, not perfection |
| "Production-ready for enterprise" | Best suited today for internal tools, prototypes, automation, and side projects |
| "Concurrent" / "Parallel" | Single-threaded execution model |
| "Replaces Python/JavaScript" | PLang replaces the *situation* (boilerplate, ceremony, setup), not the language |

---

## 4. Competitive Positioning

### The frame PLang wins
PLang doesn't compete with programming languages. It competes with the **friction between having an idea and running it**.

- **vs. No-code tools (Zapier, Bubble, Airtable):** They hit walls. PLang is the next step without learning traditional syntax
- **vs. AI coding assistants (Copilot, Cursor):** They generate code you maintain. PLang makes your intent the code
- **vs. Boilerplate-heavy frameworks:** 200 lines of setup before your first line of business logic. PLang: zero setup
- **vs. Traditional scripting:** Domain experts using Excel macros or Bash scripts they half-understand. PLang reads like English

### The frame PLang loses
- **vs. Python/JS for production systems at scale** — don't take this fight
- **vs. Established ecosystems with rich libraries** — don't take this fight
- **vs. Performance-critical applications** — don't take this fight

### Category to own
**Intent-first programming.** Not low-code (GUI constraints). Not no-code (template limits). Not AI-assisted coding (you still maintain generated code). PLang is where what you write IS what you mean.

---

## 5. Audience Pain Map

### Primary: Builders blocked by syntax
**Who:** Founders, product managers, domain experts, automation thinkers
**Pain language they actually use:**
- "I know exactly what I want to build, I just can't write the code"
- "I've tried no-code tools but I always hit a wall"
- "I hired a developer for something that should have been simple"
- "I understand the logic, I just don't know the syntax"

**What converts them:** Seeing a `.goal` file and understanding it instantly. The moment of "wait, that's the program?" is the conversion moment.
**What loses them:** Any whiff of vaporware. They've been burned by "revolutionary" tools before.

### Secondary: Developers tired of ceremony
**Who:** Senior developers, indie hackers, side-project builders
**Pain language they actually use:**
- "I spent 3 hours setting up auth before I wrote one line of business logic"
- "Another framework. Another ORM. Another migration tool"
- "I just want to build the thing, not configure the thing"
- "Half my code is plumbing"

**What converts them:** The module count (40+) plus zero config plus identity built in. They do the mental math and realize they just saved days of setup.
**What loses them:** Lack of control. They need to see the `.pr` files, the escape hatch to C#, and the transparent compilation. They won't trust what they can't inspect.

### Tertiary: Technical leaders evaluating tools
**Who:** CTOs, engineering managers, team leads
**Pain language they actually use:**
- "We need to move faster without adding headcount"
- "Our junior devs spend months becoming productive"
- "We need to prototype before committing engineering resources"

**What converts them:** Readable code anyone on the team can understand + built-in security model + inspectable output.
**What loses them:** "Is it production-ready?" — be honest: young ecosystem, best for internal tools and prototypes today.

---

## 6. Messaging Hierarchy

### The line
*Stop writing code. Start writing what you mean.*

### The elevator pitch (30 seconds)
PLang is a programming language where you write in plain English. Your steps compile into executable code using AI at build time — but your app runs independently, with no AI at runtime. Database, authentication, HTTP, files, crypto — all built in. No packages, no config, no ceremony. Just write what you want and run it.

### The landing page narrative (scroll order)
1. **Hero:** The line + one code example that proves it
2. **The split:** Build-time AI → runtime independence (this is the "aha")
3. **The proof:** Real app in 10 lines doing 5 things (database, HTTP, auth, files, UI)
4. **The model:** How `.goal` files become `.pr` files become running software
5. **The security story:** `%Identity%`, no passwords, signed requests, OWASP coverage
6. **The comparison:** PLang lines vs. equivalent framework setup lines
7. **The action:** Install, first build, first run — 60 seconds to working software

### The conference talk (5 minutes)
"Every programming language was designed for computers. You learn the machine's rules and translate your ideas into its syntax. PLang inverts this." → Live demo: write a goal, build it, run it. Show the `.pr` file. Show it runs offline. Show the 5-line auth flow. End with the line.

---

## 7. Objection Responses

| Objection | Honest response |
|---|---|
| "Is this real or vaporware?" | Show the build. Show the `.pr` files. Show it running. PLang is open source — inspect the runtime yourself |
| "What if the AI compiles wrong?" | Every `.pr` file is human-readable JSON. You can inspect and verify every step. Incremental builds mean you fix one step, not the whole app |
| "Can it do [complex thing]?" | The `[code]` module is an escape hatch to C#. If the built-in modules don't cover it, you write custom logic |
| "What about performance?" | C# runtime + SQLite. Appropriate for internal tools, prototypes, automation, web apps. Not designed for high-frequency trading |
| "Lock-in?" | `.goal` files are plain text. `.pr` files are JSON. No proprietary binary formats. The logic is readable English |
| "Who else uses this?" | Young ecosystem, growing community. Be honest about maturity. Strength is the vision and the architecture, not the install count (yet) |
| "What about testing?" | Build incrementally, test each goal. `.pr` files are inspectable. Event sourcing provides audit trail |
| "What if I lose my private key?" | Multi-device identity linking via email verification. Designed for real-world usage |

---

## 8. Content Strategy Guardrails

### Always do
- Lead with a working example, not a feature list
- Show real PLang code — it's the best marketing asset because it's readable English
- Compare against the *situation* (boilerplate, ceremony, setup), not against languages
- Be honest about maturity — developers respect honesty more than polish
- Include the build-time/runtime distinction in every piece of content — it's the key differentiator

### Never do
- Don't use "revolutionary" or "game-changing" — let the demo speak
- Don't compare PLang to Python/JavaScript as if they're competitors
- Don't show pseudo-PLang that violates real constraints (inline loop substeps, inline error handlers)
- Don't hide the LLM build dependency — be upfront, then explain why it's a feature not a bug
- Don't promise enterprise readiness before it's real
- Don't use stock illustrations of robots or AI brains — the code IS the visual

---

## 9. The Feeling Every Piece of Marketing Should Create

1. **"Wait, that's the program?"** — the moment they read a `.goal` file and realize it's not pseudocode
2. **"Why hasn't this existed before?"** — the realization that natural language compilation is now viable
3. **"I could build [my thing] with this"** — the mental leap from reading to imagining their own use case
4. **"This is honest"** — trust built from transparent limitations and inspectable output

The Signal Maker's job is to engineer moment #1, enable moment #3, and never compromise moment #4.