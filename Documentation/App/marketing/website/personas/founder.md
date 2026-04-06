# PLang for Startup Founders

---

## Headline
**Build your MVP without hiring a dev team.**

---

## The Daily Grind

You have the idea. You've talked to customers. You know exactly what the product should do — collect signups, process orders, send notifications, track usage. The logic is clear in your head.

But between you and a working prototype is a wall of technology decisions you never asked for. Which framework? Which database? Which hosting? Which auth provider? You're comparing React vs. Vue, Postgres vs. MongoDB, AWS vs. Vercel — and you haven't written a single line of business logic yet.

You've tried no-code tools, but they hit a wall the moment you need something custom. You've tried hiring a freelance developer, but translating your vision through someone else costs weeks and thousands of dollars.

---

## The PLang Way

```plang
Start
- start webserver

SignUp - POST
- if %request.email% is empty then
    - write out {error: 'Email required'}, status code 400
- insert into users, name=%request.name%, email=%request.email%, plan='free', write to %id%
- send email to %request.email%, subject: "Welcome to MyApp", body: "Hi %request.name%, you're in! Start exploring at https://myapp.com/dashboard"
- write out {id: %id%, status: 'created'}

Dashboard - GET
- select * from users where id=%request.userId%, write to %user%
- select count(*) as total from orders where userId=%user.id%, write to %orderCount%
- write out {user: %user%, orders: %orderCount.total%}

CreateOrder - POST
- insert into orders, userId=%Identity%, product=%request.product%, amount=%request.amount%, status='pending', write to %orderId%
- write out {orderId: %orderId%, status: 'pending'}
```

---

## Wait — that's the program?

That's your MVP backend. User registration, email onboarding, a dashboard endpoint, and order creation. Database included. Authentication included. Web server included. No framework selection. No hosting configuration. No developer required.

---

## What Just Happened

- **`start webserver`** — Your app is live on port 8080. That's it.
- **`insert into users`** — Database created automatically. No Postgres setup, no Docker, no migration tool. SQLite built in.
- **`send email`** — Transactional email sent directly. No SendGrid SDK, no API key configuration.
- **`%Identity%`** — Every user gets a cryptographic identity automatically. Ed25519 key pairs. No passwords to store, no sessions to manage, no auth library to integrate.
- **`%request.name%`** — HTTP request bodies parsed automatically.
- **`write out {}`** — Returns JSON responses to your frontend or API client.

The entire thing builds and runs with one command: `plang exec`.

---

## The Cost Model

| Phase | Cost |
|-------|------|
| **Build** | LLM compiles your English into executable instructions. Typically $0.002–$0.009 per step. A 50-step app costs about $0.10–$0.45 to build. |
| **Rebuild** | Incremental. Only changed steps recompile. |
| **Run** | Zero. No AI at runtime. No API calls. No ongoing cost. |
| **Database** | SQLite. Free. Built in. |
| **Auth** | Built in. Free. |

Compare: a freelance developer for an MVP starts at $5,000. A no-code tool subscription is $30–$100/month with feature limits.

---

## The Build-Time / Runtime Split

PLang uses AI at **build time** to understand your English and compile it into executable instructions (`.pr` files — human-readable JSON). At **runtime**, no AI is involved. Your app runs independently, deterministically, with no ongoing API costs.

This means: you build your MVP with $5 in LLM credits. Then it runs forever.

---

## What You Can Build

- **Landing page + waitlist** — collect signups, send welcome emails
- **SaaS backend** — user management, subscriptions, dashboards
- **Marketplace** — listings, orders, notifications
- **API** — endpoints for your mobile app or frontend
- **Internal tools** — admin panels, reporting dashboards
- **Automation** — scheduled tasks, data processing, integrations

---

## Honest About Maturity

PLang is young. It's best suited today for MVPs, internal tools, prototypes, and side projects. It has 40+ built-in modules and a growing community — but it's not yet the right choice for enterprise production at scale.

What it is: the fastest path from your idea to a working, runnable prototype. No setup tax. No developer dependency. No framework decisions.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir MyMVP && cd MyMVP
# Create Start.goal with your product logic
plang exec
```

Write what your product does. Build it. Show it to customers.

[Full getting started guide →](/get-started)
