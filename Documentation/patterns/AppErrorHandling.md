# App error handling

An error a visitor hits is only fixed when someone knows about it. This pattern records every error, from the server and from the
browser, in one table that a bot (or a person) reads to find what to fix: one row per distinct error, with where it happened, how
often, and whether it was marked fixed. One `on app error` event records the server's errors and answers the visitor with an error
page, the plang js client reports the browser's, and a route stores what it sends. [Error handling](../ErrorHandler.md) has the
mechanics of `on error`; this page is the shape.

## 1. The table

```plang
Setup
- create table errors, columns:
    fingerprint(string, not null, unique),
    source(string, not null),
    message(string, nullable), key(string, nullable), status_code(int, nullable),
    goal(string, nullable), goal_path(string, nullable), step(string, nullable), line(int, nullable),
    url(string, nullable), identity(string, nullable), user_agent(string, nullable), stack(string, nullable),
    count(int, default 1), first_seen(datetime, default now), last_seen(datetime, default now),
    status(string, default 'open'), note(string, nullable)
```

`fingerprint` is what makes two errors the same error. A repeat adds to `count` and moves `last_seen`, it never adds a row, so a page
in an error loop costs one row. `status` and `note` are for whoever fixes it: `fixed` and `fixed in 4c26393`.

## 2. Server errors

One handler records the error and answers the visitor with an error page that has the error's status code. What the page shows
depends on who is asking: a visitor gets a general text, an admin also gets the error's reference, the developer gets the details.

```plang
/ events/Events.goal
Events
- on app error, call goal /error/HandleError
```

```plang
/ error/HandleError.goal
HandleError
- if %!error.StatusCode% != 415 and %!error.Key% != "NoRouteNoFile" and %!error.Key% != "UserDefinedError" then call goal SaveError
- if %request!IsAjax% is false and %request!Headers.Accept% contains "application/json" then call JsonError, else PageError

SaveError
- set %fingerprint% = "server|%!error.Goal.RelativeGoalPath%:%!error.Step.LineNumber%|%!error.Key%|%!error.Message%"
- insert into errors (fingerprint, source, message, key, status_code, goal, goal_path, step, line, url, identity, user_agent)
    values (%fingerprint%, 'server', %!error.Message%, %!error.Key%, %!error.StatusCode%, %!error.Goal.GoalName%,
        %!error.Goal.RelativeGoalPath%, %!error.Step.Text%, %!error.Step.LineNumber%,
        %request!Path%, %Identity%, %request!User-Agent%)
    on conflict(fingerprint) do update set count = count + 1, last_seen = CURRENT_TIMESTAMP,
        url = excluded.url, identity = excluded.identity, user_agent = excluded.user_agent,
        status = case when status = 'fixed' then 'returned' else status end
    on error ignore
- select id from errors where fingerprint = %fingerprint%, return 1 row, write to %errorRef%

JsonError
- write out {"error": "Something went wrong. It has been recorded.", "status": %!error.StatusCode%}, status code %!error.StatusCode%

PageError
- if %!error.StatusCode% == 415 or %!error.Key% == "NoRouteNoFile" then call NotFound, else call DefaultError

NotFound
- [ui] render template "/errors/error404.html", status code: 404

DefaultError
- if file "/errors/error%!error.StatusCode%.html" exists then RenderCustomStatusError, else DefaultErrorStatus

RenderCustomStatusError
- [ui] render template "/errors/error%!error.StatusCode%.html", status code: %!error.StatusCode%

DefaultErrorStatus
- set %key% = %!error.Key%, %message% = %!error.Message%
- set %showErrorRef% = false
- if %user.role% contains 'admin' then
    - set %showErrorRef% = true
- if %user.role% contains 'sysadmin' then call goal ShowDetailError
- [ui] render template "/errors/error.html", status code: %!error.StatusCode%

ShowDetailError
- set %errorInfo% = %!error%
- set %source% = %!Event.SourceStep%
- set %callstackInfo% = %!callstack%
- [ui] render template "/errors/errorDetails.html" without main layout, write to %details%
```

