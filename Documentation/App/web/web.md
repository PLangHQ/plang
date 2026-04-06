# PLang — Storefront Architect Ground Truth

**Purpose:** This is the technical source of truth for the Storefront Architect. Every section you build must prove a claim from the Signal Maker's messaging. If you can't show it visually in 10 seconds, redesign until you can. This document tells you what's real, what to show, and what not to fake.

---

## 1. What You're Building For

A developer or builder hears "programming in plain English" and lands on the site. They are skeptical. They've seen "revolutionary" tools before. Your job is to make them think **"wait, that's actually the program?"** within 3 seconds of landing.

The single most powerful asset you have is PLang code itself — it's readable English. Don't hide it behind illustrations. Don't explain it with paragraphs. Show it. The code IS the design.

---

## 2. Technical Facts You Must Show (And How)

### Fact 1: `.goal` files are the source code
**What's true:** Programs are written as `.goal` files containing steps in plain English. These are what developers write, version control, and maintain.

**What to show:**
```plang
CreateOrder
- validate %user.id% is not empty, "User must be logged in"
- validate %cart% is not empty, "Cart cannot be empty"
- begin transaction "users/%user.id%"
- insert into orders, status='pending', amount=%cartTotal%, created=%now%, write to %orderId%
- foreach %cart% call CreateOrderItem item=%cartItem%
- call goal ProcessPayment
    on error call HandlePaymentError
- update orders set status='paid' where id=%orderId%
- end transaction
- call goal SendOrderConfirmation
```

**Design directive:** This code block should be the largest visual element in the hero. No syntax highlighting tricks needed — it reads as English. Let the whitespace and simplicity do the work. The reader should be able to understand it without any accompanying explanation.

---

### Fact 2: Build-time AI, runtime independence
**What's true:** `plang build` calls an LLM once per changed step and generates `.pr` files (JSON). `plang` runs with zero LLM calls, zero network dependency. Incremental builds — only changed steps recompile.

**What to show:**
A two-phase visual:
1. **Build phase:** `plang build` → LLM icon → `.pr` files appear (show them as JSON snippets)
2. **Run phase:** `plang` → app running → explicit "no AI, no network" indicator

**Design directive:** This is the "aha" moment. The visitor must understand that AI is the compiler, not a runtime dependency. Consider an animation: build step shows a brief LLM call, then a clear "disconnect" moment, then the app running independently. The disconnect is the emotional beat.

---

### Fact 3: 40+ built-in modules, zero configuration
**What's true:** Database (SQLite default), HTTP, files, crypto, caching, scheduling, LLM integration, UI rendering, email, code execution, and 30+ more. No package manager, no imports, no `node_modules`.

**What to show:**
A compact app doing multiple things:
```plang
Start
- start webserver

HandleRequest
- select * from products where active=true, write to %products%
- get https://api.pricing.com/rates, write to %rates%
- read config/settings.json, write to %config%
- if %Identity% is not empty
    - select * from users where identity=%Identity%, return 1, write to %user%
- [ui] render "products.html", navigate
```

**Design directive:** The point is density of capability with zero ceremony. Count the things happening: webserver, database query, HTTP call, file read, authentication check, UI render — in 8 lines with zero imports. Show a subtle counter or annotation: "6 capabilities. 0 imports. 0 config files."

---

### Fact 4: Cryptographic identity built in
**What's true:** Every app generates Ed25519 key pairs. `%Identity%` = public key. Every request auto-signed. Server validates signature. No passwords, no sessions, no tokens.

**What to show:**
Side-by-side comparison:

**PLang auth (complete):**
```plang
Events
- before each goal, call LoadUser

LoadUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user% is empty then
    - insert into users, identity=%Identity%, created=%now%, write to %user.id%
    - select * from users where id=%user.id%, return 1, write to %user%
```

**vs. Traditional auth (partial list):**
```
passport.js + bcrypt + jsonwebtoken + express-session +
cookie-parser + cors + helmet + csrf-token + ...
npm install (47 packages) + middleware config + 
session store + password hashing + token refresh + 
reset flow + ...
```

**Design directive:** The contrast must be visceral. Left side: 6 lines, complete. Right side: a fading list of dependencies and config that trails off. Don't mock any specific framework — mock the *situation*.

---

### Fact 5: SQLite works on first run
**What's true:** `- insert into users, name=%name%` works immediately. No database setup, no Docker, no connection string, no migration tool. Database lives in `.db/data.sqlite`. Per-user databases supported for GDPR.

