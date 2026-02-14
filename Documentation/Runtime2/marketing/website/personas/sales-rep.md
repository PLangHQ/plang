# PLang for Sales Reps

---

## Headline
**Automate follow-ups. Focus on closing.**

---

## The Daily Grind

Your CRM has 200 contacts that need follow-ups. You spend the first hour of every day scrolling through leads, checking who needs a touch, copy-pasting email templates, and logging activities. By the time you're done with admin, half your selling time is gone.

You've asked for better CRM automation. IT says it's "on the roadmap." Your manager says to "use the existing tools better." Meanwhile, leads go cold because nobody followed up on day 3.

---

## The PLang Way

```plang
Start
- every day at 7am, call !MorningFollowUps
- every friday at 5pm, call !WeeklyPipeline

MorningFollowUps
- select * from leads where nextFollowUp <= %Now% and status='active', write to %dueLeads%
- foreach %dueLeads%, call !FollowUp item=%lead%
- write out 'Processed %dueLeads.count% follow-ups'

FollowUp
- set %daysSinceContact% = %Now% - %lead.lastContact%
- send email to %lead.email%, subject: "Checking in, %lead.name%", body: "Hi %lead.name%, wanted to follow up on our conversation about %lead.interest%. Any questions I can help with?"
- update leads set lastContact=%Now%, nextFollowUp=%Now+3days%, touchCount=%lead.touchCount%+1 where id=%lead.id%
- insert into activity_log, leadId=%lead.id%, type='email', date=%Now%, notes='Auto follow-up sent'

WeeklyPipeline
- select status, count(*) as count, sum(dealValue) as value from leads group by status, write to %pipeline%
- select name, dealValue, status from leads where status='proposal' order by dealValue desc, write to %hotDeals%
- send email to me@company.com, subject: "Weekly Pipeline - %Now%", body: "Pipeline Summary:\n%pipeline%\n\nHot Deals:\n%hotDeals%"

AddLead - POST
- insert into leads, name=%request.name%, email=%request.email%, interest=%request.interest%, dealValue=%request.value%, status='new', nextFollowUp=%Now+1day%, lastContact=%Now%, touchCount=0
- write out {status: 'added'}
```

---

## Wait — that's the program?

That's your personal sales assistant. Automatic follow-ups on schedule, activity logging, weekly pipeline reports, and a way to add new leads — all in plain English you can adjust yourself.

---

## What Just Happened

- **`every day at 7am`** — Your follow-ups run before you pour your coffee.
- **`select from leads where nextFollowUp <= %Now%`** — Finds every lead due for contact today. Database included automatically.
- **`send email`** — Personalized follow-ups sent directly. No template tool needed.
- **`update leads set nextFollowUp=%Now+3days%`** — Automatically schedules the next touch.
- **`insert into activity_log`** — Every interaction logged. Your manager sees activity without you entering it.
- **`WeeklyPipeline`** — Pipeline report delivered to your inbox every Friday.

Change the follow-up interval from 3 days to 5? Edit one line. Change the email template? Edit the text. No IT ticket.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your follow-up system runs reliably every morning with no ongoing cost. Build once, your sales automation runs forever.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir SalesAutomation && cd SalesAutomation
# Create Start.goal with your follow-up logic
plang exec
```

Write your sales workflow. Build it. Let it do the admin while you close deals.

[Full getting started guide →](/get-started)
