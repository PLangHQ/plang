# PLang for Developers

---

## Headline
**Skip the ceremony. Ship the thing.**

---

## The Daily Grind

You know the drill. New project, new idea — and then 3 hours disappear into setup. `npm init`. Install Express. Install the ORM. Configure the database. Set up auth middleware. Write the migration. Create the user model. Add bcrypt. Add JWT. Add session handling. Add CORS. Add error handling middleware. Now create `app.js`. Now configure dotenv. Now write the first route.

Half your code is plumbing. Half your time is fighting tools that were supposed to help.

You just wanted to build the thing.

---

## The PLang Way

```plang
Start
- start webserver

CreateUser - POST
- hash %request.password% using bcrypt, write to %hashed%
- insert into users, name=%request.name%, email=%request.email%, password=%hashed%, write to %id%
- write out {id: %id%, status: 'created'}

Login - POST
- select password from users where email=%request.email%, write to %stored%
- if %stored% is empty then
    - write out {error: 'User not found'}, status code 404
- verify %request.password% against %stored.password% using bcrypt, write to %valid%
- if %valid% is false then
    - write out {error: 'Invalid password'}, status code 401
- write out {status: 'authenticated', identity: %Identity%}

ListUsers - GET
- select id, name, email from users, write to %users%
- write out %users%
```

---

## Wait — that's the program?

Yes. Web server, database, password hashing, authentication, three API endpoints. No `package.json`. No `node_modules`. No ORM. No migration tool. No auth library. No config file.

---

## What Just Happened

- **`start webserver`** — HTTP server on port 8080. Done.
- **`insert into users`** — SQLite database created automatically. Table created on first insert. No schema file.
- **`hash using bcrypt`** — Built-in crypto module. No library to install.
- **`%Identity%`** — Ed25519 key pair generated per app. Every request is cryptographically signed. Authentication without sessions, tokens, or passwords.
- **`%request.name%`** — Request body parsed automatically. No body-parser middleware.
- **`write out {}`** — Returns JSON to the client.

PLang has 40+ built-in modules. Database, HTTP, files, crypto, email, scheduling, LLM integration, error handling with retries — all zero-config. You write what you want, and the built-in modules handle it.

---

## The Build Model — Why Developers Trust It

PLang isn't magic. Here's the mechanism:

1. You write `.goal` files (the English steps above)
2. `plang build` sends each step to an LLM once — it maps your intent to a specific module and action
3. The output is a `.pr` file — **human-readable JSON** with the exact function call, parameters, and return values
4. `plang` executes the `.pr` files directly. No LLM at runtime. Deterministic.

Open the `.build/` folder. Read the `.pr` files. Every instruction is visible. If the LLM mapped something wrong, you see it, fix the wording, rebuild. You're always in control.

Incremental builds — only changed steps recompile. The AI cost is build-time only (typically $0.002–$0.009 per step).

---

## The Escape Hatch

Need to call custom C# code? The `[code]` module lets you write inline C# for anything the built-in modules don't cover. PLang isn't a walled garden.

---

## Where It Fits

PLang isn't replacing your production backend. It's for:

- **Internal tools** — admin panels, data dashboards, CRUD apps
- **Prototypes** — validate an idea before committing a sprint
- **Automation** — scheduled jobs, file processing, API integrations
- **Side projects** — build things on weekends without the tax of setup
- **Scripts that outgrew bash** — when shell scripts get painful but a full framework is overkill

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
# Unzip, add to PATH, then:

mkdir MyApp && cd MyApp
# Create Start.goal with your steps
plang exec
```

Your first app builds and runs in 60 seconds. The LLM compiles once, your app runs forever.

[Full getting started guide →](/get-started)
