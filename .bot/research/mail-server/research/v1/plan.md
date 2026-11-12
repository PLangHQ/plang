# v1 plan: own mail server in PLang?

Target branch: main (v0.1.x line, `PLang/Modules/<Name>Module/Program.cs`). Not runtime2.

## Question

Should PLang build its own mail server: PLang goals on top, a C# module for the SMTP server? Requirements: custom domain; thousands of sends/day through our API; inbound with auto-delete after X days; websocket push of incoming mail; identity-based addresses (every identity gets one unless linked to a real email; one real email can map to many identities).

## Steps

1. Read main: what mail, websocket, identity and push code exists (done: no email module; `message.SendEmail` -> `Email` app -> `email.plang.io`; websocket module is a client stub, server websocket is "Not Supported"; identity-keyed poll stream exists; `%Identity%` is base64 Ed25519 public key).
2. Prior art for the MTA layer: Stalwart, Haraka, Maddy, SmtpServer, MailKit/MimeKit.
3. Outbound deliverability facts: Gmail/Yahoo/Microsoft rules, SES enforcement thresholds, IP warm-up.
4. Infra: Hetzner and Scaleway port 25, PTR.
5. Cost: SES vs Scaleway TEM at 1k/3k/5k/10k per day.
6. AgentMail check (the "none cover it" claim).
7. Write `/shared/research/mail-server/brief.md` (+ `evidence.md` for numbers and sources), update `INDEX.md`, hand to the architect (`architect/mail-server`).

## Position going in

Own inbound, relay outbound. The "mail server" is two products: receiving is cheap and safe, sending is a reputation business. Push back on: port 25 as the issue, websocket as a mail problem, "linked to real email" as a simple switch.

## Not doing

No design, no class names, no code. Doc changes would go in the brief.
