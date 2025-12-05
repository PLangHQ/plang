# PLang User Interface Reference

## Overview

PLang uses HTML as its rendering engine with **Scriban** as the template engine. The UI module handles template rendering, DOM manipulation, user interactions, and navigation. All UI operations use the `[ui]` module hint for clarity.

**Related references:**
- [ui-patterns.md](ui-patterns.md) - Common UI patterns, UIKit components, actions reference
- [ui-interactions.md](ui-interactions.md) - User interaction, navigation, dialogs, notifications

## Client-Server Communication

PLang uses a streaming response model for efficient UI updates:

1. **First Request**: Full server-side render of the complete page
2. **Subsequent Requests**: AJAX calls with responses streamed as `application/plang+jsonl`
3. **Response Handling**: The `plang.js` client library processes each command and updates the DOM immediately

**How it works:**
- PLang intercepts all link clicks and form submissions
- Requests are automatically signed for security
- Server pushes commands (render, navigate, notify, etc.) as JSON lines
- Client executes each command in sequence, updating the UI progressively

This enables fast, incremental UI updates without full page reloads.

### Calling Goals from JavaScript

Use `plang.callGoal()` to call server-side goals from JavaScript. **Let the server decide where to render content** - don't handle the response in JavaScript:

```javascript
// CORRECT: Let the goal handle rendering
plang.callGoal('/product/AddToCart', { productId: '{{ product.id }}' });

// CORRECT: With parameters
plang.callGoal('/admin/campaign/PreviewEmail', { campaignId, userId });
```

The goal handles everything - fetching data, rendering, and targeting:

```plang
PreviewEmail
- select * from campaigns where id=%campaignId%, return 1, write to %campaign%
- select * from users where id=%userId%, return 1, write to %user%
- [ui] render "email-preview.html", target="#previewContainer"
```

Or show as modal:

```plang
PreviewEmail
- select * from campaigns where id=%campaignId%, return 1, write to %campaign%
- [ui] render "email-preview.html", show as modal
```

**Anti-pattern - don't do this:**

```javascript
// WRONG: Don't handle rendering in JavaScript
plang.callGoal('/campaign/PreviewEmail', { campaignId })
    .then(result => {
        document.getElementById('preview').innerHTML = result.html;
    });
```

**Why server-side rendering decisions:**
- Consistent behavior across all entry points
- Server knows the current state and context
- Easier to change UI without updating JavaScript
- Security - server controls what gets displayed where

## Template Engine: Scriban

PLang uses Scriban syntax for templates. Variables are accessed with double curly braces.

```html
{{ variable }}
{{ object.property }}
{{ if condition }}...{{ end }}
{{ for item in collection }}...{{ end }}
```

### Working with IDs (Important)

When working with `.id` properties on objects, IDs are `long` type and must be wrapped in quotes **in JavaScript/HTML** (not in PLang code):

```html
<!-- CORRECT: Wrap id in quotes in HTML/JavaScript -->
<a href="/product/{{ product.id }}">{{ product.name }}</a>
<input type="hidden" name="productId" value="{{ product.id }}">
<button onclick="deleteItem('{{ item.id }}')">Delete</button>
```

```plang
/ In PLang goals, NO quotes needed around variables
- set %productId% = %product.id%
- call goal DeleteItem itemId=%item.id%
- select * from products where id=%productId%, return 1, write to %product%
```

### Common Scriban Patterns

```html
<!-- Conditionals -->
{{ if user }}
    <p>Welcome, {{ user.name }}</p>
{{ else }}
    <p>Please log in</p>
{{ end }}

<!-- Loops -->
{{ for product in products }}
    <div class="product">
        <h3>{{ product.name }}</h3>
        <span>{{ product.price | math.format "n0" }}</span>
    </div>
{{ end }}

<!-- Empty checks -->
{{ if products.size > 0 }}
    <!-- show products -->
{{ else }}
    <p>No products found</p>
{{ end }}

<!-- Formatting -->
{{ amount | math.format "n0" }}           <!-- Number: 1,234 -->
{{ date | date.to_string "%Y-%m-%d" }}    <!-- Date formatting -->
{{ text | string.truncate 100 }}          <!-- Truncate text -->

<!-- JSON output for debugging -->
{{ item | object.to_json }}
```

## Template Composition (Partials)

For maintainability, split large templates into smaller, focused files using `{{ render "file.html" }}`. This keeps templates readable and encourages reuse.

### When to Split Templates

Split a template when:
- It exceeds ~100 lines
- It contains distinct logical sections
- Parts could be reused elsewhere
- Different team members work on different sections

### Basic Partial Rendering

```html
<!-- product.html (main template) -->
<div class="uk-container">
    <h1>{{ product.name }}</h1>
    <p>{{ product.description }}</p>
    
    {{ render "gallery.html" }}
    
    {{ render "variants.html" }}
    
    {{ render "specs.html" }}
    
    {{ render "reviews.html" }}
</div>
```

The PLang goal stays simple - it loads all data, then renders the main template:

```plang
Product
- select * from products where id=%id%, return 1, write to %product%
- select * from productImages where productId=%id%, write to %images%
- select * from variants where productId=%id%, write to %variants%
- select * from productSpecs where productId=%id%, write to %specs%
- select * from reviews where productId=%id%, write to %reviews%
- [ui] render "product.html", navigate
```

### Partial Templates

Each partial has access to all variables from the parent context:

```html
<!-- variants.html -->
<div class="uk-margin-large">
    <h3>Available Options</h3>
    {{ if variants.size > 0 }}
    <div class="uk-grid uk-child-width-1-3@m" uk-grid>
        {{ for variant in variants }}
        <div>
            <div class="uk-card uk-card-default uk-card-body">
                <h4>{{ variant.name }}</h4>
                <p class="uk-text-large">{{ variant.price | math.format "n0" }} kr.</p>
                <button class="uk-button uk-button-primary" 
                        onclick="addToCart('{{ variant.id }}')">
                    Add to Cart
                </button>
            </div>
        </div>
        {{ end }}
    </div>
    {{ else }}
    <p class="uk-text-muted">No variants available</p>
    {{ end }}
</div>
```

```html
<!-- specs.html -->
<div class="uk-margin-large">
    <h3>Specifications</h3>
    {{ if specs.size > 0 }}
    <table class="uk-table uk-table-striped">
        <tbody>
            {{ for spec in specs }}
            <tr>
                <td class="uk-text-bold">{{ spec.name }}</td>
                <td>{{ spec.value }}</td>
            </tr>
            {{ end }}
        </tbody>
    </table>
    {{ else }}
    <p class="uk-text-muted">No specifications available</p>
    {{ end }}
</div>
```

### Nested Partials

Partials can render other partials:

```html
<!-- reviews.html -->
<div class="uk-margin-large">
    <h3>Customer Reviews ({{ reviews.size }})</h3>
    {{ for review in reviews }}
        {{ render "review-card.html" }}
    {{ end }}
</div>
```

```html
<!-- review-card.html -->
<div class="uk-card uk-card-default uk-card-body uk-margin">
    <div class="uk-flex uk-flex-between">
        <span class="uk-text-bold">{{ review.author }}</span>
        <span>{{ review.rating }}/5 ★</span>
    </div>
    <p>{{ review.text }}</p>
</div>
```

## Rendering Templates

### Basic Rendering

```plang
/ Simple render
- [ui] render "template.html"

/ Render with navigation (updates browser URL, scrolls to top)
- [ui] render "template.html", navigate and scroll to top

/ Render to specific target
- [ui] render "template.html", target="#container"

/ Render with action
- [ui] render "template.html", target="#content", append
- [ui] render "template.html", target="#content", prepend
- [ui] render "template.html", target="#content" and replace
```

### Render Actions

| Action | Description |
|--------|-------------|
| `append` | Add content at end of target |
| `prepend` | Add content at beginning of target |
| `replace` | Replace target's innerHTML |
| `replaceSelf` | Replace target's outerHTML (replaces the element itself) |
| `appendOrReplace` | Append content, but if element with same ID exists, replace it |
| `prependOrReplace` | Prepend content, but if element with same ID exists, replace it |

### Layout System

PLang intercepts default click and form behavior, signs all requests, and injects responses into a designated container (usually `#main`). The first request renders server-side, subsequent requests use AJAX with responses streamed as `application/plang+jsonl`.

```plang
/ Basic layout setup - call this on webserver request
- [ui] set "layout.html" as default layout

/ With CSS framework (uikit is default)
- [ui] set "layout.html" as default layout, css framework="uikit"

/ Full configuration
- [ui] set "layout.html" as default layout, css framework="uikit", default render target="#main"

/ Custom CSS framework
- [ui] set "layout.html" as default layout, set css="mycustom.css", default target="#main", variable="main"
```

**Setup in webserver:**
```plang
Start
- start webserver, on request call OnRequest

OnRequest
- [ui] set "layout.html" as default layout
```

**layout.html example:**
```html
<!DOCTYPE html>
<html>
<head>
    <title>{{ title | default "My App" }}</title>
    <link rel="stylesheet" href="/css/uikit.min.css">
    <script src="/js/plang.js"></script>
    <script src="/js/uikit.min.js"></script>
</head>
<body>
    <nav><!-- navigation --></nav>
    <main id="main">
        {{ main }}
    </main>
    <footer><!-- footer --></footer>
</body>
</html>
```

**Important:** 
- The `#main` element is where AJAX content gets injected
- The `{{ main }}` Scriban variable renders the initial server-side content
- Always include `/js/plang.js` - it handles client-server communication
- First request: full page render; subsequent requests: AJAX into `#main`

### CSS Selector Targeting

```plang
/ Target by ID
- [ui] render "content.html", target="#main"

/ Target by class
- [ui] render "items.html", target=".item-list", append

/ Target by CSS selector
- [ui] render template "nav.html", cssSelector:"header nav", action:replace
```

