# Mail server in plang: v1 first-pass trace

Branch `architect/mail-server`, built on `main` (v0.1 module system), not runtime2. Status: first pass, waiting on the researcher's brief for prior art and deliverability, and on Ingi for the questions below. Nothing is carved into stages yet.

## QUESTIONS

1. **What is linking a real address for?** My reading: a linked address is where we *deliver* that identity's mail, proven by a code sent to it. It does not merge identities: identity A and identity B both linked to `ingi@gmail.com` stay two identities that happen to share a mailbox. An app that wants "same person" asks "which identities share this address?". Is that right, or is "1 email → many identities" meant as a sign-in/merge key?
2. **Who may send to the outside world?** An identity costs nothing to make, so a per-identity limit stops nobody: whoever wants to spam makes 10,000 identities. My lean: internal → internal is free for everyone; sending to an outside address needs the sender on an allowlist (a setting) in v1. Without some gate, our domain's reputation is gone within days.
3. **Websocket, or the live stream we already have?** main has no websocket server (`WebserverModule/Program.cs:1165` returns "Not Supported"). It does have a per-identity live HTTP stream (`/?plang.poll=1`, `RequestHandler.cs:515`), signed, already used by plang.js. I'd push mail over that. A websocket is only worth building if a subscriber can't speak the plang client. Who are the subscribers?

## Why

A plang identity has no email address, by design (`Documentation/Identity.md`: no name, no email, just a key). But apps still need to reach people by mail, and people need to receive mail somewhere. Today `send email` (`MessageModule/Program.cs:474`) forwards to the app `os/apps/Email`, which dispatches to `/service/%service%/SendEmail`, default service `email.plang.io`, and that goal is a stub with its `post` commented out. So the door developers use already exists and points at a server that doesn't exist yet. This plan is that server.

## The model: an identity has one address

Every identity has exactly one current address. It sends from it and receives to it.

- **internal**: `<random>@ourdomain`. We host the mailbox: mail to it lands in our inbox and is pushed live. Exactly one identity owns it.
- **linked**: a real address someone else hosts (gmail etc.), proven by a code. We deliver to it; we can't receive for it (its MX isn't ours). Many identities can link the same address, each proving it separately.

One table holds both, one row per (identity, address) pair:

```
address(value, identity, kind internal|linked, created, retired)
```

- identity → its address: the row for that identity with `retired` empty. One answer.
- address → owner (inbound): `value = rcpt and kind = 'internal' and retired is empty`. One identity.
- address → identities (linked): same value, `kind = 'linked'`. Many.
- An internal value is unique across every row, retired ones included: **a retired address is never given to another identity.** Reissuing it would hand the new owner the old owner's password resets.

Rules that fall out:

- **Born on first ask.** An identity gets its internal address the first time anything asks for it (send, the API, a link check), never at identity creation. An identity that links before its first ask never gets an internal address, which is exactly "unless linked".
- **Linking retires the internal address.** It stops accepting new mail (550); what's in the inbox ages out by retention. No forwarding to the linked address: forwarding someone else's mail breaks SPF/DMARC and lands in spam, unless we do SRS/ARC. Not v1.
- **Unlinking** brings back that identity's own retired internal address, or births one if it never had one.
- The random local part never derives from the identity key, so the address leaks nothing about the identity.

## Play the movie

### A. The world mails an internal address, the owner sees it live

1. Someone at gmail sends to `k3x9q2@ourdomain`. Gmail looks up our MX and connects to port 25.
2. **C# MailModule** (receive server) runs the SMTP session. It accepts RCPT only for our domains (never an open relay), enforces a size limit, offers STARTTLS. At end of DATA it parses with MimeKit (already in `PLang.csproj:84`) and calls the goal given in `on mail call !ReceiveMail`, in a fresh engine, the way `MessageModule.ProcessEvent` does (`MessageModule/Program.cs:217-287`). It hands `%mail%`: from, to (list), subject, text, html, date, message id, attachments (name, type, bytes), plus the sender checks (SPF/DKIM/DMARC; how, per the brief).
3. **ReceiveMail.goal** (plang), sketch:

