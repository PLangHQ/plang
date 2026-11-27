# Page layout

A web app needs every page in the same frame (head, menu, footer) and needs the browser to talk to the app through the plang
js client. The pattern: put the client in the app, write one layout with `{{ main }}` where a page goes, set it once in
`Start`, and let each page goal render its own template with `navigate`.

## 1. The files

```
myapp/
  plang-sw.js              <- js/plang/dist/plang-sw.js from the plang release, at the site root
  js/plang/dist/...        <- the whole js/plang/dist/ folder from the plang release
  ui/layout.html
  Start.goal
  home/Home.goal, home/home.html
  product/Products.goal, product/products.html
```

- The client comes with every plang release, in `js/plang/`: `dist/` (the client), `VERSION` (its number) and `README.md`.
- `plang-sw.js` must be at the root, so it is served as `/plang-sw.js` and covers the whole site. It signs page loads, so the
  server knows who is asking on a plain page load (see [First page load](./FirstPageLoad.md)).
- Do not edit the copied files. Copy them again when plang is updated, and change a line in `plang-sw.js` on every deploy
  (`// build <commit>`), so open tabs pick up the new version.
- A page's goal and its template sit in the folder of the thing it shows (`product/`), see [App folders](./AppFolders.md).

## 2. The layout

```html
<!-- ui/layout.html -->
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>Shop</title>
<script type="module" src="/js/plang/dist/v13/main.js"></script>
</head>
<body>
<nav><a href="/">Home</a> <a href="/product">Products</a></nav>
<main id="main">{{ main }}</main>
</body>
</html>
```

- Load the client with the number in `js/plang/VERSION` in the path: `/js/plang/dist/v13/main.js`. The web server finds
  `js/plang/dist/main.js` for it. A new number is a new address, so a browser never runs a cached old module next to new ones.
- `{{ main }}` is where a page's content goes. The layout is a template: it sees the variables of the request, so a part of
  the frame that depends on the visitor is a variable here (see [First page load](./FirstPageLoad.md)).
- A link inside the app is followed by the client: the request is signed, and only `#main` is replaced. The page is not
  loaded again, so scripts and state in the frame stay.

## 3. Set the layout once, in Start

```plang
Start
- [ui] set "/ui/layout.html" as default layout, default render variable "main"
- start webserver, port: 8080, on start call AddRoutes

AddRoutes
- add route "/", call /home/Home
- add route "/product", call /product/Products
- add route "/product/list", POST, call /product/List
- add route "/product/detail/%id%(number)", POST, call /product/Detail
```

Each url is a route to a goal. A route is needed for every url the browser asks for, also for a goal that a control on the
page calls ([Partial update](./PartialUpdate.md)). A small app adds all its routes in one `AddRoutes` goal in `Start.goal`.

When the list grows, split it by area into `routes/`, one goal per area, and call them from `AddRoutes`:

```plang
AddRoutes
- add route "/", call /home/Home
- call goal /routes/Product
- call goal /routes/User
```

```plang
/ routes/Product.goal
Product
- add route "/product", call /product/Products
- add route "/product/list", POST, call /product/List
- add route "/product/detail/%id%(number)", POST, call /product/Detail
```

`routes/` is the one folder not named for a thing ([App folders](./AppFolders.md)). Keep it at the top, never inside a guarded
folder: `admin/Routes.goal` would run the admin gate when the app starts, with no user, and fail. Routes only for development
(`routes/Dev.goal`) are added only in dev ([Start and environments](./StartAndEnvironments.md)).

## 4. A page renders into the layout

```plang
/ product/Products.goal
Products
- select id, name, category from products order by name, write to %products%
- [ui] render "products.html", navigate
```

- `navigate` sends the page: on a plain page load the server wraps it in the layout and returns the whole html; when the
  client follows a link, only the content is sent and put into `#main`, and the address bar shows the url.
- A template path without a leading `/` is next to the goal: `products.html` is `product/products.html`.
- To render a piece of html into a variable inside a request, say `without layout`:

  ```plang
  - [ui] render "list.html" without layout, write to %productList%
  ```

  Without it, `render "list.html", write to %productList%` in a request wraps the piece in the whole layout, and the page
  shows the layout inside itself.

## 5. A page with another layout

A layout set while a request is handled is for that request only; every other request keeps the layout from `Start`:

```plang
/ product/Print.goal
Print
- [ui] set "/ui/print.html" as default layout, default render variable "main"
- select id, name, category from products order by name, write to %products%
- [ui] render "list.html", navigate, redraw the whole body inside the layout
```

A link in the app only replaces `#main`, so the old menu would stay around the print view. `redraw the whole body inside the
layout` sends the page inside its own layout and replaces the body. A plain page load of `/product/print` gets the print
layout either way.

## Why this way

- **Set the layout in Start, not in `on goal begin request`.** A layout set in a request is for that request only, so setting
  the same layout at the start of every request repeats on every request what `Start` sets once. Set it in a request only for
  the page that uses a different one.
- **Write no fetch code of your own.** A request the client does not make carries no identity, and what the server sends back
  is not applied to the page. Use links, forms, `plang.callGoal` and `window.plang.fetch`
  ([Web server](../Webserver.md#calling-a-route-from-a-script)).

See also: [Partial update](./PartialUpdate.md), [First page load](./FirstPageLoad.md), [App folders](./AppFolders.md),
[Web server](../Webserver.md), [Identity in the browser](../IdentityInTheBrowser.md).
