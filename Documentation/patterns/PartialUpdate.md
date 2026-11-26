# Partial update

A filter, a "show more" or a "details" button changes one part of a page. Loading the whole page again runs every query of the
page and redraws everything for one list. The pattern: the page renders each part into its own element, a control calls a
goal with `plang.callGoal`, and that goal renders only that part, with `target`. The goal names the target.

## 1. The page renders the part into its own element

The part is its own template. The page renders it into a variable and puts it in an element with an id:

```plang
/ product/Products.goal
Products
- select id, name, category from products order by name, write to %products%
- [ui] render "list.html" without layout, write to %productList%
- [ui] render "products.html", navigate
```

```html
<!-- product/list.html -->
<ul>{{ for p in products }}<li>{{ p.name }}</li>{{ end }}</ul>
```

```html
<!-- product/products.html -->
<h1>Products</h1>
<select onchange="plang.callGoal('/product/list', { category: this.value })">
<option value="">all</option><option value="chairs">chairs</option><option value="tables">tables</option>
</select>
<div id="productList">{{ productList }}</div>
{{ for p in products }}
<p>{{ p.name }} <button onclick="plang.callGoal('/product/detail/{{ p.id }}')">details</button></p>
<div id="detail-{{ p.id }}"></div>
{{ end }}
```

`without layout` matters: inside a request, a render into a variable without it gets the whole layout around it.

## 2. The goal renders the part into the element

An id goes in the route's path, with its type. The goal reads it as a bare `%id%` and builds the target from it:

```plang
/ product/Detail.goal
Detail
- select name, category from products where id=%id%, return 1 row, write to %product%
- [ui] render "detail.html", target: "#detail-%id%"
```

More data, a form or the value of a control, goes in the body. The goal reads it as `%request.body.x%`, and the target is fixed:

```plang
/ product/List.goal
List
- set %category% = %request.body.category%
- if %category% is empty then
    - select id, name, category from products order by name, write to %products%
- if %category% is not empty then
    - select id, name, category from products where category=%category% order by name, write to %products%
- [ui] render "list.html", target: "#productList"
```

```plang
AddRoutes
- add route "/product", call /product/Products
- add route "/product/list", POST, call /product/List
- add route "/product/detail/%id%(number)", POST, call /product/Detail
```

- `plang.callGoal(url, data)` is a signed POST with `data` as a JSON body (`plang.callGoal('/product/detail/42')` sends none).
  Every url it calls needs its own route, and the route must say `POST`: a route with no method takes only GET, and the call
  ends in 404 without changing the page.
- `target: "#productList"` replaces what is inside that element and nothing else. The same `list.html` is used for the first
  render and for every update.
- The promise `callGoal` returns resolves when the answer has been applied, so a script can go on after `await`.
- `(number)` is checked: `/product/detail/abc` and `/product/detail/1.5` do not match the route, so the next route is tried
  and otherwise the answer is 404. The goal gets `%id%` as a number. A goal still checks that `%product%` was found, since a
  number that is not in the table is a valid id.
- Build the url in the template (`'/product/detail/{{ p.id }}'`), never pass the id to javascript as a number: row ids are
  large, and javascript rounds them.

## 3. A target the page sends

The page can send the selector, and the goal render into it:

```html
<button onclick="plang.callGoal('/product/detailto/{{ p.id }}', { target: '#detail-{{ p.id }}' })">details</button>
```

```plang
/ product/DetailTo.goal
DetailTo
- select name, category from products where id=%id%, return 1 row, write to %product%
- [ui] render "detail.html", target: %request.body.target%
```

It works, with two drawbacks. Both sides must agree on the element's name, so a change in one breaks the other. And the
browser can send any selector: `{ target: '#main' }` replaced the whole page content with the detail. Use the goal's own target.

## Why this way

- **The target comes from the server.** `callGoal` has no `target` option, by design: where a goal's answer goes is the goal's
  render step.
- **An id in the path, data in the body.** The path says which thing (`/product/detail/42`); the body carries what the visitor
  typed or chose (`%request.body.full_name%`). A bare `%x%` is set only from the route's path, never from the body.
- **A callback takes the answer instead of the page.** `plang.callGoal(url, data, function (message) { … })` calls the function
  with the answer (`message.content` is the rendered html) and does not apply it to the page. Pass nothing when the goal's
  render should land. Options (`onSuccess`, `onFailure`, …) go in the same place; a key that starts with `p-` is sent as a
  header of that name.
- **A link is a worse control for a part.** A GET link to a url whose goal renders a part works when the client follows it,
  but the same url opened in a new tab or from a bookmark returns only the piece, with no page around it.
- **Do not reload the page for one part.** A whole-page render reruns every query of the page.

`window.plang.fetch(url, { method: 'POST', body: JSON.stringify(data) })` reaches the same route and is applied the same way.
Use `plang.callGoal` for a control that calls a goal ([Web server](../Webserver.md#calling-a-route-from-a-script)).

See also: [Page layout](./PageLayout.md), [First page load](./FirstPageLoad.md), [App folders](./AppFolders.md),
[Web server](../Webserver.md).