**What to show:**
Terminal sequence:
```
$ plang build
Building... ✓

$ plang
> Order created: #1042
> User data stored
> Analytics logged

$ ls .db/
data.sqlite        (12 KB - just appeared)
```

**Design directive:** The "just appeared" moment matters. The reader should see that no database setup step exists. Consider a "what's missing" callout: "No Docker. No migration files. No connection string. No ORM. It just works."

---

### Fact 6: Transparent compilation
**What's true:** `.pr` files are human-readable JSON stored in `.build/`. Every step's compiled output is inspectable. No black box.

**What to show:**
A `.goal` step and its corresponding `.pr` file:

Step: `- insert into orders, status='pending', amount=%cartTotal%, write to %orderId%`

Compiled `.pr`:
```json
{
  "Action": {
    "FunctionName": "InsertAndSelectIdOfInsertedRow",
    "Parameters": [{
      "Type": "System.String",
      "Name": "sql",
      "Value": "INSERT INTO orders (status, amount) VALUES (@status, @amount)"
    }]
  }
}
```

**Design directive:** This builds developer trust. They can see exactly what the LLM decided to do. Consider making this an expandable/collapsible element — click a step, see its `.pr` output. Reinforces "nothing hidden."

---

## 3. The Complete Example Library

These are real, valid PLang examples. Use them exactly as written — they follow all PLang constraints (loops call goals, error handlers call goals, variables use `%name%` syntax).

### Minimal: Hello World
```plang
Start
- write out "Hello World"
```

### Simple: Todo API
```plang
Start
- start webserver
- add route /todos, GET, call ListTodos
- add route /todos, POST, call CreateTodo
- add route /todos/%todo.id%, DELETE, call DeleteTodo

ListTodos
- select * from todos where identity=%Identity%, write to %todos%
- write out %todos%

CreateTodo
- insert into todos, title=%request.body.title%, identity=%Identity%, created=%now%

DeleteTodo
- delete from todos where id=%todo.id% and identity=%Identity%
```

### Medium: E-commerce order flow
```plang
CreateOrder
- validate %user.id% is not empty, "User must be logged in"
- validate %cart% is not empty, "Cart cannot be empty"
- begin transaction "users/%user.id%"
- insert into orders, status='pending', amount=%cartTotal%, created=%now%, write to %orderId%
- foreach %cart% call CreateOrderItem item=%cartItem%
- call goal ProcessPayment
    on error call HandlePaymentError
- update orders set status='paid' where id=%orderId%
- end transaction
- call goal SendOrderConfirmation
- [ui] render "order_success.html", navigate

CreateOrderItem
- insert into orderItems, orderId=%orderId%, productId=%cartItem.productId%, quantity=%cartItem.quantity%, price=%cartItem.price%
```

### Full-stack: Web app with auth and routing
```plang
Start
- start webserver
- add route /product/%product.id%, call ShowProduct
- add route /product/%product.id%(number > 0), POST, call SaveProduct

Events
- before each goal, call LoadUser
- before each goal in /admin/.*, call CheckAdmin

LoadUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user% is empty then
    - insert into users, identity=%Identity%, created=%now%, write to %user.id%
    - select * from users where id=%user.id%, return 1, write to %user%

CheckAdmin
- if %user.role% does not contain "admin", throw 403 "Forbidden"

ShowProduct
- select * from products where id=%product.id%, return 1, write to %product%
- [ui] render "product.html", navigate
```

### GDPR: Per-user data isolation
```plang
SetupUserData
- create datasource "users/%user.id%"
- create table documents, columns: title(string, not null), content(string), created(datetime, default now)

SaveDocument
- insert into documents, title=%title%, content=%content%, datasource: "users/%user.id%"

DeleteAllUserData
/ GDPR "right to be forgotten" - one line
- delete file ".db/users/%user.id%.sqlite"
- delete from users where id=%user.id%
```

---

## 4. PLang Constraints You Must Not Violate in Examples

These are real language rules. Showing code that breaks them destroys credibility with developers who try it.

| Rule | Example of violation | Correct form |
|---|---|---|
| Loops must call goals | `- foreach %items%` with indented substeps | `- foreach %items% call ProcessItem` |
| Error handlers must call goals | `- get url, on error write out "failed"` | `- get url, on error call HandleError` |
| Variables use `%name%` syntax | `userId` or `${userId}` | `%userId%` |
| Simple math uses NCalc, not [code] | `- [code] calculate %a% + %b%` | `- set %total% = %a% + %b%` |
| Start.goal is the default entry point | Naming the entry point `Main.goal` | `Start.goal` |
| Setup.goal runs once | Using Setup.goal for repeated logic | Setup for init, Start for runtime |
| No password authentication | Implementing login/password flow | Use `%Identity%` |

