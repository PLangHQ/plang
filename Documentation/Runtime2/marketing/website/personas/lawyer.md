# PLang for Lawyers

---

## Headline
**Automate documents and never miss a deadline.**

---

## The Daily Grind

Your practice runs on deadlines — filing dates, response windows, statute of limitations. Missing one isn't just embarrassing, it's malpractice. So you track them manually: calendar entries, sticky notes, a spreadsheet you check every morning.

Document assembly is another time sink. You open a template, find-and-replace the client name, the case number, the dates. Then you email it to the client for review. Then you do it again for the next case.

You bill $300/hour, and you're spending that time on copy-paste.

---

## The PLang Way

```plang
Start
- start webserver
- every day at 7am, call !CheckDeadlines

NewCase - POST
- insert into cases, clientName=%request.clientName%, clientEmail=%request.clientEmail%, caseType=%request.caseType%, filingDate=%request.filingDate%, status='active', opened=%Now%, write to %caseId%
- call !SetDeadlines
- write out {caseId: %caseId%, status: 'created'}

SetDeadlines
- insert into deadlines, caseId=%caseId%, type='response', dueDate=%request.filingDate-5days%, status='pending'
- insert into deadlines, caseId=%caseId%, type='filing', dueDate=%request.filingDate%, status='pending'
- insert into deadlines, caseId=%caseId%, type='review', dueDate=%request.filingDate-14days%, status='pending'

CheckDeadlines
- select d.type, d.dueDate, c.clientName, c.caseType from deadlines d join cases c on d.caseId=c.id where d.dueDate <= %Now+3days% and d.status='pending', write to %upcoming%
- foreach %upcoming%, call !AlertDeadline item=%deadline%

AlertDeadline
- send email to attorney@firm.com, subject: "Deadline in %deadline.dueDate - Now% days: %deadline.type%", body: "Case: %deadline.clientName% (%deadline.caseType%)\nDeadline: %deadline.type%\nDue: %deadline.dueDate%\n\nDo not miss this."

GenerateDocument - POST
- read templates/%request.template%.txt into %template%
- set %document% = %template%
- save %document% to documents/%request.caseId%_%request.template%_%Now%.txt
- send email to %request.clientEmail%, subject: "Document for Review: %request.template%", body: "Please review the attached document for your case."
- write out {status: 'document generated and sent'}
```

---

## Wait — that's the program?

That's your case management system. New case intake, automatic deadline tracking, daily alerts, and document generation with client delivery. All in plain English.

---

## What Just Happened

- **`insert into cases`** / **`insert into deadlines`** — Case database and deadline tracker created automatically.
- **`every day at 7am`** — Deadline checks run before you reach the office. Alerts for anything due within 3 days.
- **`send email to attorney@firm.com`** — Deadline reminders delivered to your inbox. No more sticky notes.
- **`read templates/...`** — Document templates are text files you control. Update them anytime.
- **`NewCase - POST`** — API endpoint. Connect a web form for intake.

Change the alert window from 3 days to 7? Change one number. Add a "discovery" deadline type? Add one line. Your practice management adapts to how you work.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your deadline alerts run reliably every morning with zero ongoing cost. Build once, your practice management runs itself.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir LegalTools && cd LegalTools
# Create Start.goal with your practice workflow
plang exec
```

Write your legal workflows in plain English. Focus on practicing law, not managing systems.

[Full getting started guide →](/get-started)
