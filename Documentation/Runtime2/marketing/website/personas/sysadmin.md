# PLang for SysAdmins

---

## Headline
**Server health checks that read like runbooks.**

---

## The Daily Grind

You've got 30 servers and a pager that doesn't sleep. You're writing bash scripts that are three `grep` pipes deep, parsing log files with `awk` commands you'll never understand again in six months, and rigging cron jobs together with shell scripts that send emails via `curl` to a Slack webhook because "it works."

Every monitoring script is a brittle chain. Every alert is another `if` statement bolted onto a script nobody wants to touch. You know what the checks should do — you just wish you could write them that way.

---

## The PLang Way

```plang
Start
- every 5 minutes, call !HealthCheck

HealthCheck
- get https://server1.internal/health, write to %status%
    on error call !AlertDown, continue to next step
- read /var/log/app/error.log into %logContent%
- set %errorCount% = %logContent.split('\n').length%
- if %errorCount% > 100 then call !AlertHighErrors
- select last_check from health_log order by id desc limit 1, write to %lastCheck%
- insert into health_log, server='server1', status=%status.code%, errors=%errorCount%, checked=%Now%
- write out 'Health check complete: %status.code%, %errorCount% errors'

AlertDown
- send email to ops@company.com, subject: "Server Down: server1", body: "Health endpoint unreachable at %Now%. Investigate immediately."

AlertHighErrors
- send email to ops@company.com, subject: "High Error Rate: server1", body: "Error log has %errorCount% entries. Threshold: 100."
```

---

## Wait — that's the program?

That's your entire monitoring script. Health check, log parsing, database logging, email alerts, scheduled execution. Read it in six months and you'll still know exactly what it does.

---

## What Just Happened

- **`every 5 minutes`** — Built-in scheduler. No cron configuration.
- **`get https://...`** — HTTP health check with automatic error handling and retries.
- **`read /var/log/...`** — File system access. Read logs, configs, anything.
- **`on error call !AlertDown`** — Error handling that reads like English. Retry logic, fallbacks, continue-on-error — all built in.
- **`insert into health_log`** — SQLite database created automatically. No setup. Query it later for trends.
- **`send email`** — SMTP email, built in. No `sendmail` config, no webhook hacks.

The `.goal` file IS your runbook. When you hand it to another admin, they can read it without learning a language.

---

## The Build / Run Split

PLang uses AI at **build time only** to compile your English into executable instructions. At **runtime**, there's no AI — no network dependency, no API calls, deterministic execution. Build once on a machine with internet access, then deploy the compiled `.pr` files anywhere.

This means your monitoring scripts run reliably offline. The AI is a one-time compiler, not a runtime dependency.

---

## More Things You Can Automate

**Disk space monitoring:**
```plang
CheckDisk
- run 'df -h /', write to %diskInfo%
- if %diskInfo% contains '9[0-9]%' then call !DiskAlert
```

**SSL certificate expiry:**
```plang
CheckCerts
- every 1 day, call !VerifyCerts

VerifyCerts
- get https://mysite.com, write to %response%
- if %response.certificate.daysRemaining% < 14 then
    - send email to ops@company.com, subject: "SSL Expiring", body: "Certificate expires in %response.certificate.daysRemaining% days"
```

**Log rotation and archival:**
```plang
ArchiveLogs
- list files in /var/log/app/*.log, write to %logFiles%
- foreach %logFiles%, call !ArchiveFile item=%logFile%

ArchiveFile
- if %logFile.size% > 10485760 then
    - copy %logFile.path% to /archive/%logFile.name%_%Now%.log
    - write to %logFile.path%, ''
    - write out 'Archived %logFile.name%'
```

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir ServerMonitor && cd ServerMonitor
# Create Start.goal with your health checks
plang exec
```

Write your monitoring like you'd explain it to a colleague. Build once. Run forever.

[Full getting started guide →](/get-started)