---

## 5. Visual Design Constraints

### What the code looks like
PLang code is monospace, minimal, and readable. It doesn't need syntax highlighting to be understood — it's English. If you highlight, keep it subtle: goal names in one weight, steps in another. Don't rainbow-highlight like traditional code.

### What the site must NOT look like
- No AI-themed illustrations (robot icons, neural network graphics, brain imagery)
- No generic SaaS landing page template (gradient hero, three-column features, testimonial carousel)
- No "enterprise" design language (shield icons, checkmark grids, pricing tables with a "Contact Sales" tier)
- No screenshots of IDEs or terminals unless showing real PLang output

### What the site SHOULD feel like
- **Developer tool confidence:** Think Vercel, Supabase, Raycast — clean, fast, code-forward
- **The code is the hero:** Largest visual elements should be PLang examples, not illustrations
- **Trust through transparency:** Show `.pr` files, show the build process, show real output
- **Speed:** Minimal JS, fast paint, no layout shift. Developers judge tools by their websites

---

## 6. Page Section Requirements

Every section must pass this test: **"Does this section prove a claim, or is it just filling space?"**

### Hero (above the fold)
**Must contain:** Headline + one PLang code example + one call to action
**Must prove:** "This is a real programming language where code reads as English"
**Time budget:** 3 seconds to comprehension

### The Split (build vs. runtime)
**Must contain:** Visual showing build-time AI → runtime independence
**Must prove:** "AI is the compiler, not a dependency"
**Time budget:** 5 seconds to understand the model

### The Proof (real app, few lines)
**Must contain:** Complete working example doing multiple things (database + HTTP + auth + UI)
**Must prove:** "This isn't a toy — it's a real tool with real capabilities"
**Time budget:** 10 seconds to scan and count capabilities

### The Trust (inspect the output)
**Must contain:** `.pr` file viewer showing compiled JSON
**Must prove:** "Nothing is hidden. You can verify what the LLM compiled."
**Time budget:** Click to expand, 5 seconds to understand

### The Comparison (PLang vs. the situation)
**Must contain:** Side-by-side: PLang lines vs. traditional setup ceremony
**Must prove:** "The ceremony is gone. You just build."
**Time budget:** 10 seconds for the contrast to land

### The Security Story
**Must contain:** `%Identity%` explanation + OWASP coverage
**Must prove:** "Security is architectural, not bolted on"
**Time budget:** 15 seconds for developers who care about this

### The Action (get started)
**Must contain:** Install command + first build + first run
**Must prove:** "You can go from zero to running in 60 seconds"
**Time budget:** Should feel like 3 steps or fewer

---

## 7. What Not To Build

| Don't build | Why |
|---|---|
| Interactive playground in browser | PLang requires build-time LLM — can't run in browser sandbox without backend infrastructure |
| Package/module marketplace | Ecosystem is young — showing an empty marketplace hurts more than showing built-in modules |
| Pricing page | PLang is open source. Build-time LLM costs are the user's API key. This isn't a SaaS |
| Community forum on-site | Use GitHub Discussions or Discord — don't split the community |
| Comparison table vs. specific languages | Positions PLang as a competitor to Python/JS, which is the wrong frame. Compare against the *situation* |
| Feature grid with checkmarks | Generic and untrusted. Show working code instead |

---

## 8. Responsive Considerations

PLang code blocks are the primary content. On mobile:
- Code blocks must remain readable — use horizontal scroll, not line wrapping that breaks readability
- The hero code example may need to be a shorter example (Hello World or Todo) on mobile, with the fuller example below
- The build/runtime split visual needs to work vertically on mobile
- Side-by-side comparisons stack vertically — PLang on top (the winner), traditional below

---

## 9. The Test

Before shipping any page, apply this checklist:

- [ ] Can a non-programmer read the hero and understand what PLang does?
- [ ] Can a developer read the hero and believe it's real?
- [ ] Does every section prove something, or is it just filler?
- [ ] Are all PLang examples valid (no constraint violations from Section 4)?
- [ ] Is the build-time/runtime split clearly communicated?
- [ ] Can the visitor get from landing to "install" in under 60 seconds of scrolling?
- [ ] Does the page load in under 2 seconds?
- [ ] Does every claim have a visible proof point within one scroll?