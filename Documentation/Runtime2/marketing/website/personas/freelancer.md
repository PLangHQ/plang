# PLang for Freelancers

---

## Headline
**Invoicing, tracking, and client management in one place.**

---

## The Daily Grind

You're a one-person shop juggling five clients, three project deadlines, and an invoicing system that's really just a Google Sheet you copy every month. Time tracking lives in your head. Client communication is scattered across email, Slack, and text messages. And sending invoices means opening a template, changing the date and amount, exporting to PDF, and emailing it — every single time.

You don't need project management software. You need YOUR workflow, automated.

---

## The PLang Way

```plang
Start
- start webserver
- every 1st of month at 9am, call !MonthlyInvoicing

LogTime - POST
- insert into timeEntries, client=%request.client%, project=%request.project%, hours=%request.hours%, description=%request.description%, date=%Now%, write to %id%
- write out {entryId: %id%, status: 'logged'}

MonthlyInvoicing
- select client, sum(hours) as totalHours from timeEntries where date >= %Now-30days% and invoiced=false group by client, write to %billable%
- foreach %billable%, call !GenerateInvoice item=%clientHours%

GenerateInvoice
- select rate from clients where name=%clientHours.client%, write to %clientInfo%
- set %total% = %clientHours.totalHours% * %clientInfo.rate%
- insert into invoices, client=%clientHours.client%, hours=%clientHours.totalHours%, rate=%clientInfo.rate%, total=%total%, date=%Now%, status='sent', write to %invoiceId%
- update timeEntries set invoiced=true where client=%clientHours.client% and date >= %Now-30days%
- select email from clients where name=%clientHours.client%, write to %clientContact%
- send email to %clientContact.email%, subject: "Invoice #%invoiceId% - %clientHours.client%", body: "Hours: %clientHours.totalHours%\nRate: $%clientInfo.rate%/hr\nTotal: $%total%\n\nPayment due within 30 days. Thank you!"
- write out 'Invoiced %clientHours.client%: $%total%'

AddClient - POST
- insert into clients, name=%request.name%, email=%request.email%, rate=%request.rate%
- write out {status: 'client added'}

Dashboard - GET
- select client, sum(hours) as hours from timeEntries where invoiced=false group by client, write to %unbilled%
- select status, count(*) as count, sum(total) as amount from invoices group by status, write to %invoiceStats%
- write out {unbilled: %unbilled%, invoices: %invoiceStats%}
```

---

## Wait — that's the program?

That's your freelance business system. Time tracking, automatic monthly invoicing, client management, and a dashboard — all in plain English. Invoices calculated and sent automatically on the 1st of every month.

---

## What Just Happened

- **`insert into timeEntries`** — Time tracking database created automatically.
- **`every 1st of month at 9am`** — Monthly invoicing runs automatically. No reminders, no manual process.
- **`sum(hours) * rate`** — Invoice totals calculated from your logged time.
- **`send email`** — Professional invoices emailed to each client automatically.
- **`Dashboard - GET`** — See your unbilled hours and invoice status at a glance.

Change your rate for a client? Update one row. Add a late payment reminder? Add a scheduled check. You're in control.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your invoicing system runs reliably with zero ongoing cost. Build once, your freelance admin runs itself.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir FreelanceTools && cd FreelanceTools
# Create Start.goal with your business workflow
plang exec
```

Write your freelance operations in plain English. Spend time on client work, not admin.

[Full getting started guide →](/get-started)