## DOM Manipulation

### Setting Element Content

```plang
/ Set element inner HTML by ID
- set element "#message" = "<strong>Success!</strong>"
- set element "#counter" = "%count%"

/ Set element text content
- set '#status' = "Processing..."
- set '#total' = "Total: %amount% kr."

/ With variable interpolation
- set element "#greeting" = "Hello, %user.name%!"
```

### Setting Attributes

```plang
/ Set single attribute
- [ui] set checked="checked" attribute on "#checkbox"
- [ui] set disabled="disabled" attribute on "#submitBtn"
- [ui] set value="%inputValue%" attribute on "#textField"

/ Set href attribute
- [ui] set href="/product/%product.id%" attribute on "#productLink"

/ Set data attributes
- [ui] set data-id="%item.id%" attribute on "#listItem"
```

### CSS Classes

```plang
/ Add class
- set class="active" to #navItem
- set class="uk-hidden" to #modal
- set class="slide-up" to #notification
```

### Removing Elements

```plang
/ Remove element by selector
- [ui] remove '#element'
- [ui] remove '.temp-item'
- [ui] remove '#notification-%notificationId%'
```

### Show/Hide Elements

```plang
/ Show element (sets display: block)
- [ui] show '#modal'
- [ui] show '.content'

/ Hide element (sets display: none)
- [ui] hide '#loading'
- [ui] hide '.dropdown'
```

**Note:** These use inline styles. For CSS framework integration (UIKit, Bootstrap), use class manipulation instead:

```plang
/ UIKit - use uk-hidden class
- set class="uk-hidden" to #element      / hide
- [ui] remove class "uk-hidden" from #element  / show

/ Bootstrap - use d-none class
- set class="d-none" to #element

/ Tailwind - use hidden class  
- set class="hidden" to #element
```

## Web Server Routes

### Defining Routes

```plang
Start
- start webserver
- add route '/products', call ShowProducts
- add route '/product/%productId%', call ShowProduct
- add route '/product/save', POST, call SaveProduct
- add route '/api/search', call SearchProducts
```

### Route with Parameters

```plang
ShowProduct
/ %productId% is automatically available from route
- select * from products where id=%productId%, return 1, write to %product%
- if %product% is empty then
    - [ui] render "404.html", navigate
    - end goal
- [ui] render "product.html", navigate and scroll to top
```

**Note:** Rendering "404.html" automatically returns status code 404 to the browser. See [error-handling.md](error-handling.md) for more on error responses and goal termination.

## File Organization

Templates live in a `templates` folder alongside the goals that use them:

### Single Goal in Folder

```
/product
  Product.goal
  /templates
    product.html
    variants.html
    specs.html
    reviews.html
```

### Multiple Goals in Folder

When a folder has multiple goals, organize templates by goal name:

```
/admin
  Products.goal
  Users.goal
  Orders.goal
  /templates
    /products
      list.html
      edit.html
      filters.html
    /users
      list.html
      detail.html
    /orders
      list.html
      detail.html
      items.html
```

### Full Project Structure

```
/project
  Start.goal
  Events.goal
  /layouts
    layout.html
  /product
    Product.goal
    /templates
      product.html
      variants.html
      specs.html
      gallery.html
  /products
    Products.goal
    /templates
      products.html
      product-card.html
      filters.html
      pagination.html
  /checkout
    Checkout.goal
    Payment.goal
    /templates
      /checkout
        cart.html
        shipping.html
        summary.html
      /payment
        form.html
        success.html
        error.html
  /admin
    Products.goal
    Users.goal
    /templates
      /products
        list.html
        edit.html
      /users
        list.html
        edit.html
  /api
    Search.goal
    Cart.goal
  /shared
    /templates
      breadcrumbs.html
      pagination.html
      empty-state.html
```

### User Identity and Protected Routes

The first request to the server is unsigned, so `%Identity%` is not available. To access user identity, use the `on connect` event:

```plang
Start
- start webserver, on connect call UserConnects

UserConnects
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user.role% contains "admin" then call RenderAdminNav
```

This ensures `%Identity%` is established before checking user roles or permissions.

### Guidelines

- **Keep templates close to goals** - easier to find and maintain
- **Flat structure when possible** - only create subfolders when you have multiple goals sharing a templates folder
- **Shared templates** - put truly shared partials (pagination, breadcrumbs) in a `/shared/templates` folder
- **Layouts are global** - keep layouts in a root `/layouts` folder since they're used everywhere

## Best Practices

1. **Use the `[ui]` module hint** for clarity in UI operations

2. **Split large templates** using `{{ render "partial.html" }}` - keep templates under 100 lines

3. **Separate concerns**: Keep templates simple, business logic in goals

4. **Use layouts** for consistent page structure

5. **Show loading states** for async operations

6. **Validate before submit** to improve user experience

7. **Use semantic HTML** and accessibility attributes

8. **Prefer replace over remove+append** for smoother updates

9. **Handle errors gracefully** with user-friendly messages