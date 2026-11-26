# Folder authentication

Some pages are only for some people: an admin area, a sysadmin area inside it. This page is the pattern for protecting them:
the restricted goals live in one folder, an `Authenticate.goal` in that folder checks the user, and one event binds it to
every goal in the folder. A request without the right user is redirected to `/`.

It needs `%user%` with its roles on every request: [Loading the user](./LoadUser.md).

## 1. The binding

```plang
/ events/Events.goal
Events
- before each goal in "/admin/.*", call /admin/Authenticate
- before each goal in "/admin/sysadmin/.*", call /admin/sysadmin/Authenticate
```

The pattern is the path of the goal file, not the url. Every goal file in `admin/` and below is guarded, whichever route or
goal calls it.

## 2. The gate

```plang
/ admin/Authenticate.goal
/ Runs before every goal in admin/ and its subfolders. Anyone who is not an admin goes to the front page.
Authenticate
- if %user.admin% is empty or %user.admin% is false then call goal ToFrontPage

/ Ends this goal, the gate and the guarded goal
ToFrontPage
- redirect to "/"
- end goal and 2 levels up
```

```plang
/ admin/sysadmin/Authenticate.goal
Authenticate
- if %user.sysadmin% is empty or %user.sysadmin% is false then call goal ToFrontPage

ToFrontPage
- redirect to "/"
- end goal and 2 levels up
```

- The gate is the first goal of its file, so it is guarded by its own folder's binding too. An event goal is never called for
  itself, so this does not loop.
- `ToFrontPage` is a private goal (not the first in its file), and a `before each goal` binding runs only for public goals.
- `redirect` ends the request, and every goal above it: the guarded goal does not run, nothing it would render is sent, and
  neither does any goal that called it. A new visitor's request, or curl, gets `302` to `/`.
- `end goal and 2 levels up` ends `ToFrontPage`, the gate and the guarded goal.
- No identity, an identity with no user, and a user without the role: `%user.admin%` is empty or false, so all three are sent away.
- The gate is a goal like any other, but no url reaches it unless you add a route for it. Do not.

Write the branch as `then call goal`, not `end goal` inside an indented `if` (see [Writing goals](../WritingGoals.md)), and
name both cases, empty and false. After building, open the gate's `.pr` and check that `ToFrontPage` is under
`goalToCallIfTrue`: a gate built the wrong way round lets everyone in, or no one.

## 3. Permissions cascade with the folders

`admin/sysadmin/` is inside `admin/`, so both bindings match a goal there. The admin gate runs first, then the sysadmin gate.
With a log line in each gate and in the page, a request to `/admin/sysadmin` (the route calls `/admin/sysadmin/Settings`) by a
user with both roles ran, in this order:

1. the admin gate, before `/admin/sysadmin/Settings.goal`
2. the admin gate again, before `/admin/sysadmin/Authenticate.goal`
3. the sysadmin gate
4. the page, `Settings`

The admin gate also runs before the sysadmin gate itself, because that gate is a goal in `admin/` too. It costs nothing: `%user%`
is already loaded. Measured with a real browser for each user:

| user | `/admin` | `/admin/sysadmin` |
|---|---|---|
| admin only | the page | sent to `/` by the sysadmin gate |
| sysadmin only | sent to `/` | sent to `/` by the admin gate, the sysadmin gate never runs |
| admin and sysadmin | the page | the page |
| identity, no user | sent to `/` | sent to `/` |

So a sysadmin must also be an admin. That is a choice, not a rule: an app where a sysadmin need not be an admin puts the
sysadmin pages in their own top level folder. The nesting of the folders is how permissions cascade.

## 4. The guard follows the goal, not the url

A route `/admin/profile` that calls `/user/Profile` is not guarded: the goal is in `user/`. In the test above, a browser with no
user opened `/admin/profile` and got the profile page, and no gate ran. Put the goal a route calls in the folder that matches
who may see it; the url does not protect anything.

## 5. Nothing that runs without a request goes in a guarded folder

A goal that runs without a request (Start, the web server's `on start`, a scheduled job) has the app's own identity, `%Identity%`
is `%MyIdentity%`, and there is no `%user%`. The gate sends it away, and the redirect ends every goal above it, with nothing
logged:

- **Routes.** `admin/Routes.goal`, called from `AddRoutes` to add the admin routes, runs the admin gate at start. With no user
  the redirect ends `AddRoutes` and `Start` with it: the web server never listens, and the log shows only the gate and the
  scheduler. Put route files in `routes/`, outside every guarded folder (see [App folders](./AppFolders.md)).
- **Scheduled jobs.** A job that calls a goal in `admin/` is sent away and ends at that step.

The recommended way: **the goals the scheduler runs live outside guarded folders**, in `job/`, and do not call goals in
`admin/`. What a job and an admin page both need goes in the folder of its thing (`order/`, `product/`), where both can call it.

```plang
Start
- every 5 seconds, call /job/Report
- start scheduler
```

Two other ways work, each with a cost:

- **The gate lets the app itself through:** `if %Identity% != %MyIdentity% and %user.admin% != true then call goal ToFrontPage`.
  A request carries the identity of the browser that signed it, so only the app is `%MyIdentity%`. But every gate must carry the
  clause, a gate written without it stops jobs silently, and the guarded goal then runs with no `%user%`.
- **The job passes a user:** `call goal /admin/Report "user.admin" = true`. The gate then trusts a value the caller sets, and the
  value stays: a later call in the same job, with no user passed, got through too.

## Why this way

- **A check in each page** is forgotten in the next page. The folder is checked for every goal in it, new ones included.
- **A gate in `events/`** guards a folder it does not live in; with the gate in the folder, the folder holds its own rule.
- **Not a 403 and not a shell page.** A request to a restricted page without the right user is redirected to `/`.

See also: [App folders](./AppFolders.md), [Loading the user](./LoadUser.md), [Events](../Events.md),
[Identity in the browser](../IdentityInTheBrowser.md), [Writing goals](../WritingGoals.md).