```
ReceiveMail
- go through %mail.To%, call DeliverTo %recipient%=item

DeliverTo
- call OwnerOf %recipient%, write to %owner%
- if %owner% is empty, end goal
- insert into mail, identity=%owner%, from, subject, text, html, received=%Now%, write to %mailId%
- go through %mail.Attachments%, call SaveAttachment
- send to identity %owner%, {id: %mailId%, from: %mail.From%, subject: %mail.Subject%}
```

4. **Push**: the last step is new (see "C# boundary"). It finds the identity's live connection and writes to it. No connection open: nothing happens, the mail waits in the inbox.
5. The owner's browser opens its live stream; the existing `OnPollStart` hook (`WebserverModule/Program.cs:161`) runs a goal that sends the unread list. So "pushed while online, fetched on connect" uses one inbox and no in-memory queue.

Unknown recipient: dropped after DATA, no bounce (a bounce to a forged sender is backscatter). Rejecting at RCPT time is better manners but needs a plang callback inside the SMTP session; I'd leave it for later.

### B. An app sends mail through our API

1. The developer writes `- send email to "ingi@gmail.com", subject "Hi", body "..."`. That is `MessageModule.SendEmail` → `os/apps/Email` → `service/email.plang.io/SendEmail.goal`, whose `post` we turn on. The request is signed, so on our side `%Identity%` is the calling app's identity.
2. **SendMail.goal** (our server): From = `AddressOf %Identity%` (born now if this is its first ask). For each recipient, `OwnerOf` answers:
   - an owner → **local delivery**: call `DeliverTo`, the same door as inbound. No SMTP at all.
   - no owner → outside address → the sender gate (question 2) → insert into `outbox` (queued).
   - an identity instead of an address → `AddressOf` that identity first, then as above. (`SendEmail`'s `@` check at `MessageModule/Program.cs:482` refuses this today; it would need to let an identity through.)
3. A schedule (`ScheduleModule.Schedule`, cron, `ScheduleModule/Program.cs:136`) runs every minute: picks due outbox rows, calls **C# `send mail`** once per row.
4. C# makes one attempt: builds MIME, DKIM-signs with our key, delivers (to the recipient's MX, or to a relay; the brief decides). It answers one of: `sent`, `retry` (4xx, no connection), `failed` (5xx), with the server's reply.
5. Plang writes the answer back: `sent` done; `retry` → attempts+1, next try with backoff; `failed` or too many attempts → the sender gets a bounce in its inbox (and live push).

Thousands a day is about one a minute on average; a db table and a cron are plenty.

### C. An identity links a real address

1. `POST /api/mail/link {address}`: make a code, store (identity, address, code hash, expires), send the code to that address through B (system sender, e.g. `noreply@ourdomain`).
2. `POST /api/mail/link/confirm {code}`: match → retire the identity's internal row (if any) → insert the linked row.
3. From now on `AddressOf %Identity%` answers the linked address. Mail sent *to* this identity goes out via the outbox to gmail.

Open for when a linked identity *sends*: we can't honestly put `ingi@gmail.com` in From (gmail's DMARC fails). The usual answer is From `ingi via ourdomain <relay@ourdomain>`, Reply-To the real address. Decide after question 1.

### D. Retention

- Reads never show expired mail: every inbox read filters `received > now − retention`. Correct the moment a mail expires, whether or not the sweep has run.
- A schedule (hourly) deletes expired mail rows and their attachment files, finished outbox rows, and expired link codes. The sweep frees space; it isn't what hides mail.
- Retention days: one app setting in v1. Per-identity later if anyone asks.
- Retired address rows are kept forever (the reuse ban needs them). They are tiny.

## C# boundary

Rule: C# speaks SMTP and MIME; plang decides everything else. One question, one answer, on the side that owns it.

