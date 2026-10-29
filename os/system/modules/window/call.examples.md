Step text: `call ShowFiles files=%files% in %browser.desktop%`
Formal: `window.call(Name: text = "ShowFiles", Parameter: list = [{files: item = %files%}], Window: item = %browser.desktop%)` — window.call, not goal.call: `in %window%` puts the goal in the window's page, not in this app.

Step text: `call Show text=%text%, file=%file% in %window%`
Formal: `window.call(Name: text = "Show", Parameter: list = [{text: item = %text%}, {file: item = %file%}], Window: item = %window%)`

Step text: `call Saved in window %asked.from% of %browser%`
Formal: `window.call(Name: text = "Saved", Window: item = %asked.from%, Browser: browser = %browser%)` — a window by its id on the screen: the browser it is in comes with it.
