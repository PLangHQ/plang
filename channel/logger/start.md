# What is logger?

You saw `write out to logger "Done. Files saved."` and wondered — what is `logger`?

When you send a report, you want it to go somewhere you can read it later. Without a channel, `write out` prints to the screen and disappears. A channel named `logger` is different: every `write out to logger` in your program sends the message to a destination you define once — a file, a URL, a database, wherever you need it.

You define the channel by creating a goal with the same name:

```plang
logger
- write to file "app.log", text=%!data%
```

That's it. Now every `write out to logger` call routes through this goal. `%!data%` is set to whatever was sent. Change this one goal, and every log in your program reroutes — no other code to touch.

Channels are one of PLang's more useful ideas. I'll hand this over now to explain the full picture.

---

# Channels

A channel is a named destination. You write to it by name. PLang calls the goal that handles it.

The channel goal receives the message in `%!data%` and decides what to do with it — write to a file, send an HTTP request, post to Slack, push a notification. The caller never needs to know.

---

## Logger

Keep a record of what happened.

```plang
logger
- write to file "app.log", text=%!data%
```

```plang
Start
- read orders.csv, write to %orders%
- foreach %orders%, call ProcessOrder order=%order%
- write out to logger "All orders processed"
```

Every `write out to logger` goes to `app.log`. Want to send it to a logging service instead? Change the goal. One edit, everywhere.

---

## Debug

The `debug` channel is built into PLang. It only runs when you start your program with `--debug`.

```plang
debug
- write out %!data%
```

Use it to add detail you only want to see while you're developing:

```plang
ProcessOrder
- write out to debug "Processing order %order.id%"
- validate %order%, write to %result%
- write out to debug "Validation result: %result%"
- save %order% to database
```

In production, those debug steps do nothing. In development, they show you everything.

---

## Custom channels

Name a channel anything. The name is just the name of a goal.

```plang
slack
- post to "https://hooks.slack.com/services/YOUR/WEBHOOK", body=%!data%
```

```plang
email
- send email to "team@example.com", subject="Alert", body=%!data%
```

```plang
mobile
- send push notification to %user.deviceToken%, message=%!data%
```

Now you write to them by name from anywhere:

```plang
- write out to slack "Order #%order.id% failed validation"
- write out to email "Daily summary: %summary%"
- write out to mobile "Your package shipped"
```

---

## A channel can do many things at once

A channel goal is just a goal — it can have as many steps as you need.

```plang
error
- write to file "errors.log", text=%!data%
- send email to "oncall@example.com", subject="Error", body=%!data%
- post to "https://hooks.slack.com/...", body=%!data%
```

Now one line — `write out to error "Payment failed"` — logs it, emails the on-call team, and posts to Slack. All defined in one place.

---

## Why channels are powerful

Most programs scatter logging and notification code throughout every file. When you need to change where errors go, you search and replace across dozens of files, hoping you didn't miss one.

With channels, all of that lives in one goal. You write `write out to logger` wherever you need it, and the channel goal decides what happens. Development, testing, production — each environment can point the channel at a different destination without touching the code that calls it.

- **One place to change** — reroute an entire program's logging by editing one goal
- **Environment-aware** — dev logs to a file, prod sends to a monitoring service
- **Composable** — one channel goal can write to multiple destinations
- **Testable** — point a channel at a test file to capture output during tests

---

## What's next

- [Goal](../../goal/) — how goals work
- [Step](../../goal/step/) — what goes inside a goal
