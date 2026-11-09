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

Copy `dist/` to the app's `js/plang/dist/` and load `/js/plang/dist/main.js` as a module.
Never edit the copied `dist/` in an app: the next copy overwrites it. Change `src/` here, build, and copy again.

## Texts

Texts the client shows itself are in `src/i18n/` (`is.ts`, `en.ts`), chosen from the browser language.
