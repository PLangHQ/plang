A screen is a window that shows frames drawn into it (`draw %!data% on %screen%`) — not a browser and not a terminal. `screen.open` returns at once and hands you the screen; later steps draw into it, send it events, or let it take the input itself.

`OnInput` is called for each input event, the event an input value in `%!data%` — `if %!data% is input`, `if %!data% is mouse`, its kind `%!data!type.kind%`. The clipboard arrives as a clipboard value, and lines that aren't values yet (stats) as text. Pass it on as-is (`send %!data% to %browser%`). `OnClose` runs when the window is closed; stop whatever feeds it there.

## Two kinds

A screen is one of two kinds, and each kind owns what it does:

- `window` — a window on the host (Windows) that shows the frames and gives out its mouse and keyboard through `OnInput`.
- `display` — PlangOS's screen (Linux) that programs draw onto; it takes the host's input (`screen.send`, `screen.listen`) and sends its frames up (`frames to output`).

`%screen!type.kind%` reads which one (`window` or `display`), and `if %screen% is display` tests it. `if %screen% is screen` asks the type instead. (Don't write `if %screen% is window` to test the kind — `is window` asks the *window* type, not a screen's kind.)

## Reading a screen

A screen's facts read with a dot:

- `%screen.Title%`, `%screen.Width%`, `%screen.Height%` — what it opened with.
- `%screen.Closed%` — true once it is closed; `%screen.Frames%` — how many frames have been drawn.
- `%screen.Verbose%` — whether it narrates what happens to its windows; `set %screen.verbose% to true` turns it on (its notes go to the debug output under `--debug`, else the error output, never to a goal).
- `%screen.element["#sel"]%` — one of its elements, picked by selector, to bind on its events.
