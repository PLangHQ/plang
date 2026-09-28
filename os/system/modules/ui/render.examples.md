Step text: `render 'page.html' with title=%pageTitle%, write to %html%`
Properties: `{"Template": "page.html", "Parameter": {title: %pageTitle%}}`

Step text: `render %template%, error=%error%, write to %text%`
Properties: `{"Template": "%template%", "Parameter": {error: %error%}}` — every `name=value` after the template is an argument, even when the value is a variable of the same name: `ui.render(Template=%template%, Parameter={error: %error%})`.

Step text: `render 'layout.html'`
Properties: `{"Template": "layout.html"}`
