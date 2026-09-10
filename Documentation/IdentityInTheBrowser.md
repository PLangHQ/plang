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
into it. Anything personal has to arrive **after** the client connects, and the connection it makes
is the poll.

So the landing route renders an empty shell, and the poll paints it:

```plang
Start
- start webserver, port: %port%, host: %host%,
    on poll start call OnPollStart
    on poll refresh call OnPollRefresh

OnPollStart
- call goal LoadUser
- if %request.query.landingPath% == "/" then call PaintLanding

OnPollRefresh
- call goal LoadUser
- if %request.query.landingPath% == "/" then call PaintLanding

PaintLanding
- if %user.role% contains "admin" then call RenderSite, else call RenderInProgress
```

Four things to get right here, each of which is a real bug I shipped:

- **Render with `replace`, never `append`.** The poll reconnects, and every reconnect runs the hook
  again. With `append` the same block is added over and over: fifty copies, a 22 KB document and
  broken javascript. A render driven by the poll has to be idempotent.
- **Handle `OnPollRefresh` as well as `OnPollStart`.** A tab that is already open reconnects with
  `?plang.poll=1&refresh=1` and only fires the refresh hook. Paint in one and not the other and
  that user sits and looks at an empty page.
- **Check `landingPath` before painting.** The poll carries the path the client is on. Without the
  check, a reconnect paints the landing page over whatever the user has since navigated to.
- **Do not put the gate in an error handler.** Throwing an error and rendering a page from the
  error event produces an empty response body.

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
- To move around in-app from a test, append an `<a href>` to the page and click it, so the client
  intercepts it. `page.goto` is a hard navigation and drops you back to anonymous.

## The mental model

Think of the first request as fetching an empty stage, and the poll as the actor walking on. Every
personal thing the user sees is put there by a request the client made. If you find yourself asking
"why does the server not know who this is", the answer is nearly always that the browser, not the
client, made the request.
