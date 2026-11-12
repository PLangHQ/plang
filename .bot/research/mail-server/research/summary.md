# Summary: mail-server research

**Version:** v1

## What this is

Ingi asked whether PLang should build its own mail server: PLang goals on top and a C# module for SMTP. Requirements: custom domain, thousands of outbound mails a day through our API, inbound with auto-delete after X days, live push of incoming mail, and identity-based addresses (every identity gets one unless linked to a real email; one real email can map to many identities). Today we use AgentMail, Scaleway TEM, Postmark, Graph and the Gmail API in different apps, and none covers the whole set.

The work is on `main` (the v0.1.x line, `PLang/Modules/...`), not `runtime2`. This branch was reset to `origin/main` for that.

## What was done

- Read `main`: no email module exists; `message.SendEmail` runs an `Email` app that calls `email.plang.io`; the server WebSocket is stubbed ("Not Supported") and `WebSocketModule` is a client stub; the identity-keyed poll stream is the push mechanism that works today; `%Identity%` is a base64 Ed25519 key.
- Researched the MTA options (Stalwart, SmtpServer, Haraka, Maddy, MailKit/MimeKit), Gmail/Yahoo/Microsoft sender rules, SES enforcement and warm-up, Hetzner and Scaleway port 25 and PTR, SES vs TEM cost, and AgentMail's real limits.
- Wrote `/shared/research/mail-server/brief.md` (starts with "Target branch: main") and `/shared/research/mail-server/evidence.md` (numbers, sources, read vs search-summary tags). Indexed in `/shared/research/INDEX.md` as `handed-over`.

**Position:** pursue, reshaped. Own inbound (thin C# SMTP-receive module, PLang goals for addressing, storage, retention, push). Relay outbound through SES or Scaleway TEM behind `message.SendEmail`. Port 25 is not the obstacle; deliverability and reputation are.

**Still open:** the architect (`architect/mail-server`) was not in the running-agents list, so no message was sent. Start it with `.\run.ps1 architect mail-server`. Scaleway facts (ports, TEM price, quota) come from search summaries because scaleway.com returned 403; they need checking on the page.

## Code example (the shape of the idea)

```plang
Start
- start smtp server, host "mx.example-id.net", port 25, certificate %cert%
    on recipient call !AcceptRecipient
    on mail call !ReceiveMail

AcceptRecipient
- select identity from addresses where address=%recipient%, return 1 row, write to %owner%
- if %owner% is empty, reject recipient "no such user"
```

C# owns the protocol (listener, TLS, limits, MIME parse, wire answers); PLang owns every decision.
