# Web server

The web server: its events, which requests carry the visitor's identity, calling a route from a script, and who the visitor
is. How the pages themselves are made is in the patterns: [Page layout](./patterns/PageLayout.md),
[Partial update](./patterns/PartialUpdate.md) and [First page load](./patterns/FirstPageLoad.md).

A web page talks to a plang app through the plang js client. It holds the visitor's identity, signs every request it makes,
keeps a connection open so the server can send to the page, and applies what the server sends. Do not write your own fetch
and signing code: a request the client does not make carries no identity. Why that matters is in
[Identity in the browser](./IdentityInTheBrowser.md). A first app with routes and a database is the
[Todo web service tutorial](./Todo_webservice.md).

## 1. Pages: the client and the layout

Copy the client from the plang release into the app, load it in one layout, set the layout once in `Start`, and let each page
goal `[ui] render "...", navigate`. The steps are in [Page layout](./patterns/PageLayout.md).

## 2. Start the web server

```plang
Start
- [ui] set "/ui/layout.html" as default layout, default render variable "main"
- start webserver, port: 8080,
    on start call AddRoutes,
    on poll start call LoadUser,
    on poll refresh call LoadUser,
    on goal begin request call OnRequest

OnRequest
- if %Identity% is not empty then
    - call goal LoadUser
```

- The client keeps one connection open (`/?plang.poll=1`), and the server can send to the page on it. `on poll start` runs
  when a page connects, `on poll refresh` when it connects again.
- `on goal begin request` runs before every route. Here it and the poll load the visitor (see 6), so every page knows
  `%user%`.
- `on request begin` runs before every request, files included: `/js/plang/dist/v12/main.js` and `/plang-sw.js` too. Put a
  check on `%Identity%` in `on goal begin request` or in the route. A check in `on request begin` that refuses anonymous
  requests also refuses the client's own files, and the client never loads.

## 3. Routes

```plang
AddRoutes
- add route "/", call /home/Home
- add route "/user/me", call /user/Me
- add route "/user/whoami", content type application/json, call /user/WhoAmI
```

A small app adds all its routes in one `AddRoutes` goal in `Start.goal`; a bigger one splits them by area into `routes/`
([Page layout](./patterns/PageLayout.md#3-set-the-layout-once-in-start)). The goal a route calls sits in the folder of the thing it
is about ([App folders](./patterns/AppFolders.md)). A route with no method takes only GET; a route that `plang.callGoal` calls
must say `POST`. An id goes in the path with its type, `add route "/product/detail/%id%(number)", POST, call /product/Detail`,
and the goal reads `%id%` ([Partial update](./patterns/PartialUpdate.md)). An api route answers plain JSON when the route says `content type application/json`:

```plang
/ user/WhoAmI.goal
WhoAmI
- write out {"identity": "%Identity%"}
```

## 4. Which requests carry the identity

Measured with a headless browser and a fresh profile:

| request | `%Identity%` in the goal |
|---|---|
| the first load of a page by a new visitor (no key yet) | empty |
| the client's connection (`/?plang.poll=1`, `on poll start`) right after it | set: the client made the key |
| a link in the app, a form, `window.plang.fetch(url, { method: 'GET' })` | set |
| a later load of any url in the same browser, typed or from a link in a mail | set: `plang-sw.js` signs it, and the server renders the page for that person at once. Chromium also sends the plain navigation beside it, without the identity, so the route runs twice |
| your own `fetch('/user/whoami')`, `<img src>`, `<script src>` | empty |

`window.plang.MyIdentity()` gives the identity the browser holds.

## Calling a route from a script

`window.plang.fetch(url, options)` is signed, and what the route sends is applied to the page. To use the answer in a script
instead, pass `onSuccess` (and `onFailure`); the promise itself resolves to nothing. The route's output is in
`message.content`, as text:

```html
<script>
window.plang.fetch('/user/whoami', {
    method: 'GET',
    onSuccess: async function (message) {
        const answer = JSON.parse(message.content);   // {"identity": "..."}
        document.querySelector('#who').textContent = answer.identity;
    },
    onFailure: async function (message) {
        console.warn(message.status, message.content);
    }
});
</script>
```

## 5. A page that needs the identity, for a new visitor

The first GET always returns the whole page: with no identity, the parts that depend on the visitor are in their anonymous
state, and the poll renders them into their elements when the client connects. See [First page load](./patterns/FirstPageLoad.md).

## 6. One browser, one identity

Each browser makes its own key, so a phone and a laptop are two identities, and a cleared browser is a new one. The app
keeps a row per identity, points it at the user when the person proves who they are in that browser, and loads that user
on every request and poll, from a cache. The pattern, with the three cases a goal has: [Load user](./patterns/LoadUser.md).
See also [Identity](./Identity.md).

## 7. Test it with a real browser

curl has no key, so it is always the new visitor. plang's own browser keeps the key in a profile folder:

```plang
Test
- [webcrawler] navigate to "http://localhost:8080/", headless: true, profileName: "/.db/browser", wait after 4000 ms
- [webcrawler] run javascript "() => window.plang.fetch('/user/me', { method: 'GET' }).then(() => document.querySelector('#main').innerText)" in the browser, write to %me%
- write out %me%
- [webcrawler] close browser
```

The first step is the new visitor; the wait lets the client make its key and connect. Every request after that in the same
profile is signed.

If the first step fails with "Executable doesn't exist at .../chrome-headless-shell", the browser this runtime needs is not
installed. Install it with the Playwright that ships with plang, from the runtime's folder:

```bash
.playwright/node/linux-x64/node .playwright/package/cli.js install chromium-headless-shell
```

Set `PLAYWRIGHT_BROWSERS_PATH` to a folder you can write to, for both the install and the run, when the default one is not
yours.
