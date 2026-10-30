Step text: `render 'page.html' with title=%pageTitle%, write to %html%`
Formal: `ui.render(Template="page.html", Parameter=[{title: %pageTitle%}]); variable.set(Name=%html%, Value=%!data%)`

Step text: `render %template%, error=%error%, write to %text%`
Formal: `ui.render(Template=%template%, Parameter=[{error: %error%}]); variable.set(Name=%text%, Value=%!data%)` — every `name=value` after the template is an argument, even when the value is a variable of the same name.

Step text: `render 'layout.html'`
Formal: `ui.render(Template="layout.html")`
