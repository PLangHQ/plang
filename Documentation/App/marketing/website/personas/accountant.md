# PLang for Accountants

---

## Headline
**Invoice processing without the spreadsheet juggle.**

---

## The Daily Grind

Invoices arrive by email, by upload, by carrier pigeon. You enter them into one spreadsheet, reconcile against bank statements in another, chase overdue payments manually, and generate monthly reports by copying data between tabs until your eyes cross.

You've looked at accounting software that promises automation. It costs $50/month, doesn't match your workflow, and still needs manual data entry for half the cases.

You don't need a product. You need your exact process, automated.

---

## The PLang Way

```plang
Start
- start webserver
- every day at 9am, call !CheckOverdue

SubmitInvoice - POST
- insert into invoices, vendor=%request.vendor%, amount=%request.amount%, dueDate=%request.dueDate%, status='pending', submitted=%Now%, write to %id%
- write out {invoiceId: %id%, status: 'recorded'}

CheckOverdue
- select * from invoices where dueDate < %Now% and status='pending', write to %overdue%
- foreach %overdue%, call !SendReminder item=%invoice%
- write out 'Checked %overdue.count% overdue invoices'

SendReminder
- send email to %invoice.vendor%, subject: "Payment Overdue: Invoice #%invoice.id%", body: "Invoice #%invoice.id% for $%invoice.amount% was due on %invoice.dueDate%. Please process at your earliest convenience."
- update invoices set status='reminded', lastReminder=%Now% where id=%invoice.id%

MarkPaid - POST
- update invoices set status='paid', paidDate=%Now% where id=%request.invoiceId%
- write out {status: 'marked paid'}

MonthlyReport
- select status, count(*) as count, sum(amount) as total from invoices where submitted >= %Now-30days% group by status, write to %summary%
- select vendor, sum(amount) as total from invoices where status='paid' and paidDate >= %Now-30days% group by vendor order by total desc, write to %byVendor%
- send email to finance@company.com, subject: "Monthly Invoice Report - %Now%", body: "Summary:\n%summary%\n\nBy Vendor:\n%byVendor%"
```

---

## Wait — that's the program?

That's your invoice tracking system. Submit invoices, automatic overdue reminders, mark payments, monthly reports — all in plain English. No spreadsheet. No accounting software subscription.

---

## What Just Happened

- **`insert into invoices`** — Database created automatically. Every invoice tracked and queryable.
- **`every day at 9am`** — Overdue checks run daily. No manual calendar reminders.
- **`send email`** — Payment reminders sent automatically. Professional, consistent, on time.
- **`sum(amount)`, `group by`** — Standard SQL for totals and breakdowns. Your data is structured and reportable from day one.
- **`SubmitInvoice - POST`** — An API endpoint. Connect a simple web form and stop manual data entry.

Need to change the overdue threshold from "past due" to "5 days past due"? Change one line. Need a quarterly report? Copy the monthly one and adjust the date range.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your invoice system runs reliably with zero ongoing cost. Build once, your financial workflows run forever.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir InvoiceTracker && cd InvoiceTracker
# Create Start.goal with your invoice workflow
plang exec
```

Write your financial processes in plain English. Build once. Stop juggling spreadsheets.

[Full getting started guide →](/get-started)
