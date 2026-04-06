# PLang Homepage Content

---

## Section 1: Hero

### Headline
**Stop writing code. Start writing what you mean.**

### Subheadline
PLang is a programming language where plain English compiles into working software. Write your intent, build once, run forever — no AI at runtime.

### Hero Code Example
```plang
Start
- get https://api.weather.com/today, write to %weather%
- insert into reports, date=%Now%, temperature=%weather.temp%, summary=%weather.description%
- if %weather.temp% < 0 then call !SendFrostAlert
- write out 'Weather logged: %weather.description%'

SendFrostAlert
- send email to %settings.alertEmail%, subject: "Frost Warning", body: "Temperature dropped to %weather.temp%°C"
```

### Hero Caption
That's not pseudocode. That's the entire program. Database, HTTP, email, conditions — all built in. No imports. No config. No dependencies.

---

## Section 2: The Split — Build-Time AI vs. Runtime Independence

### Headline
**AI builds it. You run it. No AI bill at runtime.**

### Left Column: Build Time
**`plang build`**
- An LLM reads your English steps
- Compiles each into a `.pr` file (JSON instructions)
- Maps to one of 40+ built-in modules
- Runs once per changed step — incremental builds

### Right Column: Runtime
**`plang`**
- Zero LLM calls
- Zero network dependency
- Deterministic: same input, same output
- Runs offline after build

### The Point
Every AI coding tool generates code *you* have to understand and maintain. PLang uses AI as a compiler — your intent stays readable, your app runs independently.

---

## Section 3: The Proof — A Real App in 10 Lines

### Headline
**A complete app. Not a demo.**

```plang
Start
- start webserver

NewContact - POST
- insert into contacts, name=%request.name%, email=%request.email%, write to %id%
- send email to %request.email%, subject: "Welcome", body: "Thanks for signing up, %request.name%"
- write out {id: %id%, status: 'created'}

ListContacts - GET
- select * from contacts, write to %contacts%
- write out %contacts%
```

### What just happened
- **Web server** started on port 8080
- **Database** created automatically — no config, no migration, no Docker
- **Two API endpoints** defined with HTTP methods
- **Email** sent on contact creation
- **JSON responses** returned to the client
- **Authentication** handled via built-in identity signing

Line count in Express.js for the same thing: ~80. In Django: ~60. In PLang: 10.

---

## Section 4: The Model — How PLang Works

### Headline
**From English to running software in three steps**

### Step 1: Write
You create `.goal` files — plain English steps describing what your app should do.
```
Start.goal → Your source code. Version it, diff it, read it.
```

### Step 2: Build
`plang build` sends each step to an LLM once. The LLM maps your intent to built-in modules and produces `.pr` files — JSON instructions the runtime understands.
```
.build/Start/00. start webserver.pr → Inspectable JSON. No magic.
```

### Step 3: Run
`plang` executes the `.pr` files directly. No AI. No network. No surprises.
```
$ plang
Webserver running on http://localhost:8080
```

### The trust argument
Every instruction your app executes lives in a `.pr` file you can open and read. It's JSON. You can inspect every parameter, every function call, every return value. Nothing is hidden.

---

## Section 5: The Security Story

### Headline
**Authentication without passwords. Security without configuration.**

### Identity
Every PLang app generates an Ed25519 key pair automatically. Your `%Identity%` is your public key. Every HTTP request is cryptographically signed. The server validates the signature — if it checks out, it knows who you are. No passwords. No sessions. No tokens. No auth libraries.

```plang
SecureEndpoint - POST
- if %Identity% is empty then
    - write out {error: 'Not authenticated'}, status code 401
- insert into messages, author=%Identity%, content=%request.message%
- write out {status: 'saved'}
```

### What's handled by default
- **SQL injection** — parameterized queries, always
- **CSRF** — signed requests, built in
- **Session hijacking** — stateless authentication, nothing to steal
- **App sandboxing** — each app confined to its own folder

---

## Section 6: The Comparison

### Headline
**Less ceremony. Same result.**

| Task | Traditional Stack | PLang |
|------|------------------|-------|
| Web server + 2 endpoints | Express: `npm init`, install deps, create server, define routes, error handling (~40 lines) | 3 lines |
| Database CRUD | ORM setup, migration tool, connection config, model definitions (~50 lines) | Write directly: `insert into`, `select from` |
| Send email on event | SMTP library, config, template engine, error handling (~30 lines) | `send email to %address%, subject: "...", body: "..."` |
| User authentication | Passport.js + bcrypt + JWT + session middleware + user model (~100 lines) | Built in. `%Identity%` exists on every request |
| Scheduled task | cron setup, daemon config, logging, error handling (~25 lines) | `every 1 hour, call !TaskName` |
| **Total setup** | **Multiple packages, config files, boilerplate** | **Zero setup. Write and run.** |

---

## Section 7: The Professions Grid

### Headline
**PLang for the way you work**

### Subheadline
Whatever you build — automation, internal tools, prototypes, workflows — PLang gets you from idea to running software faster.

| Persona | One-Liner |
|---------|-----------|
| [Developer](/personas/developer) | Skip the ceremony. Ship the thing. |
| [SysAdmin](/personas/sysadmin) | Automate server health in plain English. |
| [Startup Founder](/personas/founder) | Build your MVP without hiring a dev team. |
| [Data Analyst](/personas/data-analyst) | Automate your data pipelines, not your weekends. |
| [DevOps Engineer](/personas/devops) | Deployment scripts that read like runbooks. |
| [Marketing Manager](/personas/marketing-manager) | Track campaigns without begging engineering. |
| [Sales Rep](/personas/sales-rep) | Automate follow-ups. Focus on closing. |
| [HR Manager](/personas/hr-manager) | Onboarding workflows that run themselves. |
| [Accountant](/personas/accountant) | Invoice processing without the spreadsheet juggle. |
| [Real Estate Agent](/personas/real-estate) | Listings and follow-ups on autopilot. |
| [Teacher](/personas/teacher) | Grade smarter. Communicate faster. |
| [Freelancer](/personas/freelancer) | Invoicing, tracking, and client management in one place. |
| [Small Business Owner](/personas/small-business) | Run your operations without an IT department. |
| [Customer Support Lead](/personas/customer-support) | Route tickets and respond faster. |
| [Project Manager](/personas/project-manager) | Status reports that write themselves. |
| [Lawyer](/personas/lawyer) | Automate documents and never miss a deadline. |
| [Content Creator](/personas/content-creator) | Publish everywhere from one workflow. |
| [Clinic Admin](/personas/clinic-admin) | Appointments and reminders, handled. |

---

## Section 8: The Action — Get Started

### Headline
**From zero to running software in 60 seconds**

### Steps

**1. Install PLang**
Download from [GitHub releases](https://github.com/PLangHQ/plang/releases), unzip, and add to your PATH.

**2. Create your first app**
```bash
mkdir MyFirstApp && cd MyFirstApp
```

Create `Start.goal`:
```plang
Start
- write out 'Hello PLang world'
```

**3. Build and run**
```bash
plang exec
```

**4. You just built your first app.**
The LLM compiled your English into executable instructions. From here, add database operations, HTTP calls, email, scheduling — all in the same natural language.

[Get Started Guide →](/get-started)

---

## Footer Note
PLang is open source and in active development. Best suited today for internal tools, automation, prototypes, and side projects. The ecosystem is young — and that means you can shape it.
