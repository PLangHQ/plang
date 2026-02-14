# PLang for HR Managers

---

## Headline
**Onboarding workflows that run themselves.**

---

## The Daily Grind

New hire starting Monday. That means: create their accounts, send the welcome packet, assign the training modules, notify their manager, schedule the 30-day check-in, add them to the right email lists, generate their offer letter, and update the headcount spreadsheet. You do this manually. Every. Single. Time.

Half your week is repetitive process management that should happen automatically. You've looked at HR software — it's either too expensive, too rigid, or requires IT to set up.

---

## The PLang Way

```plang
Start
- start webserver

NewHire - POST
- insert into employees, name=%request.name%, email=%request.email%, department=%request.department%, startDate=%request.startDate%, manager=%request.manager%, write to %id%
- call !SendWelcomePacket
- call !NotifyManager
- call !ScheduleCheckIns
- write out {employeeId: %id%, status: 'onboarding started'}

SendWelcomePacket
- read templates/welcome_email.txt into %template%
- send email to %request.email%, subject: "Welcome to the Team, %request.name%!", body: "%template%"

NotifyManager
- send email to %request.manager%, subject: "New Hire Starting: %request.name%", body: "%request.name% joins your team on %request.startDate%. Department: %request.department%."

ScheduleCheckIns
- insert into checkins, employeeId=%id%, type='30-day', dueDate=%request.startDate+30days%, status='pending'
- insert into checkins, employeeId=%id%, type='90-day', dueDate=%request.startDate+90days%, status='pending'

Start
- every day at 8am, call !SendReminders

SendReminders
- select c.type, c.dueDate, e.name, e.manager from checkins c join employees e on c.employeeId=e.id where c.dueDate=%Tomorrow% and c.status='pending', write to %upcoming%
- foreach %upcoming%, call !SendCheckInReminder item=%checkin%

SendCheckInReminder
- send email to %checkin.manager%, subject: "%checkin.type% Check-in Due: %checkin.name%", body: "%checkin.name%'s %checkin.type% check-in is due tomorrow."
```

---

## Wait — that's the program?

That's your onboarding system. New hire registration, welcome emails, manager notifications, scheduled check-ins with automatic reminders — all in one readable file. No HR software subscription. No IT dependency.

---

## What Just Happened

- **`insert into employees`** — Employee database created automatically. No spreadsheet.
- **`send email`** — Welcome packet, manager notification, check-in reminders — all built-in email.
- **`read templates/welcome_email.txt`** — Use text files as templates. Edit them anytime.
- **`insert into checkins`** — 30-day and 90-day check-ins scheduled automatically with the hire.
- **`every day at 8am`** — Daily reminder check runs automatically. No calendar reminders needed.
- **`NewHire - POST`** — An API endpoint you can connect to a web form.

Need to add a 60-day check-in? Add one line. Need to change the welcome email? Edit the template file. You control the process.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your onboarding workflows run reliably with zero ongoing cost. Build once, your HR automations run forever.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir HRWorkflows && cd HRWorkflows
# Create Start.goal with your onboarding steps
plang exec
```

Write your HR processes like you'd explain them to a new team member. Build once. Let them run.

[Full getting started guide →](/get-started)