| C# (MailModule, `PLang/Modules/MailModule/`) | plang (the mail server app) |
|---|---|
| `start mail server`: domains, port, size limit, cert, `on mail` goal. Listener + `KeepAlive` like the webserver. | address model, `AddressOf`, `OwnerOf`, link/confirm/unlink |
| per message: parse with MimeKit, sender checks, call the goal | inbox storage, attachments as files |
| `send mail`: compose, DKIM-sign, one delivery attempt, answer `sent`/`retry`/`failed` | outbox, retries, backoff, bounces, sender gate |
| | retention sweep, API routes, push call |

One more C# piece, outside MailModule: **`send to identity`** on WebserverModule. The live connections sit in `Engine.LiveConnections`, keyed by identity (`Engine.cs:156`), and the webserver's `listeners` list is static (`WebserverModule/Program.cs:53`), so any context in the process can find them; `GetNumberOfLiveConnections` (`Program.cs:69`) already walks them. But the only writer, `HttpSink`, is made per request and reads `context.HttpContext!.Request.Path` in its constructor (`HttpSink.cs:53`), so a goal running from the mail listener can't push today. The new action finds the identity's connection and writes through the same transformer. It's useful beyond mail (any background goal telling a user something), which is a reason to put it on the webserver and not in MailModule.

Core module vs `.modules`: main loads app modules from `.modules` (`Container.cs:557`). MailModule pulls in an SMTP server library and an SMTP client; only the mail host needs them. I'd still put it in core beside MessageModule, the way the webserver and Nostr listeners are, unless the brief's library choice makes the dependency heavy. Not a strong lean.

Out of scope, on purpose: IMAP, POP3, the submission port (587), SMTP AUTH. Nobody opens our mail in Thunderbird; the API and the live stream are the only doors. That cuts most of a mail server away.

## OBP check

| Question | Who answers | Pattern |
|---|---|---|
| What is this identity's address? | `AddressOf`, one goal, used by send, link and the API | 12, one answer |
| When is the internal address made? | its first asker | 11, lazy |
| Who owns this address? | `OwnerOf`; "is it local?" is "did it answer an owner?", not a second check | 12 |
| How is a mail delivered locally? | `DeliverTo`, the one door for inbound and internal sends | one door |
| What does a delivery attempt mean? | C# answers once; plang decides retry | one question, one answer |
| What does a user see after reconnecting? | the inbox, read by the `OnPollStart` asker; nothing queued in memory | 11 |
| Is this mail expired? | the read's filter; the sweep only frees space | 12 |

## Waiting on the brief

- SMTP server library (SmtpServer by cosullivan, or hand-rolled) and client (MailKit). MimeKit is already in.
- Direct to MX from our own IP, or a relay (SES, Postmark, ...). Warm-up for a fresh IP at thousands/day.
- DNS: SPF, DKIM key and rotation, DMARC, PTR/rDNS, MTA-STS.
- Bounce handling: Return-Path / VERP, feedback loops.
- Inbound sender checks: SPF, DKIM, DMARC, and what we do on fail (drop, flag).
- Prior art for identity-keyed / disposable addresses.

## Tentative stages (not carved)

1. Address model in plang: table, `AddressOf` with lazy birth, `OwnerOf`, retire and the reuse ban, link/confirm/unlink. No SMTP needed to test.
2. Receive: MailModule `start mail server`, `ReceiveMail`/`DeliverTo`, inbox API.
3. Push: WebserverModule `send to identity`, the push in `DeliverTo`, unread on `OnPollStart`.
4. Send: MailModule `send mail`, outbox, retry schedule, local delivery, sender gate, turn on the `email.plang.io` service goal.
5. Retention: read filter and sweep.

What this replaces: the stub `os/apps/Email/service/email.plang.io/SendEmail.goal` (its `post` gets turned on and pointed at us), and the old sketch `Tests/Email/Email.goal` ("set smtp as localhost, 25"), which goes.
