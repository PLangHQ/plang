# Identity in the browser

[Identity.md](./Identity.md) explains what an identity is and what you can do with it. This page is
about where it comes from when the user is sitting in a browser, because that one fact decides how
a plang web app is put together, and getting it wrong produces bugs that look like everything else.

## The one sentence

**The browser holds the identity, and only requests made by the plang js client carry it.**

The keypair lives in the visitor's browser. The plang js client signs every request it makes with
it, and the server turns that signature into `%Identity%`. Nothing else does. A request the browser
makes on its own is anonymous, no matter that it goes to the same server, in the same tab, a
millisecond later.

Requests that **carry** the identity:

- the poll the client opens when the page loads (`?plang.poll=1`)
- navigation inside the app, that is a click on an `<a href>` the client intercepts
- a form the client submits
- an `ask user` callback

Requests that **do not**:

- the very first load of a url, typed, bookmarked, or followed from another site
- `<img src>`, `<script src>`, `<link href>`, a background `fetch` you wrote yourself
- a link with `target="_blank"`, a download, anything the browser opens as a new document
- curl, wget, a webhook from a payment provider or a mail service
- a crawler

## What follows from it

### 1. The first document can never be personal

The html the browser gets on the first hit is the same for everyone, because at that moment the
server has no idea who is asking. You cannot render a name, a cart count, a role or a private page
into it. Anything personal has to arrive **after** the client connects.

The clean way is to let **the page ask for itself again** once it has an identity. A `before each
goal` event decides, and the shell it renders carries one line:

```plang
Gate
- if %Identity% is empty then
    - [ui] set "/ui/layoutBare.html" as default layout, default render variable "main"
    - [ui] render "/ui/pages/shell.html", navigate
    - end goal, and previous
```

```html
<!-- shell.html: whatever anonymous visitors should see, plus the re-request -->
<script>
addEventListener('plang:ready', function (e) {
    e.detail.fetch(location.pathname + location.search, { method: 'GET', headers: [{ key: 'X-Plang-Layout', value: 'body' }] });
}, { once: true });
</script>
```

`plang:ready` fires when the client has its keys and has been answered once. The fetch is a signed
request to the **same url**, so the goal runs again, now with `%Identity%` and `%user%` known. The
header `X-Plang-Layout: body` asks the server to render the layout around the content as it would
for a plain GET and to send it as a render message that replaces `body`. Header, footer, menus and
the page arrive in one answer, drawn for the person who is actually there.

A goal can ask for the same from its side, `- [ui] render "page.html", navigate, redraw the whole
body inside the layout` (`LayoutTarget`). Use it when a goal switches layout, for instance a bare
kiosk screen next to the normal site: in-app navigation only swaps `#main`, so the old header would
stay unless the whole body is redrawn.

What you get for free: the layout can hold the header, footer and role based menus **statically**,
because only identified requests ever render it. No slots, no painting from the poll, no separate
landing template.

Three things to get right, each of which was a real bug:

- **Do not bind the gate to the layout event's own folder.** If the goal that sets the layout is in
  `/pages/` and the gate is bound to `/pages/.*`, the gate runs before the layout event as well and
  the shell is rendered twice. Keep both in `events/`.
- **`end goal, and previous`**, not `end goal`. The plain form ends only the event goal and the
  route goal runs anyway.
- **A route that shows an `ask user` form needs `get and post`.** The form posts back to the url it
  was rendered from.

### 2. There is no sign in page

A person is not "logged in", their browser either is or is not linked to a user row. Sign in is a
step inside whatever goal needs it, not a page you send people to and not a `returnTo` parameter:

```plang
Checkout
- if %user.id% is empty then call goal SignInStep, else call goal AfterSignIn
```

The identity is created on first contact and linked to a user when the person proves who they are.
Roles live on the user row. See [Identity.md](./Identity.md) for the queries.

### 3. Anything the browser fetches by itself must be public

This is the one that catches people twice. A private file cannot be served from a guarded route and
put in an `<img src>`, because the image request is a plain browser request with no identity and
the guard rejects it. The image is simply broken and nothing in the log explains why.

Two ways out:

- **Send the bytes with the page.** Store or read the file as a `data:` url and render it into the
  html that the identity-carrying request produced. Nothing private is ever on a public url.
- **Give it an unguessable url** if the payload is large or the page would get too heavy. This is a
  capability url: possession of the link is the permission, so treat it as a secret.

The same applies to a webhook. A payment provider or a mail service posting to your app has no
identity, so that route has to authenticate itself some other way, a shared secret in the query
string or a signature you verify.

### 4. You cannot test a page with curl

curl has no keypair, so it is the anonymous case by definition. Everything behind
`- if %user.id% is empty` will behave for curl exactly as it does for a stranger, which is usually
not what you are trying to test. Use a real browser, and remember that a hard navigation to a url
in that browser is also anonymous, only in-app navigation carries the identity.

Two practical notes for automated browser tests:

- A headless browser is detected as a crawler by its user agent and gets no identity row at all,
  which then fails at the first step that expects one. Set a normal desktop user agent.
- To move around in-app from a test, go through the client: `window.plang.fetch('/the/path',
  { method: 'GET' })` is exactly what a click on an in-app link does, or click a real link on the
  page. `page.goto` is a hard navigation and drops you back to anonymous.

With plang's own browser, `PLang.Modules.WebCrawlerModule`, the whole recipe is three steps. The
profile folder keeps the keypair, so the browser is the same visitor every time and can be linked
to a user once; without a profile every start is a stranger.

```plang
- [webcrawler] navigate to "http://localhost:8080/", headless: true, profileName: "/.db/browser", wait after 3000 ms
- [webcrawler] evaluate javascript "() => window.plang.fetch('/admin/orders/42', { method: 'GET' })" in the page
- [webcrawler] take screenshot of website, save to "/tmp/orders.png", overwrite
```

The first step is the anonymous load, and its wait is the one wait there is: the client needs a
moment to connect and be answered. The second is the signed navigation; `plang.fetch` returns a
promise that resolves after the answer has been applied to the page, so when the step returns
the page is there and the next step can act on it at once. Navigating straight to
`/admin/orders/42` in the first step gives the anonymous version of that page, whatever the
guard does with strangers, and no amount of waiting changes it.

One browser profile is one visitor, and chromium locks the profile folder while it is open. Two
processes using the same profile at the same time do not share it: the second one starts with an
empty profile, a new keypair, and is a stranger.

## The mental model

Think of the first request as fetching an empty stage, and the first signed request as the actor walking on. Every
personal thing the user sees is put there by a request the client made. If you find yourself asking
"why does the server not know who this is", the answer is nearly always that the browser, not the
client, made the request.
