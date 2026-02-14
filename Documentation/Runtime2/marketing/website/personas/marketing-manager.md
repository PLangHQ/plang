# PLang for Marketing Managers

---

## Headline
**Track campaigns and nurture leads without begging engineering.**

---

## The Daily Grind

You're managing campaigns across three platforms, tracking leads in a spreadsheet that's become a monster, and manually sending follow-up emails because "the CRM doesn't do that." Every week you need a report that takes 2 hours of copy-paste from different dashboards.

You've asked engineering to build an internal tool. It's been on the backlog for four months.

You know exactly what you need — you just can't build it yourself. Or couldn't.

---

## The PLang Way

```plang
Start
- every day at 9am, call !DailyLeadNurture
- every friday at 4pm, call !WeeklyReport

DailyLeadNurture
- select * from leads where status='new' and created < %Now-2days%, write to %staleLeads%
- foreach %staleLeads%, call !SendFollowUp item=%lead%

SendFollowUp
- send email to %lead.email%, subject: "Still interested, %lead.name%?", body: "Hi %lead.name%, I noticed you signed up recently. Want to schedule a quick call? Reply to this email and I'll set it up."
- update leads set status='followed_up', lastContact=%Now% where id=%lead.id%
- write out 'Followed up with %lead.name%'

WeeklyReport
- select status, count(*) as total from leads group by status, write to %pipeline%
- select source, count(*) as leads from leads group by source, write to %sources%
- send email to marketing@company.com, subject: "Weekly Lead Report - %Now%", body: "Pipeline:\n%pipeline%\n\nLead Sources:\n%sources%"

AddLead - POST
- insert into leads, name=%request.name%, email=%request.email%, source=%request.source%, status='new', created=%Now%
- write out {status: 'added'}
```

---

## Wait — that's the program?

That's your lead nurturing system. Automatic follow-ups on stale leads, weekly pipeline reports, lead tracking by source, and an API endpoint to receive new leads from your website — all in one file you can read and understand.

---

## What Just Happened

- **`every day at 9am`** — Scheduled tasks. No cron, no third-party scheduler.
- **`select from leads where status='new'`** — Database queries in plain SQL. The database is created automatically.
- **`foreach %staleLeads%, call !SendFollowUp`** — Loop through results and take action on each.
- **`send email`** — Direct SMTP email. No email marketing platform needed for simple follow-ups.
- **`AddLead - POST`** — An API endpoint your website form can submit to.

No engineering ticket required. No four-month wait. Write what you need, run it.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your automations run reliably on schedule with zero ongoing cost. Build once for a few cents, run forever.

---

## More Marketing Automations

**Campaign performance tracking:**
```plang
TrackCampaign
- get https://api.adplatform.com/campaigns/%campaignId%/stats, write to %stats%
- insert into campaign_metrics, campaign=%campaignId%, date=%Now%, clicks=%stats.clicks%, conversions=%stats.conversions%, spend=%stats.spend%
- if %stats.cpa% > 50 then
    - send email to marketing@company.com, subject: "High CPA Alert", body: "Campaign %campaignId% CPA is $%stats.cpa%. Budget review needed."
```

**Content calendar reminders:**
```plang
Start
- every day at 8am, call !CheckDeadlines

CheckDeadlines
- select * from content_calendar where dueDate=%Tomorrow% and status='draft', write to %dueSoon%
- foreach %dueSoon%, call !RemindAuthor item=%item%

RemindAuthor
- send email to %item.author%, subject: "Content Due Tomorrow", body: "Reminder: '%item.title%' is due tomorrow."
```

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir MarketingTools && cd MarketingTools
# Create Start.goal with your automations
plang exec
```

Write your marketing workflows in plain English. Build once. Let them run.

[Full getting started guide →](/get-started)
