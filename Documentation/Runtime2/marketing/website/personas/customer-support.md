# PLang for Customer Support Leads

---

## Headline
**Route tickets and respond faster.**

---

## The Daily Grind

Tickets come in through email, through the website, through the app. You copy them into your ticketing system manually. Then you triage — is this billing? Technical? General? — and assign them to the right person. Meanwhile, three customers are waiting on simple questions that have the same answer you've typed 200 times this month.

Your team spends more time on ticket logistics than on actually helping people.

---

## The PLang Way

```plang
Start
- start webserver
- every 30 minutes, call !CheckSLAs

SubmitTicket - POST
- insert into tickets, customer=%request.email%, subject=%request.subject%, body=%request.body%, status='open', priority='normal', created=%Now%, write to %id%
- call !ClassifyTicket
- send email to %request.email%, subject: "Ticket #%id% Received", body: "We've received your request: '%request.subject%'. We'll get back to you shortly."
- write out {ticketId: %id%, status: 'received'}

ClassifyTicket
- if %request.subject% contains 'billing' or %request.subject% contains 'invoice' or %request.subject% contains 'charge' then
    - update tickets set category='billing', assignee='billing@company.com' where id=%id%
- if %request.subject% contains 'bug' or %request.subject% contains 'error' or %request.subject% contains 'broken' then
    - update tickets set category='technical', assignee='engineering@company.com' where id=%id%

CheckSLAs
- select * from tickets where status='open' and created < %Now-4hours%, write to %breaching%
- foreach %breaching%, call !EscalateTicket item=%ticket%

EscalateTicket
- update tickets set priority='high' where id=%ticket.id%
- send email to support-lead@company.com, subject: "SLA Breach: Ticket #%ticket.id%", body: "Ticket '%ticket.subject%' from %ticket.customer% has been open for over 4 hours."

Dashboard - GET
- select status, count(*) as count from tickets group by status, write to %byStatus%
- select category, count(*) as count from tickets where status='open' group by category, write to %byCategory%
- select priority, count(*) as count from tickets where status='open' group by priority, write to %byPriority%
- write out {byStatus: %byStatus%, byCategory: %byCategory%, byPriority: %byPriority%}
```

---

## Wait — that's the program?

That's your ticket routing system. Automatic intake, keyword-based classification, assignment, customer confirmation, SLA monitoring, escalation, and a dashboard — in plain English.

---

## What Just Happened

- **`insert into tickets`** — Ticket database created automatically. Every ticket tracked and queryable.
- **`ClassifyTicket`** — Keyword-based routing. "Billing" tickets go to billing, "bug" tickets go to engineering.
- **`send email`** — Instant customer acknowledgment. Professional, consistent.
- **`every 30 minutes`** — SLA monitoring runs continuously. Tickets breaching the 4-hour window get escalated automatically.
- **`Dashboard - GET`** — Real-time view of your queue by status, category, and priority.

Want to add an "urgent" keyword that sets high priority immediately? Add two lines. Want to change the SLA window from 4 hours to 2? Change one number.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your ticket system runs reliably with zero ongoing cost. Build once, your support automation runs forever.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir SupportTools && cd SupportTools
# Create Start.goal with your ticket workflow
plang exec
```

Write your support process in plain English. Spend time helping customers, not managing tickets.

[Full getting started guide →](/get-started)
