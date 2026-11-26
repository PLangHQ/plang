# First page load

The browser holds the visitor's identity. A new visitor's first page load has none: the key is made by the plang js client
after the page has loaded. A returning browser has it, and its page load is signed. The pattern: the first GET always returns
the whole page from the server. With no identity the parts that depend on the visitor are in their anonymous state (a sign in
link where the name would be); when the client connects, the poll renders those parts into their elements. Never send an
empty page that a script fills in.

## 1. The part that depends on the visitor is its own template

```html
<!-- user/who.html -->
{{ if user.name }}Hi {{ user.name | html.escape }}{{ else }}<a href="/user/signin">Sign in</a>{{ end }}
```

```html
<!-- ui/layout.html, the frame -->
<nav><a href="/">Home</a> <a href="/product">Products</a> <span id="who">{{ who }}</span></nav>
<main id="main">{{ main }}</main>
```

With no identity `%user%` is empty and `user.name` is empty, so the sign in link shows. The name was typed by the visitor, so it
goes through `html.escape`.

## 2. The server renders it on every request and on the poll

```plang
Start
- [ui] set "/ui/layout.html" as default layout, default render variable "main"
- start webserver, port: 8080,
    on start call AddRoutes,
    on poll start call OnPollStart,
    on poll refresh call OnPollRefresh,
    on goal begin request call OnRequest

OnRequest
- if %Identity% is not empty then
    - call goal /user/LoadUser
- [ui] render "/user/who.html" without layout, write to %who%

OnPollStart
- call goal /user/LoadUser
- [ui] render "/user/who.html", target: "#who"

OnPollRefresh
- call goal /user/LoadUser
- [ui] render "/user/who.html", target: "#who"
```

- `OnRequest` runs before every route. With an identity it loads the user, and either way it renders `who.html` into `%who%`,
  which the layout shows. The first GET is complete: curl, always the new visitor, gets

  ```html
  <nav><a href="/">Home</a> <a href="/product">Products</a> <span id="who"><a href="/user/signin">Sign in</a>
  </span></nav>
  <main id="main"><h1>Welcome</h1>
  ...
  ```

- The client connects on every page load (`on poll start`) and when it connects again (`on poll refresh`), always with
  `%Identity%`. The poll renders the same template into `#who`, the same way a goal updates a part
  ([Partial update](./PartialUpdate.md)).
- `LoadUser` finds or creates the row for the identity ([Loading the user](./LoadUser.md)).

What a browser sees, measured with a headless browser:

| visitor | first GET renders `#who` as | after the poll |
|---|---|---|
| new, fresh profile | Sign in | Sign in: a new identity has no user yet |
| returning, same profile, signed in as Anna | Hi Anna: `plang-sw.js` signed the GET | Hi Anna |
| returning, but the GET was not signed (service worker not installed yet, a hard reload) | Sign in | Hi Anna |

The poll is what corrects the third row. A page where a new identity already owns something (a cart made on the poll) shows
it the same way.

## 3. Writes keyed on the identity happen on the poll

Recording where a visitor came from, counting a visit per person, linking a row to the identity: do it in `OnPollStart`, not
in the route. A new visitor's GET has no identity to key it on. A returning browser's page load reaches the server twice in
Chromium, once as the plain navigation without an identity and once signed by `plang-sw.js`, so a route goal that writes would
write twice, once without the identity.

The poll carries what it needs in its query: `landingPath` (the page the visitor landed on), `landingReferrer` (the referring
site, only when it is another site), and `visitSource` when the page defines a global `visitSource`. The layout sets it from
the landing url:

```html
{{ if request.query.src }}<script>var visitSource = {{ request.query.src | object.to_json }};</script>{{ end }}
<script type="module" src="/js/plang/dist/v10/main.js"></script>
```

```plang
OnPollStart
- call goal /user/LoadUser
- if %request.query.visitSource% is not empty then
    - update identities set source=%request.query.visitSource% where identity=%Identity% and source is null, dont validate affected rows
- [ui] render "/user/who.html", target: "#who"
```

A fresh browser opening `/?src=offer` gets its identity row with `source = offer`. `object.to_json` writes the value
as a javascript string, so a quote in the url cannot end the script.

## Why this way

- **No shell.** An empty page that asks for itself again once the client has a key shows nothing to a crawler, to a link
  preview, or to a visitor whose script fails, and every new visitor waits for two requests. The whole page with a sign in link
  is right for all of them.
- **No identity guard on a public page.** A page everyone may see is rendered for everyone; only its personal parts change.
- **One template, two uses.** `who.html` is rendered into the layout on the GET and into `#who` on the poll, so the two can
  never differ.

See also: [Partial update](./PartialUpdate.md), [Page layout](./PageLayout.md),
[Identity in the browser](../IdentityInTheBrowser.md), [Web server](../Webserver.md).
