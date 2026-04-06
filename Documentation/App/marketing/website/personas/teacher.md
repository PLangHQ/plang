# PLang for Teachers

---

## Headline
**Grade smarter. Communicate faster.**

---

## The Daily Grind

You spend your evenings grading papers and your mornings writing the same email to parents about missing assignments. Progress reports mean hours of copying grades from one system to another. You'd love to send personalized feedback to each student, but there are 120 of them and one of you.

You didn't get into teaching to do data entry.

---

## The PLang Way

```plang
Start
- start webserver
- every friday at 3pm, call !WeeklyParentUpdate

SubmitGrade - POST
- insert into grades, student=%request.student%, assignment=%request.assignment%, score=%request.score%, maxScore=%request.maxScore%, date=%Now%, write to %id%
- write out {gradeId: %id%, status: 'recorded'}

WeeklyParentUpdate
- select student, avg(score * 100.0 / maxScore) as average from grades where date >= %Now-7days% group by student, write to %weeklyAverages%
- foreach %weeklyAverages%, call !NotifyParent item=%studentAvg%

NotifyParent
- select email from parents where student=%studentAvg.student%, write to %parent%
- if %parent% is empty then
    - write out 'No parent email for %studentAvg.student%, skipping'
- send email to %parent.email%, subject: "Weekly Progress: %studentAvg.student%", body: "This week's average: %studentAvg.average%%%. Keep up the encouragement at home!"

MissingAssignments
- select student from assignments where dueDate < %Now% and student not in (select student from grades where assignment=assignments.name), write to %missing%
- foreach %missing%, call !NotifyMissing item=%student%

NotifyMissing
- select email from parents where student=%student.student%, write to %parent%
- send email to %parent.email%, subject: "Missing Assignment: %student.student%", body: "Your child has a missing assignment. Please check in with them."
```

---

## Wait — that's the program?

That's your grade tracking, parent communication, and missing assignment notification system. Weekly progress updates go out automatically every Friday afternoon.

---

## What Just Happened

- **`insert into grades`** — Grade book database created automatically. Every score tracked and queryable.
- **`avg(score * 100.0 / maxScore)`** — Automatic grade calculation with SQL.
- **`every friday at 3pm`** — Weekly parent emails sent automatically before the weekend.
- **`send email`** — Personalized parent communication. 120 students, 120 personalized emails, zero manual effort.
- **`SubmitGrade - POST`** — API endpoint. Build a simple web form for grade entry from any device.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your grading system runs reliably with zero ongoing cost. Build once, your automations run all semester.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir ClassroomTools && cd ClassroomTools
# Create Start.goal with your grading workflow
plang exec
```

Write your classroom processes in plain English. Spend your evenings on something better than data entry.

[Full getting started guide →](/get-started)
