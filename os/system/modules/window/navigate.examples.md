Step text: `navigate window %event.id% to %event.navigate% in %browser%`
Formal: `window.navigate(Window: item = %event.id%, Url: text = "%event.navigate%", Browser: browser = %browser%)` — a window by its id on the screen: the browser it is in comes with it.

Step text: `send %window% to 'https://www.mbl.is'`
Formal: `window.navigate(Window: item = %window%, Url: text = "https://www.mbl.is")` — a window from window.open needs no browser.
