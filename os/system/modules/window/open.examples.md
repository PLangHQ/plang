Step text: `open window %asked.open% in %browser%`
Properties: `{"Url": "%asked.open%", "Browser": "%browser%"}` — `%browser%` holds a running browser (from browser.start).

Step text: `open window 'start.html' in %browser%, write to %window%`
Properties: `{"Url": "start.html", "Browser": "%browser%"}` — the trailing `write to %window%` is its own action and holds the window.
