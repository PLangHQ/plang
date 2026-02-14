# PLang for Data Analysts

---

## Headline
**Automate your data pipelines, not your weekends.**

---

## The Daily Grind

Every Monday starts the same way. Pull data from three different APIs. Clean it. Merge it with last week's spreadsheet. Run the calculations. Format the report. Email it to the team. Repeat.

You know Python well enough to be dangerous, but maintaining a data pipeline with pandas, requests, SQLAlchemy, and cron feels like a second job. Half your time goes to dependency management and debugging environment issues, not actual analysis.

You just want the data in the right place, in the right shape, on the right schedule.

---

## The PLang Way

```plang
Start
- every monday at 8am, call !WeeklyReport

WeeklyReport
- get https://api.analytics.com/metrics?period=weekly, write to %metrics%
- get https://api.sales.com/revenue?period=weekly, write to %revenue%
- insert into reports, date=%Now%, pageviews=%metrics.pageviews%, revenue=%revenue.total%, conversion=%metrics.conversions%
- select date, pageviews, revenue, conversion from reports order by date desc limit 12, write to %trend%
- set %summary% to 'Weekly Report: %metrics.pageviews% views, $%revenue.total% revenue, %metrics.conversions% conversions'
- send email to team@company.com, subject: "Weekly Analytics - %Now%", body: "%summary%\n\nTrend (12 weeks):\n%trend%"
- write out 'Report sent: %summary%'
```

---

## Wait — that's the program?

That's your entire data pipeline. API calls, data storage, trend tracking, email delivery, weekly scheduling. No Python environment. No pip install. No cron configuration.

---

## What Just Happened

- **`every monday at 8am`** — Built-in scheduler. No cron, no task scheduler, no third-party service.
- **`get https://api...`** — HTTP requests to pull data from any API. Headers, auth, pagination — all supported.
- **`insert into reports`** — SQLite database created automatically. Your data is stored, queryable, and persistent. No database setup.
- **`select ... order by date desc limit 12`** — SQL queries work naturally. Build trend analysis with standard SQL.
- **`send email`** — SMTP email built in. No email API library needed.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English steps into executable instructions. At **runtime**, no AI is involved — deterministic execution, no API costs. Build once, your pipeline runs reliably on schedule.

---

## More Data Tasks You Can Automate

**CSV processing:**
```plang
ProcessCSV
- read sales_data.csv into %data%
- foreach %data%, call !ProcessRow item=%row%
- select category, sum(amount) as total from sales group by category, write to %summary%
- write out %summary%

ProcessRow
- insert into sales, date=%row.date%, category=%row.category%, amount=%row.amount%
```

**API data aggregation:**
```plang
AggregateData
- get https://api.service1.com/data, write to %dataset1%
- get https://api.service2.com/data, write to %dataset2%
- foreach %dataset1%, call !StoreRecord item=%record%
- select source, count(*) as records, avg(value) as average from combined_data group by source, write to %stats%
- save %stats% to report_%Now%.json
```

**Anomaly alerting:**
```plang
CheckAnomalies
- select avg(value) as avg, value from metrics order by id desc limit 1, write to %latest%
- if %latest.value% > %latest.avg% * 2 then
    - send email to analyst@company.com, subject: "Anomaly Detected", body: "Latest value %latest.value% is 2x above average %latest.avg%"
```

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir DataPipeline && cd DataPipeline
# Create Start.goal with your pipeline steps
plang exec
```

Write your pipeline like you'd describe it to a colleague. Build once. Run on schedule forever.

[Full getting started guide →](/get-started)
