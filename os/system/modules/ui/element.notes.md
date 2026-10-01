A `#name` in a step is an element of the screen in play — `%!screen.element["#name"]%` — never a variable or a text:
- a window's own parts are named under `window`: `#window.bot` (☰), `#window.globe`, `#window.back`, `#window.forward`, `#window.minimize`, `#window.maximize`, `#window.close`, and an app's tools (`#window.save`, `#window.new`, `#window.open`, `#window.run`);
- any other `#id` is an element of a page, by its id.
Its events are bound like any item's with on.event: `on click on #window.bot, call X` → `Event` = `%!screen.element["#window.bot"].on.click%`.
