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
attributes are prefixed with p-, e.g. p-target, p-action

p-target => where should the content rendered when response comes back from backend
p-action => what action should be executed when response comes from backend, this can be list of actions. 
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
p-before-request => allows user to manipulate the request before it is sent to server
p-before-action => allows user to manipulate response before the action is called 
p-after-action or p-execute => executes after action is applied

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
<a href="notification" p-target="#notification" p-action="show focus" p-execute="afterAction">Show notification</a>
```

