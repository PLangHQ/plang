Step text: `call ShowFiles files=%files% in %browser.desktop%`
Formal: `window.call(Name="ShowFiles", Parameter=[{files: %files%}], Window=%browser.desktop%)` — window.call, not goal.call: `in %window%` puts the goal in the window's page, not in this app.

Step text: `call Show text=%text%, file=%file% in %window%`
Formal: `window.call(Name="Show", Parameter=[{text: %text%}, {file: %file%}], Window=%window%)`

Step text: `call Saved in window %asked.from% of %browser%`
Formal: `window.call(Name="Saved", Window=%asked.from%, Browser=%browser%)` — a window by its id on the screen: the browser it is in comes with it.
