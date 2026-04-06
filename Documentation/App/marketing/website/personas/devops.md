# PLang for DevOps Engineers

---

## Headline
**Deployment scripts that read like runbooks.**

---

## The Daily Grind

Your deployment pipeline is 500 lines of YAML that nobody fully understands. Your monitoring scripts are bash one-liners chained together with `&&` and prayers. When something breaks at 2 AM, you're reading shell scripts you wrote six months ago, trying to remember what `$3` referred to in that `awk` command.

You maintain the automation that's supposed to reduce toil — but maintaining the automation IS the toil.

---

## The PLang Way

```plang
Start
- every 5 minutes, call !MonitorServices
- every day at 2am, call !NightlyBackup

MonitorServices
- get https://app.internal/health, write to %appStatus%
    on error call !HandleAppDown, continue to next step
- get https://api.internal/health, write to %apiStatus%
    on error call !HandleApiDown, continue to next step
- insert into health_log, service='app', status=%appStatus.code%, checked=%Now%
- insert into health_log, service='api', status=%apiStatus.code%, checked=%Now%

HandleAppDown
- send email to ops@company.com, subject: "APP DOWN - %Now%", body: "Application health endpoint unreachable. Investigate immediately."

HandleApiDown
- send email to ops@company.com, subject: "API DOWN - %Now%", body: "API health endpoint unreachable. Investigate immediately."

NightlyBackup
- read /data/app/database.db into %dbContent%
- save %dbContent% to /backups/db_%Now%.bak
- write out 'Backup completed: /backups/db_%Now%.bak'
- select count(*) as total from health_log where checked >= %Now-24hours%, write to %checkCount%
- send email to ops@company.com, subject: "Nightly Backup Complete", body: "Database backed up. %checkCount.total% health checks logged in last 24 hours."

DeployReport - GET
- select service, status, max(checked) as lastCheck from health_log group by service, write to %latest%
- select service, count(*) as failures from health_log where status != 200 and checked >= %Now-7days% group by service, write to %weeklyFailures%
- write out {current: %latest%, weeklyFailures: %weeklyFailures%}
```

---

## Wait — that's the program?

That's your monitoring and backup system. Health checks every 5 minutes, automatic alerts on failure, nightly database backups, and a deployment report endpoint. Read it six months from now — you'll know exactly what it does.

---

## What Just Happened

- **`every 5 minutes`** — Continuous health monitoring. No cron, no systemd timer config.
- **`get https://...health`** — HTTP health checks with automatic error handling.
- **`on error call !HandleAppDown`** — Error handling reads like English. Retry logic and fallbacks built in.
- **`insert into health_log`** — All monitoring data stored in SQLite. Query trends, generate reports.
- **`every day at 2am`** — Nightly backups run automatically. No cron configuration.
- **`DeployReport - GET`** — API endpoint for dashboards or status pages.

The `.goal` file IS your runbook. New team member? They can read it. Incident response? The process is right there in English.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English steps into executable `.pr` files (JSON). At **runtime**, no AI — deterministic execution, no network dependency. Build on a machine with internet, deploy the compiled `.pr` files to your servers.

This means your monitoring runs reliably without depending on an external AI service. The AI is a one-time compiler, not a runtime dependency.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir InfraTools && cd InfraTools
# Create Start.goal with your monitoring and automation
plang exec
```

Write your infrastructure automation in plain English. Build once. Run reliably.

[Full getting started guide →](/get-started)
