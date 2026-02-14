# PLang for Real Estate Agents

---

## Headline
**Listings and follow-ups on autopilot.**

---

## The Daily Grind

You've got 15 active listings, 40 prospects, and a phone that never stops buzzing. Between showing properties, you're manually updating listing descriptions across platforms, sending follow-up emails to prospects who showed interest three days ago, and trying to remember who wanted a 3-bedroom with a garage under $400k.

Your CRM has a follow-up feature that's too clunky to use on the go. Your listing spreadsheet is always out of date. And that prospect who was "very interested" last Tuesday? You forgot to call them back.

---

## The PLang Way

```plang
Start
- start webserver
- every day at 8am, call !DailyFollowUps

AddProspect - POST
- insert into prospects, name=%request.name%, email=%request.email%, phone=%request.phone%, budget=%request.budget%, bedrooms=%request.bedrooms%, notes=%request.notes%, status='active', nextFollowUp=%Now+2days%, write to %id%
- write out {prospectId: %id%, status: 'added'}

DailyFollowUps
- select * from prospects where nextFollowUp <= %Now% and status='active', write to %due%
- foreach %due%, call !FollowUpProspect item=%prospect%

FollowUpProspect
- select * from listings where price <= %prospect.budget% and bedrooms >= %prospect.bedrooms% and status='available', write to %matches%
- if %matches.count% > 0 then call !SendMatchEmail
- update prospects set nextFollowUp=%Now+5days%, lastContact=%Now% where id=%prospect.id%

SendMatchEmail
- send email to %prospect.email%, subject: "New listings for you, %prospect.name%", body: "Hi %prospect.name%, I found %matches.count% properties matching your criteria. Let's schedule a viewing — reply to this email or call me."

AddListing - POST
- insert into listings, address=%request.address%, price=%request.price%, bedrooms=%request.bedrooms%, description=%request.description%, status='available', write to %id%
- write out {listingId: %id%, status: 'listed'}

WeeklyReport
- select status, count(*) as count from prospects group by status, write to %prospectStats%
- select status, count(*) as count from listings group by status, write to %listingStats%
- send email to me@realestate.com, subject: "Weekly Report", body: "Prospects:\n%prospectStats%\n\nListings:\n%listingStats%"
```

---

## Wait — that's the program?

That's your prospect management and listing system. Automatic follow-ups, property matching, email notifications, and weekly reports. Read it — it describes exactly what it does.

---

## What Just Happened

- **`insert into prospects`** / **`insert into listings`** — Database created automatically. All your prospects and listings tracked and queryable.
- **`every day at 8am`** — Follow-ups run before your first showing. No manual reminders.
- **`select from listings where price <= %prospect.budget%`** — Automatically matches prospects to listings based on their criteria.
- **`send email`** — Personalized emails sent to prospects with matching properties.
- **API endpoints** — Connect a web form for prospect intake and listing management.

Change the follow-up interval? Edit one line. Add a "square footage" filter? Add it to the query. You control the process.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your lead nurturing runs reliably every morning with zero ongoing cost.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir RealEstateTools && cd RealEstateTools
# Create Start.goal with your prospect workflow
plang exec
```

Write your real estate workflows in plain English. Never forget a follow-up again.

[Full getting started guide →](/get-started)
