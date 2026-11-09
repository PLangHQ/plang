# plang JS client

The browser side of PLang: identity and signing, the poll, navigation and history, and the handlers
that apply what the server sends (render, execute, error, ask).

`src/` is the only place to change it. `dist/` is the build output and is committed so an app can copy it.

## Build

```
cd js/plang
npm install
npm run build
```

## Use in an app

1. Copy `dist/` to the app's `js/plang/dist/` and load `/js/plang/dist/main.js` as a module.
2. Copy `dist/plang-sw.js` to the root of the site, so it is served as `/plang-sw.js`. It has to be at
   the root, that is what gives it the whole site as its scope. If it is missing the client works
   as before, only without the signed navigation below.

Never edit the copied files in an app: the next copy overwrites them. Change `src/` here, build, and copy again.

## Signed navigation (`plang-sw.js`)

The identity lives in the browser, so a plain page load, a link from a mail or Back on a phone, carries
none, and the server cannot tell who is asking. The service worker answers each page navigation
itself: it reads the signing key from IndexedDB, signs the request the way `Plang.fetch` does, and
returns the server's answer. The server sees `%Identity%` on the first request, and the page is for
the right person at the address that was asked for, with no shell, redirect or wait for the poll.

- It is registered from `Plang.init()` once the key store is resolved, and only when the key is in
  IndexedDB (a worker cannot read localStorage or memory). A browser that cannot use it keeps the
  plain navigation, and a registration left from an earlier visit is removed.
- A visitor with no key yet, a failed signature or an offline server falls through to the browser's
  own navigation, so nothing is worse than before.
- Only `GET` navigations are signed. Forms and `fetch` are signed by the page as always.

### Updating a tab that was left open

The client asks the browser to check `plang-sw.js` for a change every five minutes while the tab is
visible, and when a hidden tab becomes visible. A changed file takes over open tabs at once, and the
tab then gets the new scripts and styles instead of running old ones against the new server:

- the next link the visitor follows is a full page load instead of a swap inside the old page, so
  the new version arrives when they move on, with nothing lost;
- a tab in the background is reloaded, unless the visitor has typed something into the page that is
  still there.

An app makes every deploy visible to this by changing its copy of `plang-sw.js`, for example by
appending the commit as a comment: `echo "// build $(git rev-parse HEAD)" >> plang-sw.js`.

## Texts

Texts the client shows itself are in `src/i18n/` (`is.ts`, `en.ts`), chosen from the browser language.
