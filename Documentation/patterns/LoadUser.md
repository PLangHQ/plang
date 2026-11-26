# Loading the user

Every request in a web app needs to know who is asking. plang gives you `%Identity%`, the key of the browser, not the person.
This page is the pattern for turning it into `%user%`: one row per identity, linked to a user once the person proves who they
are, loaded by one goal, `LoadUser`, that is cached per identity and called before every route and from the poll.

## 1. One browser, one identity

Each browser makes its own key. A phone and a laptop are two identities, and a cleared browser is a new one. So the app keeps a
row per identity and points it at a user when the person proves who they are in that browser, for example with a code sent by
mail. Roles live on the user.

```plang
Setup
- create table users, columns: name(string, not null), admin(bool, default false), sysadmin(bool, default false)
- create table identities, columns: identity(string, not null), userAgent(string), userId(int)
```

## 2. LoadUser

```plang
/ user/LoadUser.goal
/ %user% for this identity, from the cache when it is there.
LoadUser
- get cache "User_%Identity%", write to %user%
- if %user% is empty then
    - call goal LoadUserFromDb

/ The first time an identity is seen it gets a row. Then it is cached.
LoadUserFromDb
- call goal FindUser
- if %user% is empty then
    - insert into identities, identity=%Identity%, userAgent=%request!User-Agent%, write to %identityId%
    - call goal FindUser
- set cache "User_%Identity%" = %user%, for 20 min from last access

FindUser
- select i.id as identityId, i.userId, u.name, u.admin, u.sysadmin from identities i left join users u on u.id = i.userId where i.identity = %Identity%, return 1 row, write to %user%
```

- The cache key holds the identity, so each browser has its own entry. `for 20 min from last access` keeps an active visitor
  in the cache and drops one who left.
- `left join`: an identity with no user still gives a row, with `identityId` set and `userId` empty.
- A goal that `LoadUser` calls shares its variables, so `%user%` set in `FindUser` is there for the route.

Call it before every route and when the page connects:

```plang
Start
- [ui] set "/ui/layout.html" as default layout, default render variable "main"
- start webserver, port: 8080,
    on start call AddRoutes,
    on poll start call /user/LoadUser,
    on poll refresh call /user/LoadUser,
    on goal begin request call OnRequest

OnRequest
- if %Identity% is not empty then
    - call goal /user/LoadUser
```

- `on goal begin request` runs before every route. A new visitor's first page load has no `%Identity%`, so it is skipped.
- `on poll start` runs when the plang client connects, which is the first request of a new visitor that has an identity. That
  is where its `identities` row is made. `on poll refresh` runs when the page connects again.

In a test with four browsers and 24 page loads, the database was read 7 times: once when each browser first had an identity,
and once after each was linked to a user. Every other request took `%user%` from the cache.

## 3. The three cases

A goal that uses the user has three cases, and they are different:

| case | what the goal sees | what it means |
|---|---|---|
| no identity | `%Identity%` empty, `%user%` empty | a new visitor's first load, or a request the client did not make (curl, `<img src>`) |
| identity, no user | `%user.identityId%` set, `%user.userId%` empty | a browser nobody has linked to a person yet |
| known user | `%user.userId%` set, roles on `%user%` | the person has proven who they are in this browser |

A public page renders in all three. A page that needs a person asks for the proof as a step in that page, there is no sign in
page. See [Identity in the browser](../IdentityInTheBrowser.md).

## 4. Linking a user

When the person has proven who they are, point the identity at the user and drop the cached entry:

```plang
- update identities set userId = %userId% where identity = %Identity%
- remove cache "User_%Identity%"
```

The next request loads the user from the database. Do the same `remove cache` when a user's roles change, otherwise the old
roles stay for up to 20 minutes.

## Why this way

- **Without the cache** every request, and every poll, reads two tables. With it, a returning request costs one cache lookup.
- **Without the identities table** a user could only have one browser, and a visitor who has not proven anything could not be
  remembered (a cart, a visit source).
- **Do not load the user in `on request begin`.** It runs for every file too, the client's own scripts included.

## The mistake that keeps coming back

Steps added in `LoadUserFromDb`, or after an early `end goal` on the cache hit, run only when the cache is cold: about once per
visit per browser, not once per request. A "last access" update or an access log written there counts visits, and a check
written there is skipped on most requests. Put work that must run on every request in `OnRequest`, after the call to
`LoadUser`. Put work that should run once per visit in `LoadUserFromDb`, on purpose.

See also: [Web server](../Webserver.md), [Identity in the browser](../IdentityInTheBrowser.md), [Identity](../Identity.md),
[Folder authentication](./FolderAuthentication.md), [Caching](../CachingHandler.md).