```html
<!-- errors/error.html -->
<div class="error">
{{ if key == "UserDefinedError" }}
  <p class="message">{{ message | html.escape }}</p>
{{ else }}
  <p class="message">Something went wrong. It has been recorded.</p>
{{ end }}
{{ if showErrorRef }}
  <p class="ref">Error reference: {{ errorRef | html.escape }}</p>
{{ end }}
{{ details }}
</div>
```

```html
<!-- errors/errorDetails.html -->
<div class="error-details">
  <h2>{{ errorInfo.Key | html.escape }} ({{ errorInfo.StatusCode }}): {{ errorInfo.Message | html.escape }}</h2>
  <p>{{ source.Goal.GoalName | html.escape }} in {{ source.Goal.RelativeGoalPath | html.escape }}:{{ source.LineNumber }}</p>
  <pre>{{ source.Text | html.escape }}</pre>
  <h3>Error</h3>
  <pre>{{ errorInfo | json true | html.escape }}</pre>
  <h3>Call stack</h3>
  <pre>{{ callstackInfo | json true | html.escape }}</pre>
</div>
```

`errors/error404.html` is the page for a url that does not exist. Any `errors/error<status>.html` (`error503.html`) replaces
`error.html` for that status code.

### What each request gets

A route whose step fails (status 503), and a url that does not exist:

