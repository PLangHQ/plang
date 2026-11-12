# architect/mail-server: summary

## 2026-11-12 — v1: first-pass trace for a plang mail server (on main)

**What this is.** A mail server written in plang, with a C# module for the SMTP protocol: our own domain, sending thousands a day through our own API, receiving mail with automatic deletion after X days, pushing new mail to its owner live, and giving every identity an address unless it links a real one. The branch was reset onto `origin/main` (v0.1 module system); runtime2 is not the base.

**What was found on main.** The developer's door already exists: `send email` (`PLang/Modules/MessageModule/Program.cs:474`) runs the app `os/apps/Email`, which dispatches to `service/email.plang.io/SendEmail.goal`, a stub with its `post` commented out. Real-time push exists as a per-identity live HTTP stream (`/?plang.poll=1`, `Engine.LiveConnections`), not a websocket; main's server websocket returns "Not Supported". A goal running outside a request can't write to another identity's live stream today (`HttpSink` needs the request's `HttpContext`).

**What was decided (first pass, not yet confirmed by Ingi).** Each identity has one current address: internal (`<random>@ourdomain`, one owner, we hold the inbox) or linked (a real address proven by a code, many identities per address, delivered to by our outbox). The internal address is born on first ask; linking retires it; a retired address is never given to another identity. C# MailModule does `start mail server` (receive, parse with MimeKit, call a goal) and `send mail` (one attempt, answer `sent`/`retry`/`failed`); plang owns the address model, inbox, outbox and retries, retention, the API and the push. One new C# action on WebserverModule, `send to identity`, writes to an identity's live stream from any context. Retention: reads filter expired mail, an hourly sweep deletes it. No IMAP/POP3/587.

**Open.** Three questions to Ingi at the top of [v1/plan.md](v1/plan.md): what linking is for (delivery target vs merge key), who may send outside (identities are free, so a per-identity limit is no gate), and websocket vs the existing live stream. Library choice and deliverability (direct to MX vs relay, DNS, warm-up, bounces) wait on the researcher's brief, not in `/shared/research/` yet.

Stage status: not carved. Tentative order in the plan: address model → receive → push → send → retention.
