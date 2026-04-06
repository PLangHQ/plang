# PLang for Clinic Administrators

---

## Headline
**Appointments and reminders, handled.**

---

## The Daily Grind

The phone rings. Someone wants to reschedule. You open the calendar, find an open slot, update the booking, then remember to text the doctor about the change. Meanwhile, three patients from yesterday didn't show — and nobody sent them a reminder the day before.

You're managing appointments in one system, patient contact info in another, and sending reminders manually (when you remember). No-shows cost the clinic money, and every missed reminder is a missed appointment.

---

## The PLang Way

```plang
Start
- start webserver
- every day at 4pm, call !SendTomorrowReminders
- every monday at 7am, call !WeeklyNoShowReport

BookAppointment - POST
- select count(*) as booked from appointments where doctor=%request.doctor% and date=%request.date% and time=%request.time%, write to %existing%
- if %existing.booked% > 0 then
    - write out {error: 'Slot already booked'}, status code 409
- insert into appointments, patient=%request.patient%, patientEmail=%request.email%, patientPhone=%request.phone%, doctor=%request.doctor%, date=%request.date%, time=%request.time%, status='confirmed', write to %id%
- send email to %request.email%, subject: "Appointment Confirmed", body: "Your appointment with Dr. %request.doctor% is confirmed for %request.date% at %request.time%. Reply to this email if you need to reschedule."
- write out {appointmentId: %id%, status: 'confirmed'}

SendTomorrowReminders
- select * from appointments where date=%Tomorrow% and status='confirmed', write to %tomorrow%
- foreach %tomorrow%, call !SendReminder item=%apt%

SendReminder
- send email to %apt.patientEmail%, subject: "Appointment Tomorrow - %apt.time%", body: "Hi %apt.patient%, reminder: you have an appointment with Dr. %apt.doctor% tomorrow at %apt.time%. Please let us know if you can't make it."

MarkNoShow - POST
- update appointments set status='no-show' where id=%request.appointmentId%
- write out {status: 'marked as no-show'}

WeeklyNoShowReport
- select doctor, count(*) as noShows from appointments where status='no-show' and date >= %Now-7days% group by doctor, write to %noShowStats%
- select count(*) as total from appointments where date >= %Now-7days%, write to %totalApts%
- send email to admin@clinic.com, subject: "Weekly No-Show Report", body: "Total appointments: %totalApts.total%\n\nNo-shows by doctor:\n%noShowStats%"

DailySchedule - GET
- select time, patient, doctor, status from appointments where date=%Today% order by time, write to %schedule%
- write out %schedule%
```

---

## Wait — that's the program?

That's your clinic scheduling system. Appointment booking with conflict checking, automatic reminders the day before, no-show tracking, and weekly reports. Patients get confirmations and reminders. You get a dashboard.

---

## What Just Happened

- **`insert into appointments`** — Appointment database created automatically. Every booking tracked.
- **`select count(*) where doctor and date and time`** — Double-booking prevention built into the logic.
- **`every day at 4pm`** — Tomorrow's reminders sent automatically every afternoon. No-shows drop.
- **`send email`** — Confirmations and reminders go out without manual effort.
- **`WeeklyNoShowReport`** — Track patterns. See which days or doctors have the most no-shows.
- **`DailySchedule - GET`** — Today's schedule at a glance from any browser.

Change the reminder time from 4pm to 6pm? Change one line. Add SMS reminders? Add a step. Your system adapts to your clinic.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your reminder system runs reliably every day with zero ongoing cost. Build once, your clinic admin runs itself.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir ClinicTools && cd ClinicTools
# Create Start.goal with your scheduling workflow
plang exec
```

Write your clinic operations in plain English. Focus on patient care, not calendar management.

[Full getting started guide →](/get-started)