| request | status | body |
|---|---|---|
| page load, visitor | 503 | `error.html` in the layout: "Something went wrong. It has been recorded." |
| page load, admin | 503 | the same, and `Error reference: 1575523994428465152` (the row's id) |
| page load, developer (`sysadmin`) | 503 | the same, and `errorDetails.html`: `Broken in /order/Broken.goal:2`, the step, the error and the call stack |
| `window.plang.fetch` or `callGoal` | 503 | `onFailure` gets `{"kind": "render", "status": 503, "actions": ["replace"], "content": "<div class=\"error\">…"}`. Without `onFailure` the client puts it in `#main` |
| JSON route, `Accept: application/json` | 503 | `{"error": "Something went wrong. It has been recorded.", "status": 503}` |
| JSON route, no `Accept` | 503 | the error page as one JSON string. A client of a JSON route must send `Accept: application/json` |
| a missing url | 404 | `error404.html` in the layout. Not recorded |

- **`on app error`** runs for an error nothing else handled: in a route, in `Start`, and in a goal the scheduler runs (no url in the
  row).
- **`throw error "Not allowed", status code 403` in a goal** is the goal's answer to the visitor, not a fault. It reaches the
  handler with the key `UserDefinedError`, which is not recorded, and `error.html` shows its message: a page load gets 403 and
  "Not allowed" in the layout. `window.plang.fetch` gets it in `onFailure`; without `onFailure` the client shows the message in
  a box in `<div id="plangVillur"></div>` when the layout has one.
- **Who is asking.** `%user%` is loaded in `on goal begin request` and is there in the handler. `contains 'admin'` is also true for
  a role `admin,sysadmin`.
- **Escape every value** with `| html.escape`, after `json` too: `json` does not escape. `{{ x | json true }}` wrote `<b>x</b>`
  from a step's text into the page as html; `{{ x | json true | html.escape }}` writes `&lt;b&gt;x&lt;/b&gt;`.
- **Where it failed.** `%!Event.SourceStep%` in the handler is the step that failed (`.Text`, `.LineNumber`, `.Goal.GoalName`,
  `.Goal.RelativeGoalPath`); for a missing url it is the `on app error` line itself. `%!callstack%` holds the request and the handler's
  own goals only, because the goals that failed have ended. `%!error%` written as text is empty, so the template takes it whole
  through `json`.
- **`without main layout`** on the details: `render template` puts the layout around what it renders, even into a variable.
- `on error ignore` on the insert: an error inside the handler must not replace the one being recorded. Check the table once after
  a deploy, since a broken handler is silent.

A route that fails gives one row however often it fails:

| source | goal_path | line | step | message | url | count | status |
|---|---|---|---|---|---|---|---|
| server | /order/Broken.goal | 2 | `get http://localhost:1/rates?q=<b>x</b>, timeout 3 seconds, write to %rates%` | Connection refused (localhost:1) … | /order/broken | 43 | open |
| server | /job/Rates.goal | 2 | `get http://localhost:1/feed, timeout 2 seconds, write to %feed%` | Connection refused (localhost:1) … | | 4 | open |

## 3. Browser errors

The plang js client already reports them. It listens to `error` and `unhandledrejection` on the page, and reports its own failures
(a request that could not be sent, a broken poll message). It posts them to `/js-error` as JSON:

```json
{"errors": [{"type": "error", "message": "TypeError: Cannot read properties of null (reading 'total')",
             "stack": "TypeError: …\n    at http://localhost:8203/:20:58", "href": "http://localhost:8203/", "count": 1, "origin": "ours"}],
 "path": "/"}
```

The app only adds the route. Without it the posts get a 404 and nothing is kept.

```plang
/ in the web server's on start
- add route "/js-error", POST, call /error/Report
```

```plang
/ error/Report.goal
Report
- go through %request.body.errors%, call SaveJsError

SaveJsError
- set %fingerprint% = "js|%item.type%|%item.message%"
- insert into errors (fingerprint, source, message, key, url, identity, user_agent, stack)
    values (%fingerprint%, 'js', %item.message%, %item.type%, %item.href%, %Identity%, %request!User-Agent%, %item.stack%)
    on conflict(fingerprint) do update set count = count + 1, last_seen = CURRENT_TIMESTAMP,
        url = excluded.url, identity = excluded.identity, user_agent = excluded.user_agent, stack = excluded.stack,
        status = case when status = 'fixed' then 'returned' else status end
```

- The client signs each report like its other requests, so `%Identity%` is the visitor and the body has `"signed": true`. When
  signing itself fails (no key yet, a crypto error, the server rejects the signature) it still sends, unsigned, with
  `"signed": false` and `"signError"`: those are the errors nobody would see otherwise. Store `signError` too if you want them
  apart.
- **Floods.** The client sends the first sighting of a message at once and gathers repeats for 5 seconds into one post, and stops
  after 20 different messages on a page. A page that threw the same error 50 times sent two posts. The upsert is the guarantee on the
  server side: one row per fingerprint, whatever a client sends. `count` is the number of reports, not of throws.
- **Do not report a failed request again.** When a route fails, the server has its row already. The client does not post an error
  answer to `/js-error` either. `onFailure` gets the rendered error page (§2); use it, or say it your own way:

```html
<script>
window.plang.fetch('/order/broken', {
    method: 'GET',
    onFailure: async function (message) {
        document.querySelector('#status').textContent = 'That did not work. Try again in a moment.';
    }
});
</script>
```

## 4. Reading it

```plang
/ error/Open.goal
Open
- select status, source, count, message, goal_path, line, step, url, note, first_seen, last_seen
    from errors where status in ('open', 'returned')
    order by status = 'returned' desc, last_seen desc
    write to %errors%
```

`returned` comes first: an error that was marked fixed and happened again. After a fix, mark it, so a return shows:

```sql
update errors set status = 'fixed', note = 'fixed in 4c26393' where fingerprint = '…'
```

## Why this way

- **One row per error, not per hit.** An insert per hit buries a new error under a thousand copies of an old one, and a loop can
  write thousands of rows a minute.
- **The fingerprint has the message.** Two different failures on the same step are two rows. A message that holds an id
  (`Order 42 not found`) is a row per id; write such messages without the id when that is noise.
- **The handler answers; it does not throw.** A handler that ends with `throw %!error%` sends the status code and an empty body.
- **`on error in step` is not the place.** A goal bound there handles the error, and the app event never sees it.

See also: [Error handling](../ErrorHandler.md), [Events](../Events.md), [Web server](../Webserver.md),
[Finding slow goals](./SlowGoals.md), [Calling an outside api](./ExternalApiCalls.md).
