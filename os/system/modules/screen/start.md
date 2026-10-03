# Screen Module
Screen: a window on this machine to draw frames in (from a browser, from PlangOS), with its mouse and keyboard given back as input events

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

## draw
Draw a frame in a screen: a JSON frame line {"frame":"<base64>",…} (what browser OnFrame gives) or an image; other lines are ignored

- draw %!data% on %screen%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Data | draw <data> | item | yes | — | the frame to draw: a JSON frame line (`{"frame":"<base64>",…}`, what a browser's OnFrame gives) or an image; lines that aren't frames are ignored |
| Screen | on %screen% | screen | yes | — | the screen to draw into, from screen.open |

**Returns:** nothing; it draws the frame into the screen.

## send
Give PlangOS's screen one line of JSON: an input event from the host (mouse, key, text, clipboard) or a command for one of its windows

- send %!data% to %screen%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Data | send <data> | item | yes | — | one line of JSON for the screen: an input event (`{"mouse":…}`, `{"key":…}`, `{"text":…}`, `{"clipboard":…}`) or a window command (`{"window":"focus|minimize|maximize|restore|close","id":…}`) |
| Screen | to %screen% | screen | yes | — | the screen to give it to, from screen.open |

**Returns:** nothing on success; a ScreenNotOpen error when the screen isn't open.

## close
Close a screen window

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Screen | close screen %screen% | screen | yes | — | the screen to close, from screen.open |

**Returns:** nothing.

## open
Open a window to draw frames in; returns the screen at once; each mouse/keyboard event calls OnInput with `%!data%` = one JSON line; closing the window calls OnClose

- open screen 'PlangOS', size 1920x1080, on input call Input, on close call Close, write to %screen%
- open screen size 1920x1080, frames to output, on window call Window, write to %screen%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Title | screen '<title>' | text | no | PlangOS | the window's title-bar text |
| Width | size <width>x<height> | number | no | 1920 | the drawing area's width in pixels |
| Height | size <width>x<height> | number | no | 1080 | the drawing area's height in pixels |
| OnInput | on input call <Goal> | action | no | — | a goal called for each input event, the event an input value in %!data% |
| OnClose | on close call <Goal> | action | no | — | a goal called when the window is closed |
| ToOutput | frames to output | bool | no | false | PlangOS (Linux): send the frames to this app's own output instead of to a window |
| OnWindow | on window call <Goal> | action | no | — | PlangOS (Linux): a goal called as a window on the screen opens, is focused, moved, closed, or asks to navigate |

**Returns:** the screen, held so later steps draw into it (draw … on %screen%).

## listen
Let PlangOS's screen take this app's input itself: each line the host sends (mouse, key, text) goes straight to the screen, no goal per line; returns when the input ends

- the screen %screen% listens to the input

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Screen | the screen %screen% listens | screen | yes | — | the screen that takes this app's input directly, line by line, from screen.open |

**Returns:** nothing; it returns when the input ends (the host closed it).
