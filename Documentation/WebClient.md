# The plang client in a web app

A web page talks to a plang app through the plang js client. It holds the visitor's identity, signs every request it makes,
keeps a connection open so the server can send to the page, and applies what the server sends. Do not write your own fetch
and signing code: a request the client does not make carries no identity. Why that matters is in
[Identity in the browser](./IdentityInTheBrowser.md); this page is how to wire it up.

## 1. Put the client in the app

The client ships with plang in `js/plang/dist/`. Copy two things into the app:

```
myapp/
  plang-sw.js              <- js/plang/dist/plang-sw.js, at the site root
  js/plang/dist/...        <- the whole js/plang/dist/ folder
  ui/default/layout.html
  Start.goal
```

- `plang-sw.js` must be at the root, so it is served as `/plang-sw.js` and covers the whole site. It signs page loads (see 4).
- Load the client with the number in `js/plang/VERSION` in the path: `/js/plang/dist/v8/main.js`. The web server finds
  `js/plang/dist/main.js` for it. A new number is a new address, so a browser never runs a cached old module next to new ones.
- Do not edit the copied files. Copy them again when plang is updated, and change a line in `plang-sw.js` on every deploy
  (`// build <commit>`), so open tabs pick up the new version.

## 2. The layout loads the client

```html
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>My app</title>
<script type="module" src="/js/plang/dist/v8/main.js"></script>
</head>
<body>
<nav><a href="/">Home</a> <a href="/me">Me</a></nav>
<main id="main">{{ main }}</main>
</body>
</html>
```

`{{ main }}` is where a page's content goes. Links inside the app are followed by the client, signed, and only `#main` is
replaced.

## 3. Start the web server

```plang
Start
- [ui] set "/ui/default/layout.html" as default layout, default render variable "main"
- start webserver, port: 8080, on start call AddRoutes

AddRoutes
- add route "/", call /pages/Home
- add route "/me", call /pages/Me
- add route "/api/whoami", content type application/json, call /api/WhoAmI
```

The layout is set once, for the whole app. The client keeps one connection open (`/?plang.poll=1`), and the server can
send to the page on it. To run a goal when a page connects, add `on poll start call X` to `start webserver`; `on poll
refresh call Y` runs when it connects again. Both are optional.

A page renders into the layout:

```plang
Home
- set %who% = %Identity%
- [ui] render "/ui/home.html", navigate
```

An api route answers plain JSON when the route says `content type application/json`:

```plang
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
| a later load of any url in the same browser, typed or from a link in a mail | set: `plang-sw.js` signs it, and the server renders the page for that person at once |
| your own `fetch('/api/whoami')`, `<img src>`, `<script src>` | empty |

`window.plang.MyIdentity()` gives the identity the browser holds.

## 5. A page that needs the identity, for a new visitor

Only a new visitor's first load is anonymous. A page that needs an identity sends a small shell then, and the shell asks
for the same page again once the client is ready:

```plang
Me
- if %Identity% is empty then
    - call goal Shell
- set %who% = %Identity%
- [ui] render "/ui/me.html", navigate

Shell
- [ui] render "/ui/shell.html", navigate
- end goal and 2 levels up
```

```html
<!-- ui/shell.html -->
<p>loading</p>
<script>
addEventListener('plang:ready', function (e) {
    e.detail.fetch(location.pathname + location.search, { method: 'GET', headers: [{ key: 'X-Plang-Layout', value: 'body' }] });
}, { once: true });
</script>
```

`plang:ready` fires when the client has its key and its connection is answered. `X-Plang-Layout: body` asks for the page
inside the layout, so the whole body is drawn for the person who is there. An admin page redirects to `/` instead of
showing a shell.

## 6. One browser, one identity

Each browser makes its own key, so a phone and a laptop are two identities, and a cleared browser is a new one. A person is
the app's own record: keep a table of identities per user, and add a row when the person proves who they are in the new
browser, for example with a code sent by mail. See [Identity](./Identity.md).

## 7. Test it with a real browser

curl has no key, so it is always the new visitor. plang's own browser keeps the key in a profile folder:

```plang
Test
- [webcrawler] navigate to "http://localhost:8080/", headless: true, profileName: "/.db/browser", wait after 4000 ms
- [webcrawler] run javascript "() => window.plang.fetch('/me', { method: 'GET' }).then(() => document.querySelector('#main').innerText)" in the browser, write to %me%
- write out %me%
- [webcrawler] close browser
```

The first step is the new visitor; the wait lets the client make its key and connect. Every request after that in the same
profile is signed.
