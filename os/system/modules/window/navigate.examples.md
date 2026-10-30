Step text: `navigate window %event.id% to %event.navigate% in %browser%`
Formal: `window.navigate(Window=%event.id%, Url="%event.navigate%", Browser=%browser%)` — a window by its id on the screen: the browser it is in comes with it.

Step text: `send %window% to 'https://www.mbl.is'`
Formal: `window.navigate(Window=%window%, Url="https://www.mbl.is")` — a window from window.open needs no browser.
