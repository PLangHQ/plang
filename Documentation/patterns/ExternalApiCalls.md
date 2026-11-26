# Calling an outside api

An order is placed and the accounting system must hear about it. The outside service is sometimes slow and sometimes down, and the
visitor must not wait for it or lose the order because of it. The pattern: call it synchronously with a short timeout, and when it
fails, write what failed to a table that a scheduled job sends again until it goes through.

## 1. The call

```plang
/ order/Create.goal
Create
- insert into orders, name=%request.query.name%, write to %orderId%
- call goal /accounting/RecordSale orderId=%orderId%
- write out "order %orderId% created, receipt: %receipt.receiptId%"
```

```plang
/ accounting/RecordSale.goal
RecordSale
- post %Settings.AccountingUrl%, data: {"orderId": %orderId%}, timeout 3 seconds, write to %receipt%
    on error call QueueSale

QueueSale
- insert into sales_outbox, order_id=%orderId%, last_error=%!error.Message%
    on error ignore
```

```plang
Setup
- create table sales_outbox, columns: order_id(int, not null, unique), attempts(int, default 1), last_error(string, nullable),
    status(string, default 'pending'), created(datetime, default now), sent(datetime, nullable)
```

- **`timeout 3 seconds`** is the longest the visitor waits for the service. Pick it per service.
- **`on error call QueueSale`** hands the error to `QueueSale`, and the goal goes on with its next step: the order is created and the
  page answers, without a receipt. `%!error.Message%` says why it failed.
- `order_id` is unique and the insert has `on error ignore`, so an order that is already queued is not queued twice.
- The service's url and key are settings, and only the `accounting/` folder knows the service's url and fields.

Run against a service that was down, then slow, then up:

| service | the page | `sales_outbox.last_error` |
|---|---|---|
| down (answers 503) | 200 in 1.5 s, no receipt | `{"error":"provider is down","key":"UserDefinedError","status":503}` |
| slow (5 s) | 200 in 3.1 s, no receipt | `The request was canceled due to the configured HttpClient.Timeout of 3 seconds elapsing.` |
| up | 200 in 0.15 s, `receipt: R-…` | no row |

## 2. The retry

```plang
/ accounting/RetrySales.goal
RetrySales
- select id, order_id from sales_outbox where status = 'pending' and attempts < 10 order by id, write to %pending%
- go through %pending%, call RetrySale sale=%item%

RetrySale
- set %receipt% = ""
- post %Settings.AccountingUrl%, data: {"orderId": %sale.order_id%}, timeout 3 seconds, write to %receipt%
    on error call StillFailing
- if %receipt% is not empty, call MarkSent

MarkSent
- update sales_outbox set status = 'sent', sent = CURRENT_TIMESTAMP where id = %sale.id%

StillFailing
- update sales_outbox set attempts = attempts + 1, last_error = %!error.Message% where id = %sale.id%
```

```plang
/ in Start, production only (see Start and environments)
RunScheduledTasks
- every 10 minutes, call /accounting/RetrySales
- start scheduler
```

- `set %receipt% = ""` first: a goal called in a loop sees the variables of the run before it, so without it a failed retry would
  find the last good receipt and be marked sent.
- Because `on error call` goes on to the next step, the step after the post checks that a receipt came back. Only then is the row
  marked `sent`.
- `attempts < 10` stops a row that never goes through. Look at those rows by hand.
- Both rows of the run above went through on the first retry after the service came back: `status = 'sent'`, `attempts = 2`.

## Why this way

- **`dont wait` on the call** ends the visitor's wait, but a failure then reaches no one: there is no page to answer and nothing
  records it unless the background goal does. A timeout of a few seconds gives the same protection and keeps the failure where
  the order is.
- **Retrying inside the request** (`retry 3 times over 10 seconds`) holds the visitor for the whole retry, and a service that is down
  for an hour still loses the order.
- **The error goes to the visitor** without `on error call`: the order exists but the page says it failed, and the visitor places it
  again.

## Development and tests

A dev or test instance must not reach the real service. Point the setting at a mock, or register a mock for the url:
[Mocking a provider for a test instance](../TalkingToAnApi.md#mocking-a-provider-for-a-test-instance).

See also: [Talking to an api](../TalkingToAnApi.md), [Start and environments](./StartAndEnvironments.md),
[App error handling](./AppErrorHandling.md), [Error handling](../ErrorHandler.md).
