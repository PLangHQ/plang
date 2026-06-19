# Channels

A channel is a named destination. You write to it by name. PLang calls the goal that handles it.

```plang
- write out to logger "Order complete"
- write out to slack "Payment failed: %order.id%"
- write out to debug "Processing step %step%"
```

The channel goal receives the message in `%!data%` and decides what to do with it — write to a file, send a request, post a message. The caller never needs to know.

---

## Defining a channel

Create a goal with the name of the channel:

```plang
logger
- write to file "app.log", text=%!data%
```

That's it. Every `write out to logger` in your program now calls this goal. Change the goal, and every log reroutes automatically.

---

## Built-in channels

### logger

Keeps a record of what your program does. You define it — PLang calls it.

```plang
logger
- write to file "app.log", text=%!data%
```

### debug

Only runs when you start your program with `--debug`. Use it for detail you want during development but not in production.

```plang
debug
- write out %!data%
```

---

## Custom channels

Name a channel anything. Slack, email, mobile — the name is just a goal name.

```plang
slack
- post to "https://hooks.slack.com/services/YOUR/WEBHOOK", body=%!data%

email
- send email to "team@example.com", subject="Alert", body=%!data%

mobile
- send push notification to %user.deviceToken%, message=%!data%
```

---

## A channel can do many things

A channel goal is just a goal — it can have as many steps as you need.

```plang
error
- write to file "errors.log", text=%!data%
- send email to "oncall@example.com", subject="Error", body=%!data%
- post to "https://hooks.slack.com/...", body=%!data%
```

`write out to error "Payment failed"` now logs, emails, and posts to Slack — all defined in one place.

---

## Why channels are powerful

Without channels, notification and logging code is scattered across every file. Change where errors go and you're editing dozens of places.

With channels, it's one goal. Write `write out to logger` anywhere, and the channel goal decides what happens.

- **One place to change** — reroute an entire program's logging by editing one goal
- **Environment-aware** — dev writes to a file, prod sends to a monitoring service
- **Composable** — one channel goal can write to multiple destinations
- **Testable** — point a channel at a test file to capture output during tests

---

## What's next

- [Goal](../goal/) — how goals work
- [Step](../goal/step/) — what goes inside a goal
