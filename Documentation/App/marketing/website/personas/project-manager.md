# PLang for Project Managers

---

## Headline
**Status reports that write themselves.**

---

## The Daily Grind

Every Monday morning: open three different tools, check who updated what, chase the people who didn't, calculate progress percentages, write the status email, CC the stakeholders, and hope nobody asks why the last report had a typo in the budget numbers.

Every Friday: do it again. On top of managing the actual project.

You didn't become a PM to be a human report generator.

---

## The PLang Way

```plang
Start
- start webserver
- every monday at 8am, call !WeeklyStatusReport
- every day at 9am, call !CheckOverdueTasks

UpdateTask - POST
- update tasks set status=%request.status%, updatedBy=%request.person%, updatedAt=%Now% where id=%request.taskId%
- if %request.status% is 'completed' then
    - insert into activity, taskId=%request.taskId%, action='completed', person=%request.person%, date=%Now%
- write out {status: 'updated'}

CheckOverdueTasks
- select * from tasks where dueDate < %Now% and status != 'completed', write to %overdue%
- foreach %overdue%, call !NudgeOwner item=%task%

NudgeOwner
- send email to %task.assignee%, subject: "Overdue: %task.title%", body: "Hi, your task '%task.title%' was due on %task.dueDate%. Can you update the status or let me know if you're blocked?"

WeeklyStatusReport
- select status, count(*) as count from tasks group by status, write to %taskSummary%
- select * from tasks where status='completed' and updatedAt >= %Now-7days%, write to %completedThisWeek%
- select * from tasks where dueDate <= %Now+7days% and status != 'completed', write to %dueSoon%
- select * from tasks where status != 'completed' and dueDate < %Now%, write to %overdue%
- send email to stakeholders@company.com, subject: "Weekly Status Report - %Now%", body: "Task Summary:\n%taskSummary%\n\nCompleted This Week:\n%completedThisWeek%\n\nDue Next Week:\n%dueSoon%\n\nOverdue:\n%overdue%"
- write out 'Status report sent'

AddTask - POST
- insert into tasks, title=%request.title%, assignee=%request.assignee%, dueDate=%request.dueDate%, status='open', created=%Now%
- write out {status: 'created'}
```

---

## Wait — that's the program?

That's your project tracking system. Task management, automatic overdue nudges, weekly status reports to stakeholders — assembled from your database automatically. No copy-paste. No Monday morning scramble.

---

## What Just Happened

- **`every monday at 8am`** — Status report compiled and sent automatically before standup.
- **`every day at 9am`** — Overdue tasks flagged and owners nudged daily. No awkward Slack messages.
- **`select count(*) from tasks group by status`** — Task summary calculated from real data. No manual counting.
- **`send email to stakeholders`** — Stakeholders get consistent, accurate reports without you formatting them.
- **`UpdateTask - POST`** — API endpoint. Connect a simple web form or integrate with other tools.

Want to add a "blocked" status? Add it to your workflow. Want bi-weekly reports? Change `every monday` to `every 2 weeks`. You control the process.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your status reports generate reliably with zero ongoing cost. Build once, your project admin runs itself.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir ProjectTracker && cd ProjectTracker
# Create Start.goal with your project workflow
plang exec
```

Write your PM workflows in plain English. Manage the project, not the reports.

[Full getting started guide →](/get-started)
