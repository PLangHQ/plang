plang UI uses HTML, css and javascript to display GUI

plang allows you to describe then intented GUI in code and also modify each html,css and javascript, allowing for full flexibility

plang will support Website, Desktop, Mobile, Tablet, TV and Watch interfaces. At current moment only Website is supported. 

## Website

Each GUI has a main layout page, allowing for consistent interface across the website. 
Setting the main layout, also define what css framework you are using when using a standard css framework the LLM knows. 

```plang
- set main layout "/ui/layout.html", css framework="uikit", default target="#main"
```

The layout.html will be rendered with any request coming to the webserver that is not an ajax request(%request!IsAjax%).
The default target is the default location the content from the server should be rendered.

when rendering a some part of webpage, you can use the keywords '- [ui] render template...'

Here is an example

```plang
- [ui] render template "product.html"
```

This will render the product.html straight to the default output stream, displaying it to the user on the client side.

when interacting with the backend you can instruct it to render content in specific places on the page. You use attributes to do this.

attributes available to use in ui is following
attributes are prefixed with p-, e.g. p-target, p-actions

p-target => where should the content rendered when response comes back from backend
p-actions => what action should be executed when response comes from backend, this can be a space
    separated list of actions.
    replace (same as innerHTML in html)
    replaceSelf (same as outerHTML in html)
    append
    prepend
    scrollIntoView
    scrollToTop
    focus
    show => sets style property to display:block
    hide => sets style property to display:none
    notify => shows a notification bar to user
    alert => alert box shown to user
    navigate => tells the browser to change the navigation history
    reload => reload the page
p-before-actions => name of a javascript function on window, called with (message, ctx) after the
    response arrives but before any action is applied, so the response can be inspected or changed
p-after-actions => name of a javascript function on window, called with (message, ctx) after the
    actions have been applied to the dom

p-target and p-actions are sent to the server as request headers and are readable there as
%request!p-target% and %request!p-actions%. p-before-actions and p-after-actions are handled on the
client only and are never sent.

Any other p-* attribute is passed through to the server as a request header of the same name.
Because of that a misspelled attribute does not fail, it just silently does nothing on the client -
so the names above have to match exactly. In particular the actions attributes are plural:
p-actions, p-before-actions, p-after-actions.

Here is an example of loading a page

```html
<a href="product/my-product">Show my product</a>
```
When you have set the layout with default target, not target is required in a link. The default behavior is to replace(using innerHTML) the content in the target. 

on the server side, the code would be something like this

```
Start
/ First we start a webserver and load routes that are available
- start webserver, call AddRoutes

AddRoutes
/ lets create a route called my-product that will call the RenderProduct goal
- add route "my-product", call MyProduct

MyProduct
/ first we load data for my-product into the %product% variable, so we can use it in our template
- select * from product where slug='my-product', return 1, write to %product%
- [ui] render template "/ui/product.html"
```



```html
<a href="/sidebar" p-target="#sidebar">Load sidebar</a>
```

The server side could be something like this

```plang
AddRoutes
- add route "/sidebar" call Sidebar

Sidebar
- set default value of %target% = %request!p-target%, else "sidebar2"
- [ui] render "sidebar.html", target: %target%
```

We can overwrite the target on the server side, or as in this case, we use the target from the client if there is one, otherwise we use the sidebar2


```html
<a href="notification" p-target="#notification" p-actions="show focus" p-after-actions="afterAction">Show notification</a>
```

afterAction is a function on window, it runs once the response has been rendered into #notification

```html
<script>
function afterAction(message, ctx) {
    // the dom has been updated at this point
}
</script>
```

A form works the same way, plang intercepts the submit, posts the form data in the background and
applies the response to the target

```html
<form action="/teacher/Add" method="post" p-target="#result" p-actions="replace" p-after-actions="afterAdd">
    <input type="text" name="name">
    <button type="submit">Add</button>
</form>
```
